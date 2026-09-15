from __future__ import annotations

from dataclasses import asdict

from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, ConfigDict, Field

from services.local_ai.control_auth import verify_local_ai_control
from services.local_ai.runtime_manager import LocalRuntimeManager
from services.local_ai.types import GpuOffload, RequestedMode
from services.local_ai.telemetry import GpuTelemetry

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
