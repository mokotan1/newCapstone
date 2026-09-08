from __future__ import annotations

from pathlib import Path

from services.local_ai.engine import HardwareInfo
from services.local_ai.gate1 import (
    build_cuda_serve_command,
    evaluate_gate1,
)
from services.local_ai.process_host import cuda_offload_confirmed


def _gpu_hw(vram_mib: int) -> HardwareInfo:
    return HardwareInfo(nvidia_available=True, vram_mib=vram_mib, fingerprint="gpu-a")


def test_gate1_fails_without_nvidia() -> None:
    verdict = evaluate_gate1(
        HardwareInfo(),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=800.0,
    )
    assert verdict.passed is False
    assert verdict.reason == "nvidia_unavailable"


def test_gate1_fails_without_cuda_offload_evidence() -> None:
    verdict = evaluate_gate1(
        _gpu_hw(8192),
        cuda_offload=False,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=800.0,
    )
    assert verdict.passed is False
    assert verdict.reason == "no_cuda_offload"


def test_gate1_fails_when_quality_or_errors_fail() -> None:
    quality = evaluate_gate1(
        _gpu_hw(8192),
        cuda_offload=True,
        quality_ok=False,
        had_error=False,
        complete_ms_p50=800.0,
    )
    errors = evaluate_gate1(
        _gpu_hw(8192),
        cuda_offload=True,
        quality_ok=True,
        had_error=True,
        complete_ms_p50=800.0,
    )
    assert quality.passed is False
    assert quality.reason == "quality_gate"
    assert errors.passed is False
    assert errors.reason == "inference_error"


def test_gate1_4gb_verify_slo_is_2500ms() -> None:
    hit = evaluate_gate1(
        _gpu_hw(4096),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=2499.0,
    )
    miss = evaluate_gate1(
        _gpu_hw(4096),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=2501.0,
    )
    assert hit.passed is True
    assert hit.slo_complete_ms == 2500
    assert hit.meets_product_slo is False
    assert miss.passed is False
    assert miss.reason == "slo_miss"


def test_gate1_8gb_verify_slo_is_1500ms() -> None:
    hit = evaluate_gate1(
        _gpu_hw(8188),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=1500.0,
    )
    miss = evaluate_gate1(
        _gpu_hw(8188),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=1501.0,
    )
    product = evaluate_gate1(
        _gpu_hw(8188),
        cuda_offload=True,
        quality_ok=True,
        had_error=False,
        complete_ms_p50=999.0,
    )
    assert hit.passed is True
    assert hit.vram_class == "rec_8gb"
    assert hit.slo_complete_ms == 1500
    assert hit.meets_product_slo is False
    assert miss.passed is False
    assert product.passed is True
    assert product.meets_product_slo is True


def test_cuda_serve_command_uses_trusted_args_only(tmp_path: Path) -> None:
    root = tmp_path / "cuda"
    command = build_cuda_serve_command(
        root, port=9380, model_alias="gemma4-e2b", num_ctx=2048, n_gpu_layers=99,
    )
    assert command[0] == str(root / "llama-server.exe")
    assert command[command.index("--model") + 1] == str(root / "gemma-4-E2B-it-Q4_0.gguf")
    assert command[command.index("--n-gpu-layers") + 1] == "99"
    assert command[command.index("--device") + 1] == "CUDA0"
    assert "--host" in command and "127.0.0.1" in command
    joined = " ".join(command)
    assert "cmd.exe" not in joined
    assert "&" not in joined


def test_cuda_offload_confirmed_requires_layers_and_model_buffer() -> None:
    log = (
        "ggml_cuda_init: found 1 CUDA devices:\n"
        "  Device 0: NVIDIA GeForce RTX 4060 Ti, compute capability 8.9, VMM: yes\n"
        "load_tensors: offloaded 35/35 layers to GPU\n"
        "CUDA0 model buffer size =  2147.32 MiB\n"
    )
    assert cuda_offload_confirmed(log) is True
    assert cuda_offload_confirmed("ggml_cuda_init: found 1 CUDA devices:\nCUDA0\n") is False
    assert cuda_offload_confirmed("offloaded 0/35 layers to GPU\nCUDA0 model buffer size = 1") is False
