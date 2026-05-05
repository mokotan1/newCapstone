from __future__ import annotations

from typing import Any

from pydantic import BaseModel, ConfigDict, Field, model_validator


class ChatRequest(BaseModel):
    """POST /chat 본문. 일부 배포(Gains 등)는 `message`·`user_id`를 요구하므로 호환 필드를 둡니다."""

    model_config = ConfigDict(extra="ignore")

    prompt: str = Field(..., min_length=1, max_length=4096)
    system: str = "당신은 저택의 도우미입니다."
    use_tools: bool = True
    #: Gains·운영 분석 호환용. 채팅 로직에서는 사용하지 않음.
    user_id: str | None = Field(default=None, max_length=256)
    #: `prompt`와 동일 텍스트를 기대하는 백엔드 호환용 별칭. prompt가 비었을 때만 채워짐.
    message: str | None = Field(default=None, max_length=4096)
    rag_profile: str | None = None
    rag_query: str | None = Field(None, max_length=4096)
    current_question_id: str | None = Field(None, max_length=128)
    rag_top_k: int | None = Field(None, ge=1, le=20)

    @model_validator(mode="before")
    @classmethod
    def _message_into_prompt(cls, data: Any) -> Any:
        """배포본이 `message`만 보내는 경우 prompt로 승격."""
        if not isinstance(data, dict):
            return data
        d = dict(data)
        pt = d.get("prompt")
        prompt_ok = isinstance(pt, str) and len(pt.strip()) >= 1
        msg = d.get("message")
        if not prompt_ok and isinstance(msg, str) and len(msg.strip()) >= 1:
            text = msg.strip()
            if len(text) > 4096:
                text = text[:4096]
            d["prompt"] = text
        return d


class TutorGradeRequest(BaseModel):
    question_id: str = Field(..., min_length=1, max_length=128)
    # 빈 문자열은 오답 처리(grade_user_answer). Unity·Fungus 상태 오류로 le=100 초과 시 422 방지.
    user_answer: str = Field(default="", max_length=4000)
    correct_count_before: int = Field(default=0, ge=0, le=10_000)
    quiz_target: int = Field(default=5, ge=1, le=50)
