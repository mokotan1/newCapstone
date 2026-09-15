from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from services.local_ai.engine import HardwareInfo

MIN_VRAM_MIB = 4096
VRAM_6GB_MIB = 6144
VRAM_8GB_MIB = 7680
VERIFY_COMPLETE_MS_4GB = 2500
VERIFY_COMPLETE_MS_6GB = 1500
PRODUCT_COMPLETE_MS_4GB = 2000
PRODUCT_COMPLETE_MS_6GB = 1000
CUDA_MODEL_FILE = "gemma-4-E2B-it-Q4_0.gguf"
CUDA_SERVER_FILE = "llama-server.exe"


@dataclass(frozen=True)
class Gate1Verdict:
    passed: bool
    reason: str
    vram_class: str | None
    slo_complete_ms: int | None
    meets_product_slo: bool


def _vram_class(vram_mib: int) -> tuple[str, int, int]:
    if vram_mib >= VRAM_8GB_MIB:
        return "rec_8gb", VERIFY_COMPLETE_MS_6GB, PRODUCT_COMPLETE_MS_6GB
    if vram_mib >= VRAM_6GB_MIB:
        return "rec_6gb", VERIFY_COMPLETE_MS_6GB, PRODUCT_COMPLETE_MS_6GB
    return "min_4gb", VERIFY_COMPLETE_MS_4GB, PRODUCT_COMPLETE_MS_4GB


def evaluate_gate1(
    hardware: HardwareInfo,
    *,
    cuda_offload: bool,
    quality_ok: bool,
    had_error: bool,
    complete_ms_p50: float | None,
) -> Gate1Verdict:
    if not hardware.nvidia_available:
        return Gate1Verdict(False, "nvidia_unavailable", None, None, False)
    if hardware.vram_mib is None or hardware.vram_mib < MIN_VRAM_MIB:
        return Gate1Verdict(False, "insufficient_vram", None, None, False)
    vram_class, verify_ms, product_ms = _vram_class(hardware.vram_mib)
    if not cuda_offload:
        return Gate1Verdict(False, "no_cuda_offload", vram_class, verify_ms, False)
    if not quality_ok:
        return Gate1Verdict(False, "quality_gate", vram_class, verify_ms, False)
    if had_error:
        return Gate1Verdict(False, "inference_error", vram_class, verify_ms, False)
    if complete_ms_p50 is None:
        return Gate1Verdict(False, "no_measurement", vram_class, verify_ms, False)
    if complete_ms_p50 > verify_ms:
        return Gate1Verdict(False, "slo_miss", vram_class, verify_ms, False)
    return Gate1Verdict(
        True,
        "pass",
        vram_class,
        verify_ms,
        complete_ms_p50 <= product_ms,
    )


def build_cuda_serve_command(
    root: Path,
    *,
    port: int,
    model_alias: str,
    num_ctx: int,
    n_gpu_layers: int = 99,
) -> list[str]:
    return [
        str(root / CUDA_SERVER_FILE),
        "--host",
        "127.0.0.1",
        "--port",
        str(port),
        "--model",
        str(root / CUDA_MODEL_FILE),
        "--alias",
        model_alias,
        "--ctx-size",
        str(num_ctx),
        "--parallel",
        "1",
        "--n-gpu-layers",
        str(n_gpu_layers),
        "--device",
        "CUDA0",
        "--no-webui",
        "--reasoning",
        "off",
        "--verbosity",
        "4",
    ]
