from __future__ import annotations

import httpx
import pytest

from config import Settings
from local_runtime import (
    LocalRuntimeStatus,
    build_chat_provider,
    check_local_runtime,
)
from providers.litert_provider import LiteRTProvider


def _settings(**overrides: object) -> Settings:
    values = {
        "ai_provider": "local",
        "local_ai_base_url": "http://127.0.0.1:9379",
        "local_ai_model": "gemma4-e2b",
    }
    values.update(overrides)
    return Settings(**values)


def test_check_local_runtime_unavailable(monkeypatch: pytest.MonkeyPatch) -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ConnectError("connection refused", request=request)

    client = httpx.Client(transport=httpx.MockTransport(handler))
    status = check_local_runtime(_settings(), client=client)

    assert status.ollama_or_litert_available is False
    assert status.model_available is False
    assert status.error is not None


def test_check_local_runtime_model_unavailable() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"data": [{"id": "other-model"}]})

    client = httpx.Client(transport=httpx.MockTransport(handler))
    status = check_local_runtime(_settings(), client=client)

    assert status.ollama_or_litert_available is True
    assert status.model_available is False
    assert status.error is not None


def test_check_local_runtime_ready() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(200, json={"data": [{"id": "gemma4-e2b"}]})

    client = httpx.Client(transport=httpx.MockTransport(handler))
    status = check_local_runtime(_settings(), client=client)

    assert status == LocalRuntimeStatus(
        ollama_or_litert_available=True,
        model_available=True,
        error=None,
    )


def test_dialogue_latency_budget_field_defaults() -> None:
    assert Settings.model_fields["dialogue_max_tokens"].default == 64
    assert Settings.model_fields["local_ai_num_ctx"].default == 2048


def test_build_chat_provider_uses_local_litert_sampling() -> None:
    provider = build_chat_provider(_settings())
    assert isinstance(provider, LiteRTProvider)
    assert provider._top_p == pytest.approx(0.95)
    assert provider._top_k == 64


def test_build_chat_provider_ignores_unknown_provider_mode() -> None:
    # Legacy .env values such as AI_PROVIDER=cloud must still resolve to the local engine.
    provider = build_chat_provider(_settings(ai_provider="cloud"))
    assert isinstance(provider, LiteRTProvider)
    assert provider.name not in ("groq", "gemini")
