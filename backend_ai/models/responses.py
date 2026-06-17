from __future__ import annotations

from typing import Any, Literal

from pydantic import BaseModel, Field


class FunctionCallResult(BaseModel):
    name: str
    arguments: dict[str, Any]


class ChatResponse(BaseModel):
    response: str
    function_calls: list[FunctionCallResult] = []


class TutorGradeResponse(BaseModel):
    is_correct: bool
    question_id: str = ""
    reference_snippet: str = Field(
        default="",
        description="힌트 원료(플레이어에게 길게 그대로 읽지 말 것).",
    )
    quiz_complete_after: bool = Field(
        default=False,
        description="이번 답이 정답으로 인정되면 미션 완료가 되는지(적용 전 기준).",
    )
    unknown_question: bool = False


class TelemetryResponse(BaseModel):
    status: Literal["ok"] = "ok"
    accepted: int = 0


class SSEEvent(BaseModel):
    type: Literal["text_delta", "function_call", "error", "done"]
    content: str | None = None
    name: str | None = None
    arguments: dict[str, Any] | None = None
    full_text: str | None = None
