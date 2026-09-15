from __future__ import annotations

from dataclasses import dataclass

import httpx

from config import Settings
from providers.litert_provider import LiteRTProvider

_MODELS_PATH = "/v1/models"
_HEALTH_TIMEOUT_SECONDS = 3.0
_ERR_RUNTIME_UNAVAILABLE = "local AI runtime is not reachable on loopback"
_ERR_MODEL_UNAVAILABLE = "local AI model is not installed"


@dataclass(frozen=True)
class LocalRuntimeStatus:
    ollama_or_litert_available: bool
    model_available: bool
    error: str | None


def build_chat_provider(settings: Settings) -> LiteRTProvider:
    """Always use the local LiteRT contract. There is no cloud or fallback provider."""
    return LiteRTProvider(
        base_url=settings.local_ai_base_url,
        model=settings.local_ai_model,
        num_ctx=settings.local_ai_num_ctx,
        think=settings.local_ai_think,
        top_p=settings.dialogue_top_p,
        top_k=settings.dialogue_top_k,
    )


def check_local_runtime(
    settings: Settings,
    *,
    client: httpx.Client | None = None,
) -> LocalRuntimeStatus:
    owns_client = client is None
    http = client or httpx.Client(timeout=_HEALTH_TIMEOUT_SECONDS)
    url = settings.local_ai_base_url.rstrip("/") + _MODELS_PATH
    try:
        response = http.get(url)
        if response.status_code >= 400:
            return LocalRuntimeStatus(False, False, _ERR_RUNTIME_UNAVAILABLE)
        model_ids = _extract_model_ids(response.json())
        if _has_model(model_ids, settings.local_ai_model):
            return LocalRuntimeStatus(True, True, None)
        return LocalRuntimeStatus(True, False, _ERR_MODEL_UNAVAILABLE)
    except (httpx.HTTPError, ValueError, TypeError):
        return LocalRuntimeStatus(False, False, _ERR_RUNTIME_UNAVAILABLE)
    finally:
        if owns_client:
            http.close()


def _extract_model_ids(payload: object) -> list[str]:
    if not isinstance(payload, dict):
        return []
    data = payload.get("data")
    if not isinstance(data, list):
        return []
    ids: list[str] = []
    for item in data:
        if isinstance(item, dict) and isinstance(item.get("id"), str):
            ids.append(item["id"])
    return ids


def _has_model(model_ids: list[str], wanted: str) -> bool:
    for model_id in model_ids:
        if model_id == wanted or model_id.split(":", 1)[0] == wanted:
            return True
    return False
