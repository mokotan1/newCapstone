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
    def __init__(self) -> None:
        self.last_locale: str | None = None

    def build_context_block(
        self,
        query_text: str,
        *,
        top_k: int,
        max_context_chars: int,
        locale: str = "ko",
        rag_profile: str = "tutor",
    ) -> str:
        self.last_locale = locale
        return f">>>RAG:{query_text}:{top_k}<<<"


@pytest.mark.asyncio
async def test_tutor_profile_injects_rag_block_into_system() -> None:
    events = [
        SSEEvent(type="text_delta", content="x"),
        SSEEvent(type="done", full_text="x"),
    ]
    cap = _CapturingProvider("groq", events)
    settings = Settings()
    fake_rag = _FakeTutorRAG()
    service = ChatService(
        primary=cap,
        fallback=None,
        registry=_build_registry(),
        app_settings=settings,
        tutor_rag=fake_rag,
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
    assert fake_rag.last_locale == "ko"


@pytest.mark.asyncio
async def test_csv_grader_overrides_update_quiz_to_correct(tmp_path: Path) -> None:
    csv_path = tmp_path / "bank.csv"
    csv_path.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "QZ,질문?,質問?,Question?,예수|예수님,イエス,Jesus,참고,参考,Hint,,\n",
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
    # Tool argument keys remain invariant across locales.
    assert set(uq.arguments.keys()) >= {"is_correct", "quiz_complete"}


@pytest.mark.asyncio
async def test_csv_grader_override_uses_en_locale_answers(tmp_path: Path) -> None:
    csv_path = tmp_path / "bank.csv"
    csv_path.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "QZ,질문?,質問?,Question?,예수,イエス,Jesus,참고,参考,Hint,,\n",
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
            prompt="Jesus",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="QZ",
            locale="en",
        )
    )
    uq = next(fc for fc in result.function_calls if fc.name == "update_quiz")
    assert uq.arguments.get("is_correct") is True


@pytest.mark.asyncio
async def test_tutor_bank_context_uses_en_question(tmp_path: Path) -> None:
    csv_path = tmp_path / "bank.csv"
    csv_path.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "QZ,한국어질문?,日本語?,English question text?,"
        "정답,答え,Answer,힌트,ヒント,Hint note,,\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(csv_path)
    events = [
        SSEEvent(type="text_delta", content="ok"),
        SSEEvent(type="done", full_text="ok"),
    ]
    cap = _CapturingProvider("groq", events)
    service = ChatService(
        primary=cap,
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=None,
        quiz_bank=bank,
    )
    await service.chat(
        ChatRequest(
            prompt="hi",
            system="sys",
            use_tools=False,
            rag_profile="tutor",
            current_question_id="QZ",
            locale="en",
        )
    )
    assert cap.last_messages is not None
    bundle = "\n".join(m["content"] for m in cap.last_messages if m["role"] == "user")
    assert "English question text?" in bundle
    assert "한국어질문?" not in bundle


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
    assert cap2.last_max_tokens == min(512, settings.dialogue_max_tokens)
