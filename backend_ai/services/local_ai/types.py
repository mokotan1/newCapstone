from __future__ import annotations

from typing import Literal

RequestedMode = Literal["auto", "cpu", "gpu"]
GpuOffload = Literal["low", "medium", "high"]
ConfiguredBackend = Literal["cpu", "gpu"]
EffectiveBackend = Literal["cpu", "gpu", "unknown"]
RuntimeState = Literal[
    "stopped",
    "ready",
    "draining",
    "starting",
    "warming",
    "failed",
    "externally_managed",
]
EngineKind = Literal["litert_cpu", "litert_gpu", "cuda"]

VALID_MODES: frozenset[str] = frozenset({"auto", "cpu", "gpu"})
VALID_OFFLOADS: frozenset[str] = frozenset({"low", "medium", "high"})
