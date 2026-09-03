from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from models.responses import SSEEvent
from providers.groq_provider import GroqProvider
from providers.litert_provider import LiteRTProvider


def _make_text_chunk(content: str):
    """Create a mock Groq streaming chunk with text content."""
    delta = MagicMock()
    delta.content = content
    delta.tool_calls = None

    choice = MagicMock()
    choice.delta = delta

    chunk = MagicMock()
    chunk.choices = [choice]
    return chunk


def _make_tool_call_chunk(index: int, name: str | None, arguments: str | None):
    """Create a mock Groq streaming chunk with a tool call fragment."""
    func = MagicMock()
    func.name = name
    func.arguments = arguments

    tc = MagicMock()
    tc.index = index
    tc.function = func

    delta = MagicMock()
    delta.content = None
    delta.tool_calls = [tc]

    choice = MagicMock()
    choice.delta = delta

    chunk = MagicMock()
    chunk.choices = [choice]
    return chunk


class _FakeAsyncIterator:
    def __init__(self, items):
        self._items = iter(items)

    def __aiter__(self):
        return self

    async def __anext__(self):
        try:
            return next(self._items)
        except StopIteration:
            raise StopAsyncIteration


@pytest.mark.asyncio
class TestGroqProviderStreaming:

    async def test_text_only_streaming(self):
        chunks = [_make_text_chunk("안녕"), _make_text_chunk("하세요")]
        mock_client = AsyncMock()
        mock_client.chat.completions.create.return_value = _FakeAsyncIterator(chunks)

        provider = GroqProvider.__new__(GroqProvider)
        provider._client = mock_client
        provider._model = "test-model"

        events: list[SSEEvent] = []
        async for event in provider.stream_chat(messages=[{"role": "user", "content": "hi"}]):
            events.append(event)

        text_events = [e for e in events if e.type == "text_delta"]
        assert len(text_events) == 2
        assert text_events[0].content == "안녕"
        assert text_events[1].content == "하세요"

        done_events = [e for e in events if e.type == "done"]
        assert len(done_events) == 1
        assert done_events[0].full_text == "안녕하세요"

    async def test_tool_call_streaming(self):
        chunks = [
            _make_text_chunk("힌트!"),
            _make_tool_call_chunk(0, "give_hint", '{"hint_level":'),
            _make_tool_call_chunk(0, None, '"moderate","target_object":"bed","hint_category":"location"}'),
        ]
        mock_client = AsyncMock()
        mock_client.chat.completions.create.return_value = _FakeAsyncIterator(chunks)

        provider = GroqProvider.__new__(GroqProvider)
        provider._client = mock_client
        provider._model = "test-model"

        events: list[SSEEvent] = []
        async for event in provider.stream_chat(
            messages=[{"role": "user", "content": "help"}],
            tools=[{"type": "function", "function": {"name": "give_hint"}}],
        ):
            events.append(event)

        fc_events = [e for e in events if e.type == "function_call"]
        assert len(fc_events) == 1
        assert fc_events[0].name == "give_hint"
        assert fc_events[0].arguments["hint_level"] == "moderate"
        assert fc_events[0].arguments["target_object"] == "bed"

    async def test_empty_stream_yields_done(self):
        mock_client = AsyncMock()
        mock_client.chat.completions.create.return_value = _FakeAsyncIterator([])

        provider = GroqProvider.__new__(GroqProvider)
        provider._client = mock_client
        provider._model = "test-model"

        events: list[SSEEvent] = []
        async for event in provider.stream_chat(messages=[{"role": "user", "content": "hi"}]):
            events.append(event)

        assert len(events) == 1
        assert events[0].type == "done"
        assert events[0].full_text == ""

    async def test_malformed_tool_args_fallback_to_empty(self):
        chunks = [
            _make_tool_call_chunk(0, "emote", '{invalid json'),
        ]
        mock_client = AsyncMock()
        mock_client.chat.completions.create.return_value = _FakeAsyncIterator(chunks)

        provider = GroqProvider.__new__(GroqProvider)
        provider._client = mock_client
        provider._model = "test-model"

        events: list[SSEEvent] = []
        async for event in provider.stream_chat(messages=[{"role": "user", "content": "hi"}]):
            events.append(event)

        fc = [e for e in events if e.type == "function_call"]
        assert len(fc) == 1
        assert fc[0].arguments == {}


def test_litert_provider_name_and_defaults() -> None:
    provider = LiteRTProvider(
        base_url="http://127.0.0.1:9379/",
        model="gemma4-e2b",
        num_ctx=4096,
        think=False,
    )
    assert provider.name == "litert"
    assert provider._base_url == "http://127.0.0.1:9379"
    assert provider._model == "gemma4-e2b"
    assert provider._num_ctx == 4096
    assert provider._think is False
