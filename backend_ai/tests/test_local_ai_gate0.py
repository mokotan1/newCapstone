from __future__ import annotations

from pathlib import Path

from services.local_ai.engine import HardwareInfo
from services.local_ai.gate0 import (
    build_litert_gpu_serve_command,
    evaluate_gate0,
    gate0_block_reason,
)
from services.local_ai.gpu_evidence import GpuEvidence


def _gpu_hw(vram_mib: int) -> HardwareInfo:
    return HardwareInfo(
        nvidia_available=True,
        vram_mib=vram_mib,
        fingerprint="gpu-a",
    )


def _gpu_evidence(*, cuda: bool = False) -> GpuEvidence:
    return GpuEvidence(
        gpu_claimed=True,
        cuda_confirmed=cuda,
        adapter_name="NVIDIA GeForce RTX 4060",
    )


def test_gate0_fails_without_nvidia() -> None:
    verdict = evaluate_gate0(
        HardwareInfo(),
        GpuEvidence(gpu_claimed=True, cuda_confirmed=True, adapter_name="x"),
        complete_ms_p50=800.0,
    )
    assert verdict.passed is False
    assert verdict.reason == "nvidia_unavailable"


def test_gate0_fails_without_runtime_gpu_evidence() -> None:
    verdict = evaluate_gate0(
        _gpu_hw(8192),
        GpuEvidence(gpu_claimed=False, cuda_confirmed=False, adapter_name=None),
        complete_ms_p50=800.0,
    )
    assert verdict.passed is False
    assert verdict.reason == "no_gpu_evidence"


def test_gate0_4gb_passes_at_two_seconds() -> None:
    verdict = evaluate_gate0(_gpu_hw(4096), _gpu_evidence(), complete_ms_p50=1999.0)
    assert verdict.passed is True
    assert verdict.vram_class == "min_4gb"
    assert verdict.slo_complete_ms == 2000


def test_gate0_4gb_fails_over_two_seconds() -> None:
    verdict = evaluate_gate0(_gpu_hw(4096), _gpu_evidence(), complete_ms_p50=2001.0)
    assert verdict.passed is False
    assert verdict.reason == "slo_miss"


def test_gate0_6gb_requires_one_second() -> None:
    miss = evaluate_gate0(_gpu_hw(6144), _gpu_evidence(), complete_ms_p50=1500.0)
    hit = evaluate_gate0(_gpu_hw(6144), _gpu_evidence(), complete_ms_p50=999.0)
    assert miss.passed is False
    assert miss.slo_complete_ms == 1000
    assert hit.passed is True
    assert hit.vram_class == "rec_6gb"


def test_gate0_8gb_requires_one_second() -> None:
    verdict = evaluate_gate0(_gpu_hw(8192), _gpu_evidence(cuda=True), complete_ms_p50=1000.0)
    assert verdict.passed is True
    assert verdict.vram_class == "rec_8gb"
    assert verdict.slo_complete_ms == 1000


def test_gate0_8gb_card_reporting_8188_mib_is_rec_8gb() -> None:
    verdict = evaluate_gate0(_gpu_hw(8188), _gpu_evidence(cuda=True), complete_ms_p50=1000.0)
    assert verdict.passed is True
    assert verdict.vram_class == "rec_8gb"
    assert verdict.slo_complete_ms == 1000


def test_gate0_blocks_when_port_already_open() -> None:
    assert gate0_block_reason(nvidia_available=True, port_open=True) == "externally_managed"
    assert gate0_block_reason(nvidia_available=False, port_open=False) == "nvidia_unavailable"
    assert gate0_block_reason(nvidia_available=True, port_open=False) is None


def test_litert_gpu_serve_command_uses_pinned_runtime_and_game_config(
    tmp_path: Path,
) -> None:
    config_path = tmp_path / "runtime-config.json"
    command = build_litert_gpu_serve_command(config_path, host="127.0.0.1", port=9379)
    assert command[:5] == ["uvx", "--from", "litert-lm==0.16.1", "litert-lm", "serve"]
    assert "--config" in command
    assert str(config_path) in command
    joined = " ".join(command)
    assert "http" not in joined
    assert "gguf" not in joined.lower()
    assert "llama.cpp" not in joined.lower()
