from __future__ import annotations

import logging
import os
from collections.abc import AsyncIterator
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import StreamingResponse

from config import get_settings
from models.requests import ChatRequest, TutorGradeRequest
from models.responses import ChatResponse, TutorGradeResponse
from providers.groq_provider import GroqProvider
from providers.gemini_provider import GeminiProvider
from services.chat_service import ChatService
from services.quiz_bank import QuizBank
from services.rate_guard import configure_rate_guard, enforce_chat_rate_limits
from services.rate_limit import build_rate_limiter
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

registry = ToolRegistry()
registry.register_many(GAME_TOOLS)

primary = GroqProvider(api_key=settings.groq_api_key, model=settings.default_model_groq) if settings.groq_api_key else None
fallback = GeminiProvider(api_key=settings.google_api_key, model=settings.default_model_gemini) if settings.google_api_key else None

if primary is None and fallback is None:
    logger.critical("No AI provider API keys configured – server will reject all /chat requests")

_first_available = primary or fallback
_second_available = fallback if primary else None

_backend_dir = Path(__file__).resolve().parent
_quiz_bank = QuizBank.load(_backend_dir / settings.tutor_quiz_csv_path)
_tutor_rag = TutorRAGService(
    index_path=_backend_dir / settings.tutor_rag_index_path,
    api_key=settings.google_api_key,
    embedding_model=settings.tutor_embedding_model,
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


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
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
    try:
        yield
    finally:
        closer = getattr(limiter, "close", None)
        if closer:
            await closer()


app = FastAPI(title="Disputatio AI Backend", lifespan=lifespan)


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------
@app.get("/")
def health_check():
    return {"status": "online", "message": "Server is Running!"}


@app.post("/chat", response_model=ChatResponse)
async def chat(request: Request, payload: ChatRequest):
    """Backward-compatible endpoint: returns full response + function_calls at once."""
    if chat_service is None:
        raise HTTPException(status_code=500, detail="API 키 설정 필요")

    await enforce_chat_rate_limits(request, payload)

    result = await chat_service.chat(payload)

    if not result.response and not result.function_calls:
        raise HTTPException(status_code=500, detail="모든 AI 엔진 실패")

    return result


@app.post("/tutor/grade", response_model=TutorGradeResponse)
async def tutor_grade(request: TutorGradeRequest):
    """LLM 없이 quiz_bank CSV로 정오만 판정합니다."""
    result = grade_tutor_answer(request, _quiz_bank, settings)
    return result


@app.post("/chat/stream")
async def chat_stream(request: Request, payload: ChatRequest):
    """SSE streaming endpoint – tokens arrive in real-time."""
    if chat_service is None:
        raise HTTPException(status_code=500, detail="API 키 설정 필요")

    await enforce_chat_rate_limits(request, payload)

    async def event_generator():
        async for event in chat_service.stream_chat(payload):
            yield f"data: {event.model_dump_json()}\n\n"

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
    uvicorn.run(app, host="0.0.0.0", port=port)
