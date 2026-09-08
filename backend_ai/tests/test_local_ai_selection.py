from __future__ import annotations

from services.local_ai.selection import select_engine_kind


def test_cpu_mode_never_selects_gpu() -> None:
    assert (
        select_engine_kind(
            "cpu",
            nvidia_available=True,
            vram_mib=8192,
            failure_fingerprint=None,
            current_fingerprint="gpu-a",
            gate0_passed=True,
            cuda_manifest_pinned=True,
        )
        == "litert_cpu"
    )


def test_auto_without_nvidia_stays_cpu() -> None:
    assert (
        select_engine_kind(
            "auto",
            nvidia_available=False,
            vram_mib=None,
            failure_fingerprint=None,
            current_fingerprint="",
            gate0_passed=False,
            cuda_manifest_pinned=False,
        )
        == "litert_cpu"
    )


def test_auto_remembers_gpu_failure_fingerprint() -> None:
    assert (
        select_engine_kind(
            "auto",
            nvidia_available=True,
            vram_mib=8192,
            failure_fingerprint="gpu-a",
            current_fingerprint="gpu-a",
            gate0_passed=True,
            cuda_manifest_pinned=False,
        )
        == "litert_cpu"
    )


def test_gpu_mode_retries_even_after_fingerprint() -> None:
    assert (
        select_engine_kind(
            "gpu",
            nvidia_available=True,
            vram_mib=8192,
            failure_fingerprint="gpu-a",
            current_fingerprint="gpu-a",
            gate0_passed=True,
            cuda_manifest_pinned=False,
        )
        == "litert_gpu"
    )


def test_gpu_without_gate0_or_cuda_manifest_falls_back_to_cpu() -> None:
    assert (
        select_engine_kind(
            "gpu",
            nvidia_available=True,
            vram_mib=8192,
            failure_fingerprint=None,
            current_fingerprint="gpu-a",
            gate0_passed=False,
            cuda_manifest_pinned=False,
        )
        == "litert_cpu"
    )


def test_cuda_only_after_manifest_pin_when_gate0_failed() -> None:
    assert (
        select_engine_kind(
            "gpu",
            nvidia_available=True,
            vram_mib=4096,
            failure_fingerprint=None,
            current_fingerprint="gpu-a",
            gate0_passed=False,
            cuda_manifest_pinned=True,
        )
        == "cuda"
    )
