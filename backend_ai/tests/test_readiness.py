from __future__ import annotations

from services.local_ai.readiness import build_readiness_payload


def test_ready_requires_chat_and_rag() -> None:
    payload = build_readiness_payload(
        protocol_version="1",
        instance_id="inst-1",
        runtime_state="ready",
        chat_ready=True,
        rag_ready=True,
        requested_device="cpu",
        effective_device="cpu",
        model_id="gemma4-e2b",
        error_code=None,
        retryable=False,
    )
    assert payload["state"] == "Ready"
    assert payload["chat_ready"] is True
    assert payload["rag_ready"] is True
    assert payload["protocol_version"] == "1"
    assert payload["instance_id"] == "inst-1"


def test_engine_ready_without_rag_is_not_ready() -> None:
    payload = build_readiness_payload(
        protocol_version="1",
        instance_id="inst-1",
        runtime_state="ready",
        chat_ready=True,
        rag_ready=False,
        requested_device="gpu",
        effective_device="cpu",
        model_id="gemma4-e2b",
        error_code="unsupported_embedding_model",
        retryable=False,
    )
    assert payload["state"] != "Ready"
    assert payload["effective_device"] == "cpu"
    assert payload["requested_device"] == "gpu"
    assert payload["error_code"] == "unsupported_embedding_model"
