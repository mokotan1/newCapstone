from __future__ import annotations

import importlib

import pytest


@pytest.fixture
def fresh_settings(monkeypatch):
    import config as cfg

    cfg.get_settings.cache_clear()
    yield cfg
    cfg.get_settings.cache_clear()


def test_get_settings_prefers_pydantic_env_over_legacy(monkeypatch, fresh_settings):
    monkeypatch.setenv("GROQ_API_KEY", "from-groq-env")
    monkeypatch.delenv("capstone", raising=False)
    cfg = importlib.reload(fresh_settings)
    assert cfg.get_settings().groq_api_key == "from-groq-env"


def test_get_settings_legacy_capstone_when_groq_empty(monkeypatch, fresh_settings):
    monkeypatch.delenv("GROQ_API_KEY", raising=False)
    monkeypatch.setenv("capstone", "legacy-key")
    cfg = importlib.reload(fresh_settings)
    assert cfg.get_settings().groq_api_key == "legacy-key"


def test_get_settings_loads_chat_api_token(monkeypatch, fresh_settings):
    monkeypatch.setenv("CHAT_API_TOKEN", "server-token")
    cfg = importlib.reload(fresh_settings)
    assert cfg.get_settings().chat_api_token == "server-token"
