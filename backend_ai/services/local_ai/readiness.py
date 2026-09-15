"""Spec readiness payload. Ready requires chat_ready and rag_ready."""

from __future__ import annotations

_RUNTIME_TO_SPEC = {
    "starting": "Starting",
    "warming": "WarmingUp",
    "draining": "Recovering",
    "failed": "Failed",
    "stopped": "Stopped",
    "stopping": "Stopping",
    "externally_managed": "Checking",
}


def map_spec_state(runtime_state: str, *, chat_ready: bool, rag_ready: bool) -> str:
    if runtime_state == "ready" and chat_ready and rag_ready:
        return "Ready"
    if runtime_state == "ready":
        return "LoadingModel"
    return _RUNTIME_TO_SPEC.get(runtime_state, "Checking")


def build_readiness_payload(
    *,
    protocol_version: str,
    instance_id: str,
    runtime_state: str,
    chat_ready: bool,
    rag_ready: bool,
    requested_device: str,
    effective_device: str,
    model_id: str,
    error_code: str | None,
    retryable: bool,
) -> dict[str, object]:
    return {
        "protocol_version": protocol_version,
        "instance_id": instance_id,
        "state": map_spec_state(
            runtime_state, chat_ready=chat_ready, rag_ready=rag_ready
        ),
        "chat_ready": chat_ready,
        "rag_ready": rag_ready,
        "requested_device": requested_device,
        "effective_device": effective_device,
        "model_id": model_id,
        "error_code": error_code,
        "retryable": retryable,
    }
