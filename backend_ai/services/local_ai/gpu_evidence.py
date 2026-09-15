from __future__ import annotations

import re
from dataclasses import dataclass

_ADAPTER = re.compile(
    r"(?:adapter:?|device(?:\s+\d+)?)\s+(NVIDIA[^\n,]+)",
    re.IGNORECASE,
)
_CUDA = re.compile(r"\b(?:using cuda|cuda device)\b", re.IGNORECASE)
_GPU_DELEGATE = re.compile(r"\bgpu delegate\b", re.IGNORECASE)
_OPENCL_GPU = re.compile(r"\bopencl device\b.+\bgpu\b", re.IGNORECASE)
_WEBGPU_DECODE = re.compile(
    r"delegate\s*\(\s*LITERT_WEBGPU\s*\).+\(decode\)",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class GpuEvidence:
    gpu_claimed: bool
    cuda_confirmed: bool
    adapter_name: str | None = None


def parse_runtime_log(text: str) -> GpuEvidence:
    cuda_confirmed = _CUDA.search(text) is not None
    gpu_delegate = _GPU_DELEGATE.search(text) is not None
    opencl_gpu = _OPENCL_GPU.search(text) is not None
    webgpu_decode = _WEBGPU_DECODE.search(text) is not None
    adapter_match = _ADAPTER.search(text)
    adapter_name = adapter_match.group(1).strip() if adapter_match else None
    gpu_claimed = cuda_confirmed or gpu_delegate or opencl_gpu or webgpu_decode
    return GpuEvidence(
        gpu_claimed=gpu_claimed,
        cuda_confirmed=cuda_confirmed,
        adapter_name=adapter_name,
    )
