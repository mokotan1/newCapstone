from __future__ import annotations

import logging
import os
import secrets
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import StreamingResponse

from config import get_settings
from local_runtime import build_chat_providers, check_local_runtime
from models.requests import ChatRequest, TelemetryIngestRequest, TutorGradeRequest
from models.responses import ChatResponse, TelemetryResponse, TutorGradeResponse
from providers.base import AIProvider
from services.chat_auth import verify_chat_api_token
from services.chat_service import ChatService
from services.local_ai.cuda_manifest import cuda_manifest_is_pinned
from services.local_ai.hardware import probe_nvidia
from services.local_ai.paths import default_local_ai_dir
from services.local_ai.process_host import ProcessEngineHost
from services.local_ai.router import router as local_ai_router
from services.local_ai.runtime_manager import LocalRuntimeManager
from services.locale_support import (
    all_engines_failed_message,
    api_key_required_message,
)
from services.quiz_bank import QuizBank
from services.rate_guard import configure_rate_guard, enforce_chat_rate_limits
from services.rate_limit import build_rate_limiter
from services.sse_format import format_sse_event
from services.telemetry_service import TelemetryService
from services.tutor_grade import grade_tutor_answer
from services.tutor_rag_service import TutorRAGService
from tools.game_tools import GAME_TOOLS
from tools.registry import ToolRegistry

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Bootstrap
# ---------------------------------------------------------------------------
settings = get_settings()
runtime_manager: LocalRuntimeManager | None = None

registry = ToolRegistry()
registry.register_many(GAME_TOOLS)

_first_available, _second_available = build_chat_providers(settings)

if _first_available is None:
    logger.critical("No AI provider configured – server will reject all /chat requests")
elif settings.ai_provider == "local":
    runtime_status = check_local_runtime(settings)
    if runtime_status.error:
        logger.warning("Local AI runtime not ready: %s", runtime_status.error)
    else:
        logger.info(
            "Local AI runtime ready model=%s url=%s",
            settings.local_ai_model,
            settings.local_ai_base_url,
        )

_backend_dir = Path(__file__).resolve().parent
_quiz_bank = QuizBank.load(_backend_dir / settings.tutor_quiz_csv_path)
_tutor_rag = TutorRAGService(
    index_path=_backend_dir / settings.tutor_rag_index_path,
    api_key=settings.google_api_key,
    embedding_model=settings.tutor_embedding_model,
    min_similarity=settings.tutor_rag_min_similarity,
)

_telemetry_service: TelemetryService | None = (
    TelemetryService(
        log_dir=_backend_dir / settings.telemetry_log_dir,
        csv_filename=settings.telemetry_csv_filename,
    )
    if settings.telemetry_enabled
    else None
)

chat_service: ChatService | None = (
    ChatService(
        primary=_first_available,
        fallback=_second_available,
        registry=registry,
        temperature=settings.default_temperature,
        max_tokens=settings.max_tokens,
        app_settings=settings,
        tutor_rag=_tutor_rag,
        quiz_bank=_quiz_bank,
    )
    if _first_available
    else None
)


def service_for_provider(provider: AIProvider) -> ChatService:
    """Bind one request to its leased engine; local recovery stays local."""
    return ChatService(
        primary=provider, fallback=None, registry=registry,
        temperature=settings.default_temperature, max_tokens=settings.max_tokens,
        app_settings=settings, tutor_rag=_tutor_rag, quiz_bank=_quiz_bank,
    )


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    global runtime_manager
    limiter = build_rate_limiter(
        enabled=settings.rate_limit_enabled,
        redis_url=settings.redis_url,
        key_prefix="rl:capstone",
    )
    configure_rate_guard(settings, limiter)
    if settings.rate_limit_enabled and not settings.redis_url.strip():
        logger.warning(
            "REDIS_URL unset — using in-process rate limiter only (not suitable for multi-replica).",
        )
    if settings.ai_provider == "local":
        if not settings.local_ai_control_token.strip():
            settings.local_ai_control_token = secrets.token_urlsafe(32)
            token_path = default_local_ai_dir() / "control.token"
            token_path.parent.mkdir(parents=True, exist_ok=True)
            token_path.write_text(settings.local_ai_control_token, encoding="utf-8")
        runtime_manager = LocalRuntimeManager(
            settings_path=default_local_ai_dir() / "settings.json",
            host=ProcessEngineHost(settings),
            hardware=probe_nvidia(),
            gate0_passed=False,
            cuda_manifest_pinned=cuda_manifest_is_pinned(
                Path(__file__).resolve().parent / "data" / "cuda_candidate_manifest.json"
            ),
        )
        await runtime_manager.attach_existing_if_present()
        if runtime_manager.snapshot().state == "stopped":
            await runtime_manager.queue_apply(runtime_manager.snapshot().requested_mode)
        await runtime_manager.start_health_poll()
    try:
        yield
    finally:
        if runtime_manager is not None:
            await runtime_manager.aclose()
        runtime_manager = None
        closer = getattr(limiter, "close", None)
        if closer:
            await closer()


app = FastAPI(title="Disputatio AI Backend", lifespan=lifespan)
app.include_router(local_ai_router)


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------
@app.get("/")
def health_check():
    payload: dict = {"status": "online", "message": "Server is Running!"}
    if settings.ai_provider == "local":
        if runtime_manager is not None:
            snapshot = runtime_manager.snapshot()
            payload["local_runtime"] = {
                "available": snapshot.inference_ready,
                "model_available": snapshot.model_available,
                "error": snapshot.fallback_reason,
            }
            if not snapshot.inference_ready:
                payload["status"] = "degraded"
            return payload
        runtime = check_local_runtime(settings)
        payload["local_runtime"] = {
            "available": runtime.ollama_or_litert_available,
            "model_available": runtime.model_available,
            "error": runtime.error,
        }
        if not runtime.model_available:
            payload["status"] = "degraded"
    return payload


@app.post("/chat", response_model=ChatResponse)
async def chat(request: Request, payload: ChatRequest):
    """Backward-compatible endpoint: returns full response + function_calls at once."""
    if chat_service is None:
        raise HTTPException(
            status_code=500,
            detail=api_key_required_message(payload.locale),
        )

    verify_chat_api_token(request, settings.chat_api_token)
    await enforce_chat_rate_limits(request, payload)

    lease = None
    if runtime_manager is not None:
        lease = await runtime_manager.admit_and_acquire()
    try:
        service = service_for_provider(lease.provider) if lease is not None else chat_service
        result = await service.chat(payload)
    finally:
        if lease is not None:
            await lease.aclose()

    if not result.response and not result.function_calls:
        raise HTTPException(
            status_code=500,
            detail=all_engines_failed_message(payload.locale),
        )

    return result


@app.post("/tutor/grade", response_model=TutorGradeResponse)
async def tutor_grade(request: TutorGradeRequest):
    """LLM 없이 quiz_bank CSV로 정오만 판정합니다."""
    result = grade_tutor_answer(request, _quiz_bank, settings)
    return result


@app.post("/telemetry", response_model=TelemetryResponse)
async def telemetry(request: Request, payload: TelemetryIngestRequest):
    """Unity 플레이 로그(CSV 행)를 서버 logs/play_logs.csv 에 누적한다."""
    if _telemetry_service is None:
        raise HTTPException(status_code=503, detail="telemetry_disabled")

    verify_chat_api_token(request, settings.chat_api_token)

    accepted = _telemetry_service.append_events(payload.events)
    return TelemetryResponse(status="ok", accepted=accepted)


@app.post("/chat/stream")
async def chat_stream(request: Request, payload: ChatRequest):
    """SSE streaming endpoint – tokens arrive in real-time."""
    if chat_service is None:
        raise HTTPException(
            status_code=500,
            detail=api_key_required_message(payload.locale),
        )

    verify_chat_api_token(request, settings.chat_api_token)
    await enforce_chat_rate_limits(request, payload)

    lease = None
    if runtime_manager is not None:
        lease = await runtime_manager.admit_and_acquire()

    async def event_generator():
        try:
            service = service_for_provider(lease.provider) if lease is not None else chat_service
            async for event in service.stream_chat(payload):
                yield format_sse_event(event)
        finally:
            if lease is not None:
                await lease.aclose()

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={
            "Cache-Control": "no-cache",
            "X-Accel-Buffering": "no",
            "Connection": "keep-alive",
        },
    )


if __name__ == "__main__":
    import uvicorn

    port = int(os.environ.get("PORT", 8000))
    host = "127.0.0.1" if settings.ai_provider == "local" else "0.0.0.0"
    uvicorn.run(app, host=host, port=port)
