from __future__ import annotations

from services.local_ai.types import EngineKind, RequestedMode


def select_engine_kind(
    mode: RequestedMode,
    *,
    nvidia_available: bool,
    vram_mib: int | None,
    failure_fingerprint: str | None,
    current_fingerprint: str,
    gate0_passed: bool,
    cuda_manifest_pinned: bool,
) -> EngineKind:
    """Pick which engine to attempt. auto is availability, not a speed benchmark."""
    if mode == "cpu":
        return "litert_cpu"

    gpu_ok = nvidia_available and (vram_mib is None or vram_mib >= 4096)
    if mode == "auto" and not gpu_ok:
        return "litert_cpu"
    if (
        mode == "auto"
        and failure_fingerprint
        and failure_fingerprint == current_fingerprint
    ):
        return "litert_cpu"

    if gate0_passed:
        return "litert_gpu"
    if cuda_manifest_pinned:
        return "cuda"
    return "litert_cpu"
