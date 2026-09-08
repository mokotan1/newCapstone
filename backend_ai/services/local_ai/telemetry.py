"""Device-wide NVIDIA measurements, not AI-only utilization."""
from __future__ import annotations

import asyncio
import subprocess
import time


def read_gpu_usage() -> dict[str, object]:
    unknown: dict[str, object] = {
        "name": None, "utilization_percent": None, "memory_used_mib": None,
        "memory_total_mib": None, "scope": "device",
    }
    try:
        result = subprocess.run(
            ["nvidia-smi", "--id=0", "--query-gpu=name,utilization.gpu,memory.used,memory.total",
             "--format=csv,noheader,nounits"],
            capture_output=True, text=True, timeout=2, check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        if result.returncode:
            return unknown
        name, usage, used, total = [value.strip() for value in result.stdout.strip().split(",")]
        return {"name": name, "utilization_percent": float(usage),
                "memory_used_mib": float(used), "memory_total_mib": float(total), "scope": "device"}
    except (OSError, ValueError, subprocess.TimeoutExpired):
        return unknown


class GpuTelemetry:
    def __init__(self) -> None:
        self._lock = asyncio.Lock()
        self._sampled_at = float("-inf")
        self._value: dict[str, object] = {}

    async def sample(self) -> dict[str, object]:
        async with self._lock:
            if time.monotonic() - self._sampled_at >= 2:
                self._value = await asyncio.to_thread(read_gpu_usage)
                self._sampled_at = time.monotonic()
            return dict(self._value)
