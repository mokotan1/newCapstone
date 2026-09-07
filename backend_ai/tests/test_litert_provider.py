from __future__ import annotations

import json

import httpx
import pytest
from models.responses import SSEEvent
from providers.litert_provider import LiteRTProvider


def _sse_line(payload: str) -> bytes:
    return f"data: {payload}\n\n".encode()


def _provider_with_stream(chunks: list[bytes], status_code: int = 200) -> LiteRTProvider:
    body = b"".join(chunks)

    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            status_code,
            headers={"content-type": "text/event-stream"},
            content=body,
        )

    client = httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="http://127.0.0.1:9379",
    )
    return LiteRTProvider(
        base_url="http://127.0.0.1:9379",
        model="gemma4-e2b",
        num_ctx=4096,
        think=False,
        client=client,
    )


class _ChunkedByteStream(httpx.AsyncByteStream):
    """Yield raw HTTP body pieces so a JSON line can be split across reads."""

    def __init__(self, chunks: list[bytes]) -> None:
        self._chunks = chunks

    async def __aiter__(self):
        for chunk in self._chunks:
            yield chunk


def _provider_with_chunked_stream(chunks: list[bytes]) -> LiteRTProvider:
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            200,
            headers={"content-type": "text/event-stream"},
            stream=_ChunkedByteStream(chunks),
        )

    client = httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="http://127.0.0.1:9379",
    )
    return LiteRTProvider(
        base_url="http://127.0.0.1:9379",
        model="gemma4-e2b",
        num_ctx=4096,
        think=False,
        client=client,
    )


async def _collect(provider: LiteRTProvider) -> list[SSEEvent]:
    return [
        event
        async for event in provider.stream_chat(
            messages=[{"role": "user", "content": "안녕"}],
            temperature=0.8,
            max_tokens=120,
        )
    ]


@pytest.mark.asyncio
async def test_text_chunks_emit_deltas_and_done() -> None:
    provider = _provider_with_stream(
        [
            _sse_line('{"choices":[{"delta":{"content":"켁"}}]}'),
            _sse_line('{"choices":[{"delta":{"content":"켁!"}}]}'),
            b"data: [DONE]\n\n",
        ]
    )

    events = await _collect(provider)
    deltas = [e.content for e in events if e.type == "text_delta"]
    done = [e for e in events if e.type == "done"]

    assert deltas == ["켁", "켁!"]
    assert len(done) == 1
    assert done[0].full_text == "켁켁!"


@pytest.mark.asyncio
async def test_partial_ndjson_line_is_buffered() -> None:
    first = b'data: {"choices":[{"delta":{"content":"H'
    second = b'i"}}]}\n\ndata: [DONE]\n\n'
    provider = _provider_with_chunked_stream([first, second])

    events = await _collect(provider)
    deltas = [e.content for e in events if e.type == "text_delta"]
    done = [e for e in events if e.type == "done"]

    assert deltas == ["Hi"]
    assert done[0].full_text == "Hi"


@pytest.mark.asyncio
async def test_malformed_chunk_is_skipped_without_abort() -> None:
    provider = _provider_with_stream(
        [
            _sse_line("not-json"),
            _sse_line('{"choices":[{"delta":{"content":"정상"}}]}'),
            b"data: [DONE]\n\n",
        ]
    )

    events = await _collect(provider)
    assert [e.content for e in events if e.type == "text_delta"] == ["정상"]
    assert events[-1].type == "done"
    assert events[-1].full_text == "정상"
    assert not any(e.type == "error" for e in events)


@pytest.mark.asyncio
async def test_mid_stream_http_error_yields_error_and_done() -> None:
    provider = _provider_with_stream([b"runtime unavailable"], status_code=503)

    events = await _collect(provider)
    assert any(e.type == "error" for e in events)
    assert events[-1].type == "done"
    assert events[-1].full_text == ""


@pytest.mark.asyncio
async def test_thinking_fields_are_not_player_visible() -> None:
    provider = _provider_with_stream(
        [
            _sse_line('{"choices":[{"delta":{"thinking":"내부 추론"}}]}'),
            _sse_line('{"choices":[{"delta":{"reasoning":"숨김"}}]}'),
            _sse_line('{"choices":[{"delta":{"content":"보이는 대사"}}]}'),
            b"data: [DONE]\n\n",
        ]
    )

    events = await _collect(provider)
    visible = "".join(e.content or "" for e in events if e.type == "text_delta")
    done = [e for e in events if e.type == "done"]

    assert visible == "보이는 대사"
    assert "내부 추론" not in visible
    assert "숨김" not in visible
    assert done[0].full_text == "보이는 대사"


@pytest.mark.asyncio
async def test_graceful_completion_without_done_sentinel() -> None:
    provider = _provider_with_stream(
        [_sse_line('{"choices":[{"delta":{"content":"끝"}}]}')]
    )

    events = await _collect(provider)
    assert events[-1].type == "done"
    assert events[-1].full_text == "끝"


@pytest.mark.asyncio
async def test_sampling_sends_top_p_and_top_k() -> None:
    captured: dict[str, object] = {}

    def handler(request: httpx.Request) -> httpx.Response:
        captured["json"] = json.loads(request.content)
        return httpx.Response(
            200,
            headers={"content-type": "text/event-stream"},
            content=b"data: [DONE]\n\n",
        )

    client = httpx.AsyncClient(
        transport=httpx.MockTransport(handler),
        base_url="http://127.0.0.1:9379",
    )
    provider = LiteRTProvider(
        base_url="http://127.0.0.1:9379",
        model="gemma4-e2b",
        num_ctx=4096,
        think=False,
        client=client,
        top_p=0.95,
        top_k=64,
    )

    async for _ in provider.stream_chat(
        messages=[{"role": "user", "content": "안녕"}],
        temperature=0.8,
        max_tokens=120,
    ):
        pass

    body = captured["json"]
    assert isinstance(body, dict)
    assert body["temperature"] == pytest.approx(0.8)
    assert body["top_p"] == pytest.approx(0.95)
    assert body["max_tokens"] == 120
    options = body.get("options")
    assert isinstance(options, dict)
    assert options["top_k"] == 64
    assert options["num_ctx"] == 4096
