from __future__ import annotations

import json
import logging
from collections.abc import AsyncIterator
from typing import Any

import httpx
from models.responses import SSEEvent

from providers.base import AIProvider

logger = logging.getLogger(__name__)

_CHAT_COMPLETIONS_PATH = "/v1/chat/completions"
_THINKING_DELTA_KEYS = frozenset({"thinking", "reasoning", "reasoning_content", "reasoning_text"})
_DEFAULT_TIMEOUT_SECONDS = 120.0
_PLAYER_VISIBLE_RUNTIME_ERROR = "로컬 AI 런타임에 연결할 수 없습니다."


class LiteRTProvider(AIProvider):
    """OpenAI-compatible local Gemma 4 E2B runtime (LiteRT-LM / compatible)."""

    def __init__(
        self,
        base_url: str,
        model: str,
        num_ctx: int,
        think: bool = False,
        *,
        client: httpx.AsyncClient | None = None,
        top_p: float = 0.95,
        top_k: int = 64,
    ) -> None:
        self._base_url = base_url.rstrip("/")
        self._model = model
        self._num_ctx = num_ctx
        self._think = think
        self._top_p = top_p
        self._top_k = top_k
        self._owns_client = client is None
        self._client = client or httpx.AsyncClient(
            base_url=self._base_url,
            timeout=httpx.Timeout(_DEFAULT_TIMEOUT_SECONDS),
        )

    @property
    def name(self) -> str:
        return "litert"

    async def aclose(self) -> None:
        if self._owns_client:
            await self._client.aclose()

    async def stream_chat(
        self,
        messages: list[dict],
        tools: list[dict] | None = None,
        temperature: float = 0.8,
        max_tokens: int = 120,
    ) -> AsyncIterator[SSEEvent]:
        payload: dict[str, Any] = {
            "model": self._model,
            "messages": messages,
            "temperature": temperature,
            "top_p": self._top_p,
            "max_tokens": max_tokens,
            "stream": True,
            "think": self._think,
            "options": {"num_ctx": self._num_ctx, "top_k": self._top_k},
        }
        if tools:
            payload["tools"] = tools
            payload["tool_choice"] = "auto"

        try:
            async with self._client.stream(
                "POST",
                _CHAT_COMPLETIONS_PATH,
                json=payload,
            ) as response:
                if response.status_code >= 400:
                    yield SSEEvent(type="error", content=_PLAYER_VISIBLE_RUNTIME_ERROR)
                    yield SSEEvent(type="done", full_text="")
                    return

                async for event in self._iter_sse_events(response):
                    yield event
        except httpx.HTTPError:
            logger.exception("LiteRT runtime request failed")
            yield SSEEvent(type="error", content=_PLAYER_VISIBLE_RUNTIME_ERROR)
            yield SSEEvent(type="done", full_text="")

    async def _iter_sse_events(self, response: httpx.Response) -> AsyncIterator[SSEEvent]:
        buffer = ""
        full_text_parts: list[str] = []
        tool_calls_acc: dict[int, dict[str, str]] = {}

        async for raw in response.aiter_text():
            buffer += raw
            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                parsed = self._parse_stream_line(line)
                if parsed is None:
                    continue
                if parsed is _STREAM_DONE:
                    for fc_event in self._emit_tool_call_events(tool_calls_acc):
                        yield fc_event
                    yield SSEEvent(type="done", full_text="".join(full_text_parts))
                    return
                for event in self._events_from_payload(parsed, full_text_parts, tool_calls_acc):
                    yield event

        for fc_event in self._emit_tool_call_events(tool_calls_acc):
            yield fc_event
        yield SSEEvent(type="done", full_text="".join(full_text_parts))

    @staticmethod
    def _parse_stream_line(line: str) -> dict[str, Any] | object | None:
        payload = line.strip()
        if not payload:
            return None
        if payload.startswith("data:"):
            payload = payload[5:].strip()
        if not payload or payload == "[DONE]":
            return _STREAM_DONE if payload == "[DONE]" else None
        try:
            parsed = json.loads(payload)
        except json.JSONDecodeError:
            return None
        if not isinstance(parsed, dict):
            return None
        return parsed

    def _events_from_payload(
        self,
        payload: dict[str, Any],
        full_text_parts: list[str],
        tool_calls_acc: dict[int, dict[str, str]],
    ) -> list[SSEEvent]:
        events: list[SSEEvent] = []
        if payload.get("error"):
            events.append(SSEEvent(type="error", content=_PLAYER_VISIBLE_RUNTIME_ERROR))
            return events

        choices = payload.get("choices")
        if not isinstance(choices, list) or not choices:
            return events
        first = choices[0]
        if not isinstance(first, dict):
            return events
        delta = first.get("delta") or first.get("message") or {}
        if not isinstance(delta, dict):
            return events

        for thinking_key in _THINKING_DELTA_KEYS:
            if thinking_key in delta:
                logger.debug("Suppressed LiteRT thinking field: %s", thinking_key)

        content = delta.get("content")
        if isinstance(content, str) and content:
            full_text_parts.append(content)
            events.append(SSEEvent(type="text_delta", content=content))

        tool_calls = delta.get("tool_calls")
        if isinstance(tool_calls, list):
            self._accumulate_tool_calls(tool_calls, tool_calls_acc)

        return events

    @staticmethod
    def _accumulate_tool_calls(
        tool_calls: list[Any],
        acc: dict[int, dict[str, str]],
    ) -> None:
        for index, item in enumerate(tool_calls):
            if not isinstance(item, dict):
                continue
            idx = item.get("index", index)
            if not isinstance(idx, int):
                continue
            if idx not in acc:
                acc[idx] = {"name": "", "arguments": ""}
            function = item.get("function") or {}
            if isinstance(function, dict):
                name = function.get("name")
                arguments = function.get("arguments")
                if isinstance(name, str):
                    acc[idx]["name"] += name
                if isinstance(arguments, str):
                    acc[idx]["arguments"] += arguments

    @staticmethod
    def _emit_tool_call_events(acc: dict[int, dict[str, str]]) -> list[SSEEvent]:
        events: list[SSEEvent] = []
        for item in acc.values():
            raw_args = item["arguments"].strip() or "{}"
            try:
                parsed_args = json.loads(raw_args)
            except json.JSONDecodeError:
                parsed_args = {}
            if not isinstance(parsed_args, dict):
                parsed_args = {}
            events.append(
                SSEEvent(
                    type="function_call",
                    name=item["name"],
                    arguments=parsed_args,
                )
            )
        return events


_STREAM_DONE = object()
