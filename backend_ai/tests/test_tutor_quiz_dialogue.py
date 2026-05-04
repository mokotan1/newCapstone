"""Two-turn tutor quiz flow: mocked provider returns question then graded answer (ChatService.chat per turn)."""

from __future__ import annotations

from pathlib import Path
from typing import AsyncIterator

import pytest

from config import Settings
from models.requests import ChatRequest
from models.responses import SSEEvent
from providers.base import AIProvider
from services.chat_service import ChatService
from services.quiz_bank import QuizBank
from tests.test_chat_service import _build_registry

_CSV_HEADER = "question_id,question_ko,acceptable_answers,reference_snippet,difficulty,tags\n"


class _QueuedSSEProvider(AIProvider):
    """Each ``stream_chat`` consumes the next scripted list of SSE events and stores ``last_messages``."""

    def __init__(self, provider_name: str, turns: list[list[SSEEvent]]) -> None:
        self._name = provider_name
        self._turns = turns
        self._index = 0
        self.last_messages: list[dict] | None = None

    @property
    def consumed_turns(self) -> int:
        """Number of ``stream_chat`` calls completed."""
        return self._index

    @property
    def name(self) -> str:
        return self._name

    async def stream_chat(
        self,
        messages,
        tools=None,
        temperature=0.7,
        max_tokens=512,
    ) -> AsyncIterator[SSEEvent]:
        self.last_messages = messages
        if self._index >= len(self._turns):
            raise RuntimeError(f"_QueuedSSEProvider: no scripted turn at index {self._index}")
        for event in self._turns[self._index]:
            yield event
        self._index += 1


def _write_bank(tmp_path: Path, csv_body: str) -> QuizBank:
    p = tmp_path / "bank.csv"
    p.write_text(_CSV_HEADER + csv_body, encoding="utf-8")
    return QuizBank.load(p)


def _tutor_service(bank: QuizBank, turns: list[list[SSEEvent]]) -> tuple[ChatService, _QueuedSSEProvider]:
    provider = _QueuedSSEProvider("mock", turns)
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=None,
        quiz_bank=bank,
    )
    return service, provider


@pytest.mark.asyncio
async def test_two_turn_model_wrong_but_csv_correct_overrides(tmp_path: Path) -> None:
    bank = _write_bank(
        tmp_path,
        "Q1,다윗이 이긴 거인 이름은?,골리앗|골리엇,참고문구,,",
    )
    turn_question = [
        SSEEvent(type="text_delta", content="다윗이 이긴 거인 이름은?"),
        SSEEvent(type="done", full_text="다윗이 이긴 거인 이름은?"),
    ]
    turn_answer = [
        SSEEvent(type="text_delta", content="응답"),
        SSEEvent(
            type="function_call",
            name="update_quiz",
            arguments={"is_correct": False, "quiz_complete": False},
        ),
        SSEEvent(type="done", full_text="응답"),
    ]
    service, prov = _tutor_service(bank, [turn_question, turn_answer])

    r1 = await service.chat(
        ChatRequest(
            prompt="[튜터] 다음 퀴즈 질문만 해 줘.",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    assert "거인" in r1.response
    assert prov.consumed_turns == 1

    r2 = await service.chat(
        ChatRequest(
            prompt="골리앗",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    assert r2.function_calls
    uq = next(fc for fc in r2.function_calls if fc.name == "update_quiz")
    assert uq.arguments.get("is_correct") is True
    assert prov.consumed_turns == 2


@pytest.mark.asyncio
async def test_two_turn_wrong_answer_no_override(tmp_path: Path) -> None:
    bank = _write_bank(
        tmp_path,
        "Q1,다윗이 이긴 거인 이름은?,골리앗|골리엇,참고,,",
    )
    turn_question = [
        SSEEvent(type="text_delta", content="질문"),
        SSEEvent(type="done", full_text="질문"),
    ]
    turn_answer = [
        SSEEvent(type="text_delta", content="오답 처리"),
        SSEEvent(
            type="function_call",
            name="update_quiz",
            arguments={"is_correct": False, "quiz_complete": False},
        ),
        SSEEvent(type="done", full_text="오답 처리"),
    ]
    service, _ = _tutor_service(bank, [turn_question, turn_answer])

    await service.chat(
        ChatRequest(
            prompt="시작",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    r2 = await service.chat(
        ChatRequest(
            prompt="완전히_틀린_답변",
            system="sys",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    uq = next(fc for fc in r2.function_calls if fc.name == "update_quiz")
    assert uq.arguments.get("is_correct") is False


@pytest.mark.asyncio
async def test_second_turn_system_includes_quiz_bank_block(tmp_path: Path) -> None:
    bank = _write_bank(
        tmp_path,
        "Q1,다윗이 이긴 거인 이름은?,골리앗,스니펫,,",
    )
    turn_question = [
        SSEEvent(type="text_delta", content="q"),
        SSEEvent(type="done", full_text="q"),
    ]
    turn_answer = [
        SSEEvent(type="text_delta", content="a"),
        SSEEvent(type="done", full_text="a"),
    ]
    service, prov = _tutor_service(bank, [turn_question, turn_answer])

    await service.chat(
        ChatRequest(
            prompt="턴1",
            system="BASE",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    await service.chat(
        ChatRequest(
            prompt="골리앗",
            system="BASE",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q1",
        )
    )
    assert prov.last_messages is not None
    user_bundle = "\n".join(m["content"] for m in prov.last_messages if m["role"] == "user")
    assert "[문제 은행" in user_bundle
    assert "Q1" in user_bundle
    assert "다윗이 이긴 거인 이름은?" in user_bundle
