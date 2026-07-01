from __future__ import annotations

import logging
from typing import AsyncIterator

from config import Settings
from llm_defense.message_builder import build_llm_messages
from models.requests import ChatRequest
from models.responses import ChatResponse, FunctionCallResult, SSEEvent
from providers.base import AIProvider
from services.answer_grader import grade_user_answer
from services.hint_rewrite import (
    HINT_REWRITE_SERVER_INSTRUCTION,
    apply_hint_rewrite_fallback,
    build_hint_rewrite_external_document,
)
from services.quiz_bank import QuizBank
from services.tutor_rag_service import TutorRAGService
from tools.registry import ToolRegistry

logger = logging.getLogger(__name__)

_MSG_ALL_ENGINES_FAILED = "모든 AI 엔진 실패"
_MSG_RATE_LIMIT = "AI 사용 한도에 도달했습니다. 잠시 후 다시 시도해 주세요."


def _user_visible_ai_error(last_error: BaseException | None) -> str:
    """Map provider exceptions to a short player-facing string (Korean)."""
    if last_error is None:
        return _MSG_ALL_ENGINES_FAILED
    raw = str(last_error)
    lower = raw.lower()
    if "429" in raw or "rate_limit" in lower or "too many requests" in lower:
        return _MSG_RATE_LIMIT
    return _MSG_ALL_ENGINES_FAILED


class ChatService:
    """Orchestrates AI provider calls with tool injection and automatic fallback."""

    def __init__(
        self,
        primary: AIProvider,
        fallback: AIProvider | None,
        registry: ToolRegistry,
        temperature: float = 0.7,
        max_tokens: int = 512,
        *,
        app_settings: Settings | None = None,
        tutor_rag: TutorRAGService | None = None,
        quiz_bank: QuizBank | None = None,
    ) -> None:
        self._primary = primary
        self._fallback = fallback
        self._registry = registry
        self._temperature = temperature
        self._max_tokens = max_tokens
        self._app_settings = app_settings
        self._tutor_rag = tutor_rag
        self._quiz_bank = quiz_bank

    _TOOL_INSTRUCTION = (
        "\n\n[중요: 응답 방식] "
        "1) 반드시 캐릭터 대사를 텍스트로 먼저 말하세요. "
        "2) 텍스트와 별개로, 제공된 tool/function을 호출하여 게임 액션을 지시하세요. "
        "3) 절대로 텍스트 안에 function call 구문이나 JSON을 넣지 마세요. "
        "텍스트 응답과 tool 호출은 완전히 분리되어야 합니다."
    )

    def _limits(self) -> tuple[int, int, int]:
        if self._app_settings is None:
            return (4096, 16000, 12000)
        return (
            self._app_settings.chat_max_prompt_chars,
            self._app_settings.chat_max_client_system_chars,
            self._app_settings.chat_max_external_document_chars,
        )

    def _gather_external_documents(self, request: ChatRequest) -> list[tuple[str, str]]:
        docs: list[tuple[str, str]] = []
        if request.hint_rewrite is not None:
            docs.append((
                "hint_rewrite",
                build_hint_rewrite_external_document(request.hint_rewrite),
            ))
        if request.rag_profile != "tutor":
            return docs
        if self._app_settings is None:
            return docs

        if self._tutor_rag:
            q = (request.rag_query or request.prompt).strip()
            top_k = request.rag_top_k or self._app_settings.tutor_rag_top_k
            rag = self._tutor_rag.build_context_block(
                q,
                top_k=top_k,
                max_context_chars=self._app_settings.tutor_rag_max_context_chars,
            )
            if rag.strip():
                docs.append(("tutor_rag", rag))

        if self._quiz_bank and request.current_question_id:
            row = self._quiz_bank.get(request.current_question_id)
            if row:
                docs.append(("quiz_bank", self._quiz_bank.format_bank_context_block(row)))
        return docs

    def _build_messages(self, request: ChatRequest) -> list[dict]:
        mp, ms, mx = self._limits()
        external = self._gather_external_documents(request)
        server_instructions: list[str] = []
        if request.use_tools and len(self._registry) > 0:
            server_instructions.append(self._TOOL_INSTRUCTION)
        if request.hint_rewrite is not None:
            server_instructions.append(HINT_REWRITE_SERVER_INSTRUCTION)
        tool_inst = "\n\n".join(server_instructions) if server_instructions else None
        return build_llm_messages(
            client_system_raw=request.system,
            user_prompt_raw=request.prompt,
            external_documents=external,
            server_tool_instruction=tool_inst,
            max_prompt_chars=mp,
            max_client_system_chars=ms,
            max_external_doc_chars=mx,
        )

    def _get_tools(self, request: ChatRequest) -> list[dict] | None:
        if not request.use_tools or len(self._registry) == 0:
            return None
        return self._registry.get_all_openai_format()

    def _max_tokens_for_request(self, request: ChatRequest) -> int:
        cap = self._max_tokens
        if self._app_settings is not None:
            cap = min(cap, self._app_settings.max_tokens_hard_cap)
        if self._app_settings is not None and request.rag_profile == "tutor":
            tcap = self._app_settings.tutor_chat_max_tokens
            if tcap > 0:
                cap = min(cap, tcap)
        return max(1, cap)

    def _apply_tutor_quiz_override(self, request: ChatRequest, result: ChatResponse) -> ChatResponse:
        """If CSV grader says correct, force update_quiz.is_correct True."""
        if request.rag_profile != "tutor" or not request.current_question_id:
            return result
        if self._quiz_bank is None or self._app_settings is None:
            return result
        row = self._quiz_bank.get(request.current_question_id)
        if row is None:
            return result

        if not grade_user_answer(
            request.prompt,
            row.acceptable_answers,
            fuzzy_ratio=self._app_settings.tutor_grade_fuzzy_ratio,
            fuzzy_max_len=self._app_settings.tutor_grade_fuzzy_max_len,
        ):
            return result

        new_calls: list[FunctionCallResult] = []
        for fc in result.function_calls:
            if fc.name == "update_quiz" and isinstance(fc.arguments, dict):
                args = dict(fc.arguments)
                args["is_correct"] = True
                new_calls.append(FunctionCallResult(name=fc.name, arguments=args))
            else:
                new_calls.append(fc)
        return ChatResponse(response=result.response, function_calls=new_calls)

    async def stream_chat(self, request: ChatRequest) -> AsyncIterator[SSEEvent]:
        messages = self._build_messages(request)
        tools = self._get_tools(request)

        primary_err: BaseException | None = None
        try:
            max_tok = self._max_tokens_for_request(request)
            logger.info("Attempting primary provider: %s", self._primary.name)
            async for event in self._primary.stream_chat(
                messages=messages,
                tools=tools,
                temperature=self._temperature,
                max_tokens=max_tok,
            ):
                yield event
            return
        except Exception as exc:
            primary_err = exc
            logger.exception("Primary provider (%s) failed: %s", self._primary.name, exc)

        if self._fallback is None:
            yield SSEEvent(type="error", content=_user_visible_ai_error(primary_err))
            yield SSEEvent(type="done", full_text="")
            return

        try:
            max_tok = self._max_tokens_for_request(request)
            logger.info("Falling back to: %s", self._fallback.name)
            async for event in self._fallback.stream_chat(
                messages=messages,
                tools=tools,
                temperature=self._temperature,
                max_tokens=max_tok,
            ):
                yield event
        except Exception as exc:
            logger.exception("Fallback provider (%s) also failed: %s", self._fallback.name, exc)
            fb_msg = _user_visible_ai_error(exc)
            final_msg = (
                fb_msg
                if fb_msg != _MSG_ALL_ENGINES_FAILED
                else _user_visible_ai_error(primary_err)
            )
            yield SSEEvent(type="error", content=final_msg)
            yield SSEEvent(type="done", full_text="")

    async def chat(self, request: ChatRequest) -> ChatResponse:
        """Non-streaming chat that collects all events into a single ChatResponse."""
        full_text_parts: list[str] = []
        function_calls: list[FunctionCallResult] = []
        error_text: str | None = None

        async for event in self.stream_chat(request):
            if event.type == "text_delta" and event.content:
                full_text_parts.append(event.content)
            elif event.type == "function_call" and event.name:
                function_calls.append(
                    FunctionCallResult(name=event.name, arguments=event.arguments or {})
                )
            elif event.type == "error":
                error_text = event.content
            elif event.type == "done" and event.full_text:
                full_text_parts = [event.full_text]

        response_text = "".join(full_text_parts) if full_text_parts else (error_text or "")
        if request.hint_rewrite is not None:
            response_text = apply_hint_rewrite_fallback(response_text, request.hint_rewrite)
        result = ChatResponse(response=response_text, function_calls=function_calls)
        return self._apply_tutor_quiz_override(request, result)
