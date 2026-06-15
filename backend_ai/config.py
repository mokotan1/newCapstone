from __future__ import annotations

import os
from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

# 저장소 클론 후 실행 위치(cwd)와 무관하게 backend_ai/.env 를 읽는다.
_BACKEND_DIR = Path(__file__).resolve().parent
_ENV_PATH = _BACKEND_DIR / ".env"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=_ENV_PATH if _ENV_PATH.is_file() else None,
        env_file_encoding="utf-8",
        extra="ignore",
    )

    groq_api_key: str = ""
    google_api_key: str = ""
    chat_api_token: str = ""

    default_model_groq: str = "llama-3.3-70b-versatile"
    default_model_gemini: str = "gemini-2.0-flash"

    default_temperature: float = 0.7
    max_tokens: int = 512
    #: 모든 경로에 적용되는 프로바이더 토큰 상한(서버 강제 하드 캡).
    max_tokens_hard_cap: int = 4096
    #: Tutor ``rag_profile`` 요청에만 적용(짧은 대사·툴 호출 위주). 전역 max_tokens와 min.
    tutor_chat_max_tokens: int = 384

    # Untrusted text bounds (defense plan §4.2)
    chat_max_prompt_chars: int = 4096
    chat_max_client_system_chars: int = 16000
    chat_max_external_document_chars: int = 12000

    # Rate limit (§3.2). ``REDIS_URL`` unset → in-process window (단일 워커 전용).
    redis_url: str = ""
    rate_limit_enabled: bool = True
    rate_limit_ip_per_minute: int = 60
    rate_limit_user_per_minute: int = 30

    # Tutor RAG / quiz bank (paths relative to backend_ai/)
    tutor_rag_corpus_dir: str = "data/tutor_rag"
    tutor_quiz_csv_path: str = "data/tutor_quiz/quiz_bank.csv"
    tutor_rag_index_path: str = "data/tutor_rag_index.json"
    tutor_embedding_model: str = "models/text-embedding-004"
    tutor_rag_top_k: int = 5
    tutor_rag_max_context_chars: int = 6000
    tutor_grade_fuzzy_ratio: float = 0.82
    tutor_grade_fuzzy_max_len: int = 24


@lru_cache
def get_settings() -> Settings:
    # Let pydantic load .env (GROQ_API_KEY, GOOGLE_API_KEY). Legacy env name for Groq only if still empty.
    s = Settings()
    if not s.groq_api_key:
        legacy = os.getenv("capstone", "")
        if legacy:
            s = s.model_copy(update={"groq_api_key": legacy})
    return s
