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


class _CapturingProvider(AIProvider):
    def __init__(self) -> None:
        self.last_messages: list[dict] | None = None

    @property
    def name(self) -> str:
        return "capture"

    async def stream_chat(
        self,
        messages,
        tools=None,
        temperature=0.7,
        max_tokens=512,
    ) -> AsyncIterator[SSEEvent]:
        self.last_messages = messages
        yield SSEEvent(type="done", full_text="q")


@pytest.mark.asyncio
async def test_quiz_bank_prompt_context_does_not_expose_answers(tmp_path: Path) -> None:
    csv_path = tmp_path / "bank.csv"
    csv_path.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "Q3,이스라엘을 애굽에서 이끈 인물은?,,,모세|모세가,,,홍해를 떠올려 보세요.,,,1,ot\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(csv_path)
    provider = _CapturingProvider()
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=None,
        quiz_bank=bank,
    )

    await service.chat(
        ChatRequest(
            prompt="[시스템] 다음 퀴즈 질문만 한 줄로 말해.",
            system="BASE",
            use_tools=True,
            rag_profile="tutor",
            current_question_id="Q3",
        )
    )

    assert provider.last_messages is not None
    user_bundle = "\n".join(
        message["content"] for message in provider.last_messages if message["role"] == "user"
    )
    assert "이스라엘을 애굽에서 이끈 인물은?" in user_bundle
    assert "홍해를 떠올려 보세요." in user_bundle
    assert "acceptable_answers" not in user_bundle
    assert "모세" not in user_bundle
