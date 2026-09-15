from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from services.local_ai.engine import HardwareInfo
from services.local_ai.gpu_evidence import GpuEvidence

MIN_VRAM_MIB = 4096
VRAM_6GB_MIB = 6144
VRAM_8GB_MIB = 7680
SLO_COMPLETE_MS_4GB = 2000
SLO_COMPLETE_MS_6GB = 1000
SLO_COMPLETE_MS_8GB = 1000
PINNED_LITERT_PACKAGE = "litert-lm==0.16.1"


@dataclass(frozen=True)
class Gate0Verdict:
    passed: bool
    reason: str
    vram_class: str | None
    slo_complete_ms: int | None


def _vram_class(vram_mib: int) -> tuple[str, int]:
    if vram_mib >= VRAM_8GB_MIB:
        return "rec_8gb", SLO_COMPLETE_MS_8GB
    if vram_mib >= VRAM_6GB_MIB:
        return "rec_6gb", SLO_COMPLETE_MS_6GB
    return "min_4gb", SLO_COMPLETE_MS_4GB


def evaluate_gate0(
    hardware: HardwareInfo,
    evidence: GpuEvidence,
    complete_ms_p50: float | None,
) -> Gate0Verdict:
    if not hardware.nvidia_available:
        return Gate0Verdict(False, "nvidia_unavailable", None, None)
    if hardware.vram_mib is None or hardware.vram_mib < MIN_VRAM_MIB:
        return Gate0Verdict(False, "insufficient_vram", None, None)
    vram_class, slo_complete_ms = _vram_class(hardware.vram_mib)
    if not evidence.gpu_claimed:
        return Gate0Verdict(False, "no_gpu_evidence", vram_class, slo_complete_ms)
    if complete_ms_p50 is None:
        return Gate0Verdict(False, "no_measurement", vram_class, slo_complete_ms)
    if complete_ms_p50 > slo_complete_ms:
        return Gate0Verdict(False, "slo_miss", vram_class, slo_complete_ms)
    return Gate0Verdict(True, "pass", vram_class, slo_complete_ms)


def gate0_block_reason(*, nvidia_available: bool, port_open: bool) -> str | None:
    if not nvidia_available:
        return "nvidia_unavailable"
    if port_open:
        return "externally_managed"
    return None


def build_litert_gpu_serve_command(
    config_path: Path,
    *,
    host: str = "127.0.0.1",
    port: int = 9379,
) -> list[str]:
    return [
        "uvx",
        "--from",
        PINNED_LITERT_PACKAGE,
        "litert-lm",
        "serve",
        "--host",
        host,
        "--port",
        str(port),
        "--config",
        str(config_path),
        "--verbose",
    ]
