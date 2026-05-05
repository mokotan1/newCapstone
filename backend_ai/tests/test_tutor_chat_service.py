from __future__ import annotations

from pathlib import Path
from typing import AsyncIterator

import pytest

from config import Settings
from models.requests import ChatRequest
from models.responses import SSEEvent
from services.chat_service import ChatService
from services.quiz_bank import QuizBank
from tests.test_chat_service import _MockProvider, _build_registry


class _CapturingProvider(_MockProvider):
    def __init__(self, provider_name: str, events: list[SSEEvent] | None = None, should_fail: bool = False):
        super().__init__(provider_name, events, should_fail)
        self.last_messages: list[dict] | None = None

    async def stream_chat(self, messages, tools=None, temperature=0.7, max_tokens=512) -> AsyncIterator[SSEEvent]:
        self.last_messages = messages
        async for e in super().stream_chat(messages, tools=tools, temperature=temperature, max_tokens=max_tokens):
            yield e


class _FakeTutorRAG:
    def build_context_block(self, query_text: str, *, top_k: int, max_context_chars: int) -> str:
        return f">>>RAG:{query_text}:{top_k}<<<"


@pytest.mark.asyncio
async def test_tutor_profile_injects_rag_block_into_system() -> None:
    events = [
        SSEEvent(type="text_delta", content="x"),
        SSEEvent(type="done", full_text="x"),
    ]
    cap = _CapturingProvider("groq", events)
    settings = Settings()
    service = ChatService(
        primary=cap,
        fallback=None,
        registry=_build_registry(),
        app_settings=settings,
        tutor_rag=_FakeTutorRAG(),
        quiz_bank=None,
    )

    await service.chat(
        ChatRequest(
            prompt="플레이어답",
            system="BASE",
            use_tools=False,
            rag_profile="tutor",
        )
    )

    assert cap.last_messages is not None
    assert cap.last_messages[0]["role"] == "system"
    trusted = cap.last_messages[0]["content"]
    assert "서버 보안 정책" in trusted
    user_msgs = [m for m in cap.last_messages if m["role"] == "user"]
    assert user_msgs
    bundle = "\n".join(m["content"] for m in user_msgs)
    assert "&gt;&gt;&gt;RAG:플레이어답:5&lt;&lt;&lt;" in bundle or ">>>RAG:플레이어답:" in bundle
    assert "<scene_config" in bundle and "BASE" in bundle
    assert "<user_input>" in bundle


@pytest.mark.asyncio
async def test_csv_grader_overrides_update_quiz_to_correct(tmp_path: Path) -> None:
    csv_path = tmp_path / "bank.csv"
    csv_path.write_text(
        "question_id,question_ko,acceptable_answers,reference_snippet,difficulty,tags\n"
        "QZ,질문?,예수|예수님,참고,,",
        encoding="utf-8",
    )
    bank = QuizBank.load(csv_path)
    events = [
        SSEEvent(type="text_delta", content="ok"),
        SSEEvent(
            type="function_call",
            name="update_quiz",
            arguments={"is_correct": False, "quiz_complete": False},
        ),
        SSEEvent(type="done", full_text="ok"),
    ]
    service = ChatService(
        primary=_MockProvider("groq", events),
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=None,
        quiz_bank=bank,
    )

    result = await service.chat(
        ChatRequest(
            prompt="예수님",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="QZ",
        )
    )
    assert result.function_calls
    uq = next(fc for fc in result.function_calls if fc.name == "update_quiz")
    assert uq.arguments.get("is_correct") is True


class _MaxTokenCaptureProvider(_MockProvider):
    def __init__(self, provider_name: str, events: list[SSEEvent] | None = None) -> None:
        super().__init__(provider_name, events)
        self.last_max_tokens: int | None = None

    async def stream_chat(
        self,
        messages,
        tools=None,
        temperature=0.7,
        max_tokens=512,
    ) -> AsyncIterator[SSEEvent]:
        self.last_max_tokens = max_tokens
        async for e in super().stream_chat(
            messages, tools=tools, temperature=temperature, max_tokens=max_tokens
        ):
            yield e


@pytest.mark.asyncio
async def test_tutor_profile_caps_max_tokens() -> None:
    events = [SSEEvent(type="done", full_text="x")]
    cap = _MaxTokenCaptureProvider("groq", events)
    settings = Settings()
    service = ChatService(
        primary=cap,
        fallback=None,
        registry=_build_registry(),
        max_tokens=512,
        app_settings=settings,
        tutor_rag=None,
        quiz_bank=None,
    )
    await service.chat(
        ChatRequest(prompt="a", system="s", use_tools=False, rag_profile="tutor")
    )
    assert cap.last_max_tokens == min(512, settings.tutor_chat_max_tokens)

    cap2 = _MaxTokenCaptureProvider("groq", events)
    service2 = ChatService(
        primary=cap2,
        fallback=None,
        registry=_build_registry(),
        max_tokens=512,
        app_settings=settings,
        tutor_rag=None,
        quiz_bank=None,
    )
    await service2.chat(ChatRequest(prompt="a", system="s", use_tools=False))
    assert cap2.last_max_tokens == 512
