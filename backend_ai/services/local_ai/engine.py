from __future__ import annotations

from dataclasses import dataclass

from providers.base import AIProvider
from services.local_ai.types import EffectiveBackend, EngineKind


@dataclass
class OwnedEngine:
    kind: EngineKind
    pid: int
    port: int
    provider: AIProvider
    started_by_manager: bool


@dataclass(frozen=True)
class WarmupResult:
    ok: bool
    effective_backend: EffectiveBackend
    error: str | None = None
    ttft_ms: float | None = None
    total_ms: float | None = None


@dataclass(frozen=True)
class HardwareInfo:
    nvidia_available: bool = False
    vram_mib: int | None = None
    fingerprint: str = ""
