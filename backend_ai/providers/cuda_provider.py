from __future__ import annotations

from typing import Any
from providers.litert_provider import LiteRTProvider


class CudaCompatProvider(LiteRTProvider):
    """llama.cpp request dialect with the existing game SSE contract."""

    @property
    def name(self) -> str:
        return "cuda"

    def adapt_payload(self, payload: dict[str, Any]) -> dict[str, Any]:
        payload.pop("think", None)
        payload.pop("options", None)
        payload["top_k"] = self._top_k
        payload["chat_template_kwargs"] = {"enable_thinking": False}
        return payload
