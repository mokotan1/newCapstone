from __future__ import annotations

import os
from dataclasses import asdict

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field

from services.local_ai.control_auth import verify_local_ai_control
from services.local_ai.readiness import build_readiness_payload
from services.local_ai.runtime_manager import LocalRuntimeManager
from services.local_ai.telemetry import GpuTelemetry
from services.local_ai.types import GpuOffload, RequestedMode

router = APIRouter()
_telemetry = GpuTelemetry()


class LocalAiSettingsBody(BaseModel):
    model_config = ConfigDict(extra="ignore")

    mode: RequestedMode
    gpu_offload: GpuOffload | None = Field(default=None)


def _manager() -> LocalRuntimeManager:
    import main as main_mod

    manager = getattr(main_mod, "runtime_manager", None)
    if manager is None:
        raise HTTPException(status_code=404, detail="local_ai_disabled")
    return manager


def _token() -> str:
    import main as main_mod

    return (getattr(main_mod.settings, "local_ai_control_token", "") or "").strip()


@router.get("/local-ai/status")
async def local_ai_status(request: Request) -> dict:
    verify_local_ai_control(request, _token())
    manager = _manager()
    await manager.recover_if_engine_died()
    payload = asdict(manager.snapshot())
    payload["gpu"] = await _telemetry.sample()
    import main as main_mod

    rag = getattr(main_mod, "_tutor_rag", None)
    rag_ready = bool(
        getattr(rag, "enabled", False) and getattr(rag, "prepare_error", None) is None
    )
    payload["readiness"] = build_readiness_payload(
        protocol_version="1",
        instance_id=os.environ.get("LOCAL_AI_INSTANCE_ID", ""),
        runtime_state=payload["state"],
        chat_ready=bool(payload["inference_ready"] and payload["model_available"]),
        rag_ready=rag_ready,
        requested_device=payload["requested_mode"],
        effective_device=payload["effective_backend"],
        model_id=str(getattr(main_mod.settings, "local_ai_model", "")),
        error_code=payload.get("fallback_reason"),
        retryable=payload["state"] not in {"failed", "stopped"},
    )
    return payload


@router.put("/local-ai/settings")
async def local_ai_settings(request: Request, body: LocalAiSettingsBody) -> JSONResponse:
    verify_local_ai_control(request, _token())
    operation_id = await _manager().queue_apply(body.mode, body.gpu_offload)
    return JSONResponse(status_code=202, content={"operation_id": operation_id})


@router.post("/local-ai/benchmark")
async def local_ai_benchmark(request: Request) -> dict:
    verify_local_ai_control(request, _token())
    return await _manager().run_benchmark()
