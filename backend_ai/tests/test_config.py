from __future__ import annotations

import importlib

import pytest

from config import Settings


@pytest.fixture
def fresh_settings(monkeypatch):
    import config as cfg

    cfg.get_settings.cache_clear()
    yield cfg
    cfg.get_settings.cache_clear()


def test_get_settings_loads_chat_api_token(monkeypatch, fresh_settings):
    monkeypatch.setenv("CHAT_API_TOKEN", "server-token")
    cfg = importlib.reload(fresh_settings)
    assert cfg.get_settings().chat_api_token == "server-token"


def test_settings_defaults_to_local_provider() -> None:
    assert Settings.model_fields["ai_provider"].default == "local"
    assert Settings.model_fields["tutor_embedding_model"].default == "local-hash-v1"
