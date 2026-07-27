"""Project wiki RAG profile injection via ChatService."""

from __future__ import annotations

from pathlib import Path
from typing import AsyncIterator

import pytest

from config import Settings
from models.requests import ChatRequest
from models.responses import SSEEvent
from services.chat_service import ChatService, _PROJECT_RAG_CITATION_INSTRUCTION
from services.quiz_bank import QuizBank
from tests.test_chat_service import _MockProvider, _build_registry
from tests.test_tutor_chat_service import _CapturingProvider, _MaxTokenCaptureProvider


class _ProjectFakeRAG:
    def build_context_block(
        self,
        query_text: str,
        *,
        top_k: int,
        max_context_chars: int,
        locale: str = "ko",
        rag_profile: str = "tutor",
    ) -> str:
        return (
            "[프로젝트 참고 자료 — 아래 인용문만 세계관·기획·구현 사실의 근거로 사용하세요. "
            "없는 내용은 상식으로 보충하지 마세요.]\n\n"
            "--- [1] source_id=scenario:abc123, source_path=시나리오/world.pdf, score=0.900\n"
            f"세계관 본문 ({query_text})"
        )


def _write_quiz_bank(path: Path) -> QuizBank:
    path.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "QZ,질문?,質問?,Question?,정답,答え,Answer,힌트,ヒント,Hint,,\n",
        encoding="utf-8",
    )
    return QuizBank.load(path)


def build_service_with_fake_rag_and_quiz_bank(
    tmp_path: Path,
) -> tuple[ChatService, _CapturingProvider]:
    events = [
        SSEEvent(type="text_delta", content="ok"),
        SSEEvent(type="done", full_text="ok"),
    ]
    provider = _CapturingProvider("groq", events)
    bank = _write_quiz_bank(tmp_path / "bank.csv")
    service = ChatService(
        primary=provider,
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=_ProjectFakeRAG(),
        quiz_bank=bank,
    )
    return service, provider


@pytest.mark.asyncio
async def test_project_profile_injects_rag_but_not_quiz_bank(tmp_path: Path) -> None:
    service, provider = build_service_with_fake_rag_and_quiz_bank(tmp_path)
    await service.chat(
        ChatRequest(
            prompt="세계관",
            system="base",
            use_tools=False,
            rag_profile="project",
            current_question_id="QZ",
        )
    )
    assert provider.last_messages is not None
    user_bundle = "\n".join(
        message["content"] for message in provider.last_messages if message["role"] == "user"
    )
    assert "source_id=scenario:abc123" in user_bundle
    assert "quiz_bank" not in user_bundle


@pytest.mark.asyncio
async def test_project_profile_adds_citation_instruction_to_trusted_system(
    tmp_path: Path,
) -> None:
    service, provider = build_service_with_fake_rag_and_quiz_bank(tmp_path)
    await service.chat(
        ChatRequest(
            prompt="세계관",
            system="base",
            use_tools=False,
            rag_profile="project",
        )
    )
    assert provider.last_messages is not None
    trusted = provider.last_messages[0]["content"]
    assert _PROJECT_RAG_CITATION_INSTRUCTION in trusted
    assert "cite the supplied source_id" in trusted


@pytest.mark.asyncio
async def test_project_profile_does_not_apply_tutor_token_cap(tmp_path: Path) -> None:
    events = [SSEEvent(type="done", full_text="x")]
    cap = _MaxTokenCaptureProvider("groq", events)
    settings = Settings()
    service = ChatService(
        primary=cap,
        fallback=None,
        registry=_build_registry(),
        max_tokens=512,
        app_settings=settings,
        tutor_rag=_ProjectFakeRAG(),
        quiz_bank=None,
    )
    await service.chat(
        ChatRequest(prompt="a", system="s", use_tools=False, rag_profile="project")
    )
    assert cap.last_max_tokens == 512


@pytest.mark.asyncio
async def test_project_profile_skips_quiz_answer_override(tmp_path: Path) -> None:
    events = [
        SSEEvent(type="text_delta", content="ok"),
        SSEEvent(
            type="function_call",
            name="update_quiz",
            arguments={"is_correct": False, "quiz_complete": False},
        ),
        SSEEvent(type="done", full_text="ok"),
    ]
    bank = _write_quiz_bank(tmp_path / "bank.csv")
    service = ChatService(
        primary=_MockProvider("groq", events),
        fallback=None,
        registry=_build_registry(),
        app_settings=Settings(),
        tutor_rag=_ProjectFakeRAG(),
        quiz_bank=bank,
    )
    result = await service.chat(
        ChatRequest(
            prompt="정답",
            system="sys",
            use_tools=True,
            rag_profile="project",
            current_question_id="QZ",
        )
    )
    uq = next(fc for fc in result.function_calls if fc.name == "update_quiz")
    assert uq.arguments.get("is_correct") is False
