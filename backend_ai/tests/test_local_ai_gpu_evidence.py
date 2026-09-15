from __future__ import annotations

from services.local_ai.gpu_evidence import parse_runtime_log


def test_config_backend_gpu_alone_is_not_evidence() -> None:
    log = (
        '{"models":{"gemma4-e2b":{"backend":"gpu"}}}\n'
        "Using backend from config for model 'gemma4-e2b': gpu\n"
        "WebGPU sampler not available, falling back to statically linked C API\n"
    )
    evidence = parse_runtime_log(log)
    assert evidence.gpu_claimed is False
    assert evidence.cuda_confirmed is False


def test_empty_log_is_not_evidence() -> None:
    evidence = parse_runtime_log("")
    assert evidence.gpu_claimed is False
    assert evidence.cuda_confirmed is False


def test_nvidia_gpu_delegate_is_evidence() -> None:
    log = "Created GPU delegate on adapter NVIDIA GeForce RTX 3060\n"
    evidence = parse_runtime_log(log)
    assert evidence.gpu_claimed is True
    assert evidence.adapter_name == "NVIDIA GeForce RTX 3060"
    assert evidence.cuda_confirmed is False


def test_cuda_device_log_confirms_cuda() -> None:
    log = "Using CUDA device 0 NVIDIA GeForce RTX 4060\n"
    evidence = parse_runtime_log(log)
    assert evidence.gpu_claimed is True
    assert evidence.cuda_confirmed is True


def test_webgpu_nvidia_adapter_and_decode_offload_is_evidence() -> None:
    log = (
        "Selected adapter: NVIDIA GeForce RTX 4060 Ti, arch=lovelace, "
        "vendor=nvidia, backend=Direct3D 12, adapterType=Discrete GPU\n"
        "Replacing 2068 out of 2068 node(s) with delegate (LITERT_WEBGPU) node, "
        "yielding 1 partitions for subgraph 0 (decode).\n"
        "Failed to create OpenCL context.\n"
    )
    evidence = parse_runtime_log(log)
    assert evidence.gpu_claimed is True
    assert evidence.cuda_confirmed is False
    assert evidence.adapter_name == "NVIDIA GeForce RTX 4060 Ti"


def test_opencl_nvidia_is_gpu_but_not_cuda() -> None:
    log = "OpenCL device NVIDIA GeForce RTX 3060 selected for GPU backend\n"
    evidence = parse_runtime_log(log)
    assert evidence.gpu_claimed is True
    assert evidence.cuda_confirmed is False
