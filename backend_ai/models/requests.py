from __future__ import annotations

from typing import Any

from pydantic import BaseModel, ConfigDict, Field, model_validator


class HintRewritePayload(BaseModel):
    """Optional client-provided hint data for server-controlled Cheshire rewrites."""

    model_config = ConfigDict(extra="ignore")

    hint_id: str = Field(..., min_length=1, max_length=128)
    item_id: str = Field(..., min_length=1, max_length=128)
    hint_target: str = Field(..., min_length=1, max_length=128)
    hint_level: str = Field(..., min_length=1, max_length=64)
    base_hint: str = Field(..., min_length=1, max_length=1000)
    required_terms: list[str] = Field(default_factory=list, max_length=16)
    forbidden_terms: list[str] = Field(default_factory=list, max_length=32)
    fallback_line: str | None = Field(default=None, max_length=1000)
    narrative_seed: str | None = Field(default=None, max_length=1000)
    interaction_type: str | None = Field(default=None, max_length=128)
    allow_highlight: bool = True


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
    hint_rewrite: HintRewritePayload | None = None

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


# --- Telemetry (play-log ingestion) ----------------------------------------
#: 절대 상한. 텍스트 필드는 Unity ChatRequest 한도(4096)와 정합.
_TELEMETRY_TEXT_MAX = 4096
_TELEMETRY_SHORT_MAX = 256
_TELEMETRY_PROGRESS_MAX = 1024
_TELEMETRY_COUNT_MAX = 1_000_000


class TelemetryEvent(BaseModel):
    """POST /telemetry 한 행. Unity ``PlayLogCsvColumns.Ordered``와 1:1 대응."""

    model_config = ConfigDict(extra="ignore")

    session_id: str = Field(..., min_length=1, max_length=_TELEMETRY_SHORT_MAX)
    anonymous_player_id: str = Field(default="", max_length=_TELEMETRY_SHORT_MAX)
    scene_name: str = Field(default="", max_length=_TELEMETRY_SHORT_MAX)
    puzzle_id: str = Field(default="", max_length=_TELEMETRY_SHORT_MAX)
    event_time: str = Field(default="", max_length=64)
    event_type: str = Field(..., min_length=1, max_length=64)
    user_message: str = Field(default="", max_length=_TELEMETRY_TEXT_MAX)
    bot_response: str = Field(default="", max_length=_TELEMETRY_TEXT_MAX)
    hint_level: str = Field(default="", max_length=64)
    progress_state: str = Field(default="", max_length=_TELEMETRY_PROGRESS_MAX)
    time_since_scene_start: float = Field(default=0.0, ge=0.0)
    attempt_count: int = Field(default=0, ge=0, le=_TELEMETRY_COUNT_MAX)
    wrong_action_count: int = Field(default=0, ge=0, le=_TELEMETRY_COUNT_MAX)
    repeated_question_count: int = Field(default=0, ge=0, le=_TELEMETRY_COUNT_MAX)
    solved: bool = False


class TelemetryIngestRequest(BaseModel):
    """POST /telemetry 본문. 단건도 ``events`` 1개 배열로 보낸다."""

    model_config = ConfigDict(extra="ignore")

    events: list[TelemetryEvent] = Field(..., min_length=1)

    @model_validator(mode="after")
    def _enforce_batch_cap(self) -> "TelemetryIngestRequest":
        # 단일 진실원: 배치 상한은 Settings.telemetry_max_batch 한 곳에서만 관리.
        from config import get_settings

        max_batch = get_settings().telemetry_max_batch
        if len(self.events) > max_batch:
            raise ValueError(
                f"events exceeds telemetry_max_batch ({len(self.events)} > {max_batch})",
            )
        return self
