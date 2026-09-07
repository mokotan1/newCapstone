from __future__ import annotations

from unittest.mock import MagicMock

import httpx
import pytest

from config import Settings
from local_runtime import (
    LocalRuntimeStatus,
    build_chat_providers,
    check_local_runtime,
)
from providers.gemini_provider import GeminiProvider
from providers.groq_provider import GroqProvider
from providers.litert_provider import LiteRTProvider


def _settings(**overrides: object) -> Settings:
    values = {
        "ai_provider": "local",
        "local_ai_base_url": "http://127.0.0.1:9379",
        "local_ai_model": "gemma4-e2b",
        "groq_api_key": "",
        "google_api_key": "",
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


def test_build_chat_providers_local_without_cloud_keys() -> None:
    primary, fallback = build_chat_providers(_settings())
    assert isinstance(primary, LiteRTProvider)
    assert fallback is None
    assert primary._top_p == pytest.approx(0.95)
    assert primary._top_k == 64


def test_build_chat_providers_local_keeps_cloud_fallback() -> None:
    primary, fallback = build_chat_providers(
        _settings(google_api_key="dev-gemini"),
    )
    assert isinstance(primary, LiteRTProvider)
    assert isinstance(fallback, GeminiProvider)


def test_build_chat_providers_cloud_requires_keys() -> None:
    primary, fallback = build_chat_providers(_settings(ai_provider="cloud"))
    assert primary is None
    assert fallback is None


def test_build_chat_providers_cloud_uses_groq() -> None:
    primary, fallback = build_chat_providers(
        _settings(ai_provider="cloud", groq_api_key="g"),
    )
    assert isinstance(primary, GroqProvider)
    assert fallback is None


def test_start_local_runtime_skips_when_already_ready(monkeypatch: pytest.MonkeyPatch) -> None:
    from local_runtime import start_local_runtime

    monkeypatch.setattr(
        "local_runtime.check_local_runtime",
        lambda settings, client=None: LocalRuntimeStatus(True, True, None),
    )
    popen = MagicMock()
    monkeypatch.setattr("local_runtime.subprocess.Popen", popen)

    handle = start_local_runtime(_settings())
    assert handle is None
    popen.assert_not_called()
