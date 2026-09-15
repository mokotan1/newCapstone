from __future__ import annotations

import subprocess
from collections.abc import Callable
from subprocess import CompletedProcess

from services.local_ai.engine import HardwareInfo

NvidiaRunner = Callable[..., CompletedProcess[str]]

_QUERY = [
    "nvidia-smi",
    "--query-gpu=name,memory.total,driver_version,uuid",
    "--format=csv,noheader,nounits",
]


def parse_nvidia_smi_csv(stdout: str) -> HardwareInfo:
    lines = [line.strip() for line in stdout.splitlines() if line.strip()]
    if not lines:
        return HardwareInfo()
    parts = [part.strip() for part in lines[0].split(",")]
    if len(parts) < 4:
        return HardwareInfo()
    try:
        vram_mib = int(float(parts[1]))
    except ValueError:
        return HardwareInfo()
    fingerprint = parts[3]
    if not fingerprint:
        return HardwareInfo()
    return HardwareInfo(
        nvidia_available=True,
        vram_mib=vram_mib,
        fingerprint=fingerprint,
    )


def probe_nvidia(*, runner: NvidiaRunner | None = None) -> HardwareInfo:
    run = runner or subprocess.run
    try:
        completed = run(
            _QUERY,
            capture_output=True,
            text=True,
            timeout=5,
            check=False,
        )
    except (FileNotFoundError, OSError, subprocess.TimeoutExpired):
        return HardwareInfo()
    if completed.returncode != 0:
        return HardwareInfo()
    return parse_nvidia_smi_csv(completed.stdout or "")
