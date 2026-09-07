from __future__ import annotations

from collections.abc import AsyncIterator

import pytest
from config import Settings
from models.requests import ChatRequest
from models.responses import SSEEvent
from providers.base import AIProvider
from services.chat_service import ChatService, _user_visible_ai_error
from services.locale_support import response_language_instruction, user_visible_ai_error
from tools.game_tools import GAME_TOOLS
from tools.registry import ToolRegistry


class _MockProvider(AIProvider):
    """Configurable mock provider for testing."""

    def __init__(self, provider_name: str, events: list[SSEEvent] | None = None, should_fail: bool = False):
        self._name = provider_name
        self._events = events or []
        self._should_fail = should_fail
        self.last_tools: list[dict] | None = None
        self.last_temperature: float | None = None
        self.last_max_tokens: int | None = None

    @property
    def name(self) -> str:
        return self._name

    async def stream_chat(self, messages, tools=None, temperature=0.7, max_tokens=512) -> AsyncIterator[SSEEvent]:
        self.last_tools = tools
        self.last_temperature = temperature
        self.last_max_tokens = max_tokens
        if self._should_fail:
            raise RuntimeError(f"{self._name} failure")
        for event in self._events:
            yield event


class _CapturingProvider(_MockProvider):
    def __init__(self, events: list[SSEEvent]):
        super().__init__("capture", events)
        self.last_messages = None

    async def stream_chat(self, messages, tools=None, temperature=0.7, max_tokens=512) -> AsyncIterator[SSEEvent]:
        self.last_messages = messages
        async for event in super().stream_chat(
            messages,
            tools=tools,
            temperature=temperature,
            max_tokens=max_tokens,
        ):
            yield event


def test_user_visible_ai_error_rate_limit() -> None:
    msg = _user_visible_ai_error(RuntimeError("Error code: 429 - rate_limit_exceeded"))
    assert "한도" in msg


def test_user_visible_ai_error_generic() -> None:
    assert _user_visible_ai_error(RuntimeError("broken")) == "모든 AI 엔진 실패"


def test_user_visible_error_rate_limit_en() -> None:
    msg = user_visible_ai_error(
        RuntimeError("Error code: 429 - rate_limit_exceeded"),
        locale="en",
    )
    lower = msg.lower()
    assert "limit" in lower or "rate" in lower


def test_user_visible_error_generic_en() -> None:
    msg = user_visible_ai_error(RuntimeError("broken"), locale="en")
    assert "fail" in msg.lower()


def test_user_visible_error_rate_limit_ja() -> None:
    msg = user_visible_ai_error(
        RuntimeError("Error code: 429 - too many requests"),
        locale="ja",
    )
    assert msg  # non-empty localized Japanese
    assert "한도" not in msg
    assert "limit" not in msg.lower()


def test_user_visible_error_generic_ja() -> None:
    msg = user_visible_ai_error(RuntimeError("broken"), locale="ja")
    assert msg
    assert "모든 AI" not in msg


@pytest.mark.asyncio
async def test_build_messages_includes_response_language_rule_for_ja() -> None:
    provider = _CapturingProvider(
        [SSEEvent(type="done", full_text="ok")]
    )
    service = ChatService(provider, None, ToolRegistry())
    await service.chat(
        ChatRequest(
            prompt="hello",
            system="UNIQUE_CLIENT_PERSONA_MARKER",
            locale="ja",
            use_tools=False,
        )
    )
    assert provider.last_messages is not None
    system = provider.last_messages[0]["content"]
    user = provider.last_messages[1]["content"]
    expected = response_language_instruction("ja")
    assert expected
    assert expected in system
    assert "Japanese" in expected or "日本語" in expected
    # Untrusted client system must stay out of the trusted system channel.
    assert "UNIQUE_CLIENT_PERSONA_MARKER" not in system
    assert "UNIQUE_CLIENT_PERSONA_MARKER" in user


@pytest.mark.asyncio
async def test_tool_instruction_en_uses_english_markers() -> None:
    """locale=en trusted system must use EN tool-instruction prose, not KO chrome."""
    provider = _CapturingProvider([SSEEvent(type="done", full_text="ok")])
    service = ChatService(provider, None, _build_registry())
    await service.chat(
        ChatRequest(
            prompt="hello",
            system="persona",
            locale="en",
            use_tools=True,
            rag_profile="tutor",
        )
    )
    assert provider.last_messages is not None
    system = provider.last_messages[0]["content"]
    assert "중요: 응답 방식" not in system
    assert "반드시 캐릭터" not in system
    lower = system.lower()
    assert "tool" in lower or "function" in lower
    assert "important" in lower or "response" in lower


@pytest.mark.asyncio
async def test_cheshire_dialogue_omits_tool_instruction() -> None:
    provider = _CapturingProvider([SSEEvent(type="done", full_text="ok")])
    service = ChatService(provider, None, _build_registry())
    await service.chat(
        ChatRequest(prompt="hello", system="persona", locale="en", use_tools=True),
    )
    assert provider.last_messages is not None
    system = provider.last_messages[0]["content"]
    assert "Important: response format" not in system
    assert "중요: 응답 방식" not in system


@pytest.mark.asyncio
async def test_stream_error_localized_for_en() -> None:
    service = ChatService(
        primary=_MockProvider("groq", should_fail=True),
        fallback=None,
        registry=_build_registry(),
    )
    collected = [
        e async for e in service.stream_chat(ChatRequest(prompt="hi", locale="en"))
    ]
    error_events = [e for e in collected if e.type == "error"]
    assert len(error_events) == 1
    assert "fail" in (error_events[0].content or "").lower()


def _build_registry() -> ToolRegistry:
    reg = ToolRegistry()
    reg.register_many(GAME_TOOLS)
    return reg


def _request(prompt: str = "테스트") -> ChatRequest:
    return ChatRequest(prompt=prompt, system="테스트 시스템", use_tools=True)


@pytest.mark.asyncio
class TestChatServiceStreaming:

    async def test_primary_success(self):
        events = [
            SSEEvent(type="text_delta", content="응답"),
            SSEEvent(type="done", full_text="응답"),
        ]
        service = ChatService(
            primary=_MockProvider("groq", events),
            fallback=_MockProvider("gemini", [SSEEvent(type="done", full_text="fallback")]),
            registry=_build_registry(),
        )

        collected = [e async for e in service.stream_chat(_request())]
        assert any(e.type == "text_delta" and e.content == "응답" for e in collected)
        assert collected[-1].type == "done"

    async def test_fallback_on_primary_failure(self):
        fallback_events = [
            SSEEvent(type="text_delta", content="폴백 응답"),
            SSEEvent(type="done", full_text="폴백 응답"),
        ]
        service = ChatService(
            primary=_MockProvider("groq", should_fail=True),
            fallback=_MockProvider("gemini", fallback_events),
            registry=_build_registry(),
        )

        collected = [e async for e in service.stream_chat(_request())]
        assert any(e.content == "폴백 응답" for e in collected if e.type == "text_delta")

    async def test_all_providers_fail_yields_error(self):
        service = ChatService(
            primary=_MockProvider("groq", should_fail=True),
            fallback=_MockProvider("gemini", should_fail=True),
            registry=_build_registry(),
        )

        collected = [e async for e in service.stream_chat(_request())]
        error_events = [e for e in collected if e.type == "error"]
        assert len(error_events) == 1
        assert "실패" in error_events[0].content

    async def test_no_fallback_yields_error(self):
        service = ChatService(
            primary=_MockProvider("groq", should_fail=True),
            fallback=None,
            registry=_build_registry(),
        )

        collected = [e async for e in service.stream_chat(_request())]
        assert any(e.type == "error" for e in collected)


@pytest.mark.asyncio
class TestChatServiceNonStreaming:

    async def test_chat_collects_text_and_function_calls(self):
        events = [
            SSEEvent(type="text_delta", content="켁켁!"),
            SSEEvent(type="function_call", name="give_hint", arguments={"hint_level": "moderate", "target_object": "bed", "hint_category": "location"}),
            SSEEvent(type="function_call", name="emote", arguments={"emotion": "mock"}),
            SSEEvent(type="done", full_text="켁켁!"),
        ]
        service = ChatService(
            primary=_MockProvider("groq", events),
            fallback=None,
            registry=_build_registry(),
        )

        result = await service.chat(
            ChatRequest(prompt="테스트", system="튜터", use_tools=True, rag_profile="tutor"),
        )
        assert result.response == "켁켁!"
        assert len(result.function_calls) == 2
        assert result.function_calls[0].name == "give_hint"
        assert result.function_calls[1].name == "emote"

    async def test_chat_returns_error_text_on_failure(self):
        service = ChatService(
            primary=_MockProvider("groq", should_fail=True),
            fallback=None,
            registry=_build_registry(),
        )

        result = await service.chat(_request())
        assert "실패" in result.response

    async def test_tools_disabled_passes_none(self):
        events = [
            SSEEvent(type="text_delta", content="답변"),
            SSEEvent(type="done", full_text="답변"),
        ]
        service = ChatService(
            primary=_MockProvider("groq", events),
            fallback=None,
            registry=_build_registry(),
        )

        request = ChatRequest(prompt="test", use_tools=False)
        result = await service.chat(request)
        assert result.response == "답변"
        assert len(result.function_calls) == 0


def _tool_names(tools: list[dict] | None) -> set[str]:
    if not tools:
        return set()
    return {str(item["function"]["name"]) for item in tools}


@pytest.mark.asyncio
async def test_cheshire_dialogue_never_receives_game_tool_registry() -> None:
    """Cheshire dialogue must not expose give_hint/emote/update_quiz even if the client asks."""
    events = [
        SSEEvent(type="text_delta", content="켁."),
        SSEEvent(type="done", full_text="켁."),
    ]
    provider = _MockProvider("groq", events)
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
    )

    result = await service.chat(
        ChatRequest(prompt="안녕", system="체셔", use_tools=True),
    )

    assert provider.last_tools is None
    assert result.function_calls == []
    assert "give_hint" not in _tool_names(provider.last_tools)
    assert "update_quiz" not in _tool_names(provider.last_tools)
    assert "emote" not in _tool_names(provider.last_tools)


@pytest.mark.asyncio
async def test_tutor_chat_still_receives_game_tools_when_requested() -> None:
    events = [
        SSEEvent(type="text_delta", content="채점 중"),
        SSEEvent(type="done", full_text="채점 중"),
    ]
    provider = _MockProvider("groq", events)
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
    )

    await service.chat(
        ChatRequest(
            prompt="골리앗",
            system="튜터",
            use_tools=True,
            rag_profile="tutor",
        ),
    )

    assert provider.last_tools is not None
    names = _tool_names(provider.last_tools)
    assert "update_quiz" in names
    assert "give_hint" in names
@pytest.mark.asyncio
async def test_hint_rewrite_adds_trusted_policy_and_untrusted_document():
    provider = _CapturingProvider(
        [SSEEvent(type="done", full_text="병은 목마르다. 싱크대가 기억한다.")]
    )
    service = ChatService(provider, None, ToolRegistry())

    await service.chat(
        ChatRequest(
            prompt="이 병 어디다 써?",
            system="client scene",
            hint_rewrite={
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
            },
        )
    )

    assert provider.last_messages is not None
    assert "rewrite only" in provider.last_messages[0]["content"]
    user_bundle = "\n".join(m["content"] for m in provider.last_messages if m["role"] == "user")
    assert "hint_rewrite" in user_bundle
    assert "opaque_bottle_sink_use" in user_bundle


@pytest.mark.asyncio
async def test_hint_rewrite_forbidden_term_falls_back():
    provider = _CapturingProvider(
        [SSEEvent(type="done", full_text="싱크대에서 열쇠를 꺼내.")]
    )
    service = ChatService(provider, None, ToolRegistry())

    result = await service.chat(
        ChatRequest(
            prompt="이 병 어디다 써?",
            hint_rewrite={
                "hint_id": "opaque_bottle_sink_use",
                "item_id": "opaque_bottle",
                "hint_target": "kitchen_sink",
                "hint_level": "direct",
                "base_hint": "이 병은 주방 싱크대에서 사용할 수 있다.",
                "required_terms": ["병", "싱크대"],
                "forbidden_terms": ["열쇠"],
                "fallback_line": "그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
            },
        )
    )

    assert result.response == "그 병은 주방 싱크대에서 물을 채워볼 수 있다."


def test_sse_event_frame_uses_data_prefix_and_blank_line() -> None:
    from services.sse_format import format_sse_event

    frame = format_sse_event(SSEEvent(type="text_delta", content="Hello"))

    assert frame.startswith("data: ")
    assert frame.endswith("\n\n")
    payload = frame[len("data: ") :].rstrip("\n")
    parsed = SSEEvent.model_validate_json(payload)
    assert parsed.type == "text_delta"
    assert parsed.content == "Hello"


@pytest.mark.asyncio
async def test_dialogue_only_uses_dialogue_temperature() -> None:
    events = [
        SSEEvent(type="text_delta", content="켁."),
        SSEEvent(type="done", full_text="켁."),
    ]
    provider = _MockProvider("groq", events)
    settings = Settings(dialogue_temperature=0.8, default_temperature=0.7)
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
        temperature=settings.default_temperature,
        app_settings=settings,
    )

    await service.chat(ChatRequest(prompt="안녕", system="체셔", use_tools=False))

    assert provider.last_temperature == pytest.approx(0.8)


@pytest.mark.asyncio
async def test_tutor_chat_keeps_default_temperature() -> None:
    events = [
        SSEEvent(type="text_delta", content="채점"),
        SSEEvent(type="done", full_text="채점"),
    ]
    provider = _MockProvider("groq", events)
    settings = Settings(dialogue_temperature=0.8, default_temperature=0.7)
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
        temperature=settings.default_temperature,
        app_settings=settings,
    )

    await service.chat(
        ChatRequest(prompt="골리앗", system="튜터", use_tools=True, rag_profile="tutor"),
    )

    assert provider.last_temperature == pytest.approx(0.7)


@pytest.mark.asyncio
async def test_dialogue_only_replaces_json_reply_with_fallback() -> None:
    from services.dialogue_guard import dialogue_fallback_line

    events = [
        SSEEvent(type="text_delta", content='{"name":"give_hint"}'),
        SSEEvent(type="done", full_text='{"name":"give_hint"}'),
    ]
    service = ChatService(
        primary=_MockProvider("groq", events),
        fallback=None,
        registry=_build_registry(),
    )

    result = await service.chat(ChatRequest(prompt="안녕", system="체셔", use_tools=False))
    assert result.response == dialogue_fallback_line("ko")
    assert result.function_calls == []


@pytest.mark.asyncio
async def test_dialogue_only_stream_forwards_deltas_live_and_drops_tools() -> None:
    from services.dialogue_guard import dialogue_fallback_line

    events = [
        SSEEvent(type="text_delta", content="첫 문장이다. "),
        SSEEvent(type="text_delta", content="둘째다. 셋째다."),
        SSEEvent(
            type="function_call",
            name="emote",
            arguments={"emotion": "mock"},
        ),
        SSEEvent(type="done", full_text="첫 문장이다. 둘째다. 셋째다."),
    ]
    service = ChatService(
        primary=_MockProvider("groq", events),
        fallback=None,
        registry=_build_registry(),
    )

    collected = [
        e async for e in service.stream_chat(ChatRequest(prompt="안녕", system="체셔"))
    ]
    deltas = [e.content for e in collected if e.type == "text_delta"]
    done = [e for e in collected if e.type == "done"]

    assert deltas == ["첫 문장이다. ", "둘째다. 셋째다."]
    assert done[-1].full_text == dialogue_fallback_line("ko")
    assert not any(e.type == "function_call" for e in collected)
