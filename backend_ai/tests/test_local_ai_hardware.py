from __future__ import annotations

from subprocess import CompletedProcess

from services.local_ai.hardware import parse_nvidia_smi_csv, probe_nvidia


def test_parse_nvidia_smi_csv_reads_name_vram_and_fingerprint() -> None:
    stdout = "NVIDIA GeForce RTX 4060, 8188, 560.94, GPU-aaaa-bbbb\n"
    info = parse_nvidia_smi_csv(stdout)
    assert info.nvidia_available is True
    assert info.vram_mib == 8188
    assert info.fingerprint == "GPU-aaaa-bbbb"


def test_parse_nvidia_smi_csv_empty_is_cpu_only() -> None:
    info = parse_nvidia_smi_csv("")
    assert info.nvidia_available is False
    assert info.vram_mib is None
    assert info.fingerprint == ""


def test_probe_nvidia_missing_binary_is_cpu_only() -> None:
    def _runner(*_args: object, **_kwargs: object) -> CompletedProcess[str]:
        raise FileNotFoundError("nvidia-smi")

    info = probe_nvidia(runner=_runner)
    assert info.nvidia_available is False
    assert info.vram_mib is None
