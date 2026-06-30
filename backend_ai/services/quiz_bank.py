from __future__ import annotations

import csv
import logging
from dataclasses import dataclass
from pathlib import Path

logger = logging.getLogger(__name__)


@dataclass(frozen=True)
class QuizRow:
    question_id: str
    question_ko: str
    acceptable_answers: tuple[str, ...]
    reference_snippet: str
    difficulty: str
    tags: str


def _split_acceptable(raw: str) -> tuple[str, ...]:
    parts = [p.strip() for p in raw.split("|") if p.strip()]
    return tuple(parts)


class QuizBank:
    def __init__(self, rows: dict[str, QuizRow]) -> None:
        self._rows = rows

    @classmethod
    def load(cls, path: Path) -> QuizBank:
        if not path.is_file():
            logger.warning("Quiz bank CSV not found: %s - using empty bank", path)
            return cls({})

        rows: dict[str, QuizRow] = {}
        with path.open(encoding="utf-8-sig", newline="") as f:
            reader = csv.DictReader(f)
            for r in reader:
                qid = (r.get("question_id") or "").strip()
                if not qid:
                    continue
                rows[qid] = QuizRow(
                    question_id=qid,
                    question_ko=(r.get("question_ko") or "").strip(),
                    acceptable_answers=_split_acceptable(r.get("acceptable_answers") or ""),
                    reference_snippet=(r.get("reference_snippet") or "").strip(),
                    difficulty=(r.get("difficulty") or "").strip(),
                    tags=(r.get("tags") or "").strip(),
                )
        logger.info("Loaded %d quiz bank rows from %s", len(rows), path)
        return cls(rows)

    def get(self, question_id: str) -> QuizRow | None:
        return self._rows.get(question_id.strip())

    def format_bank_context_block(self, row: QuizRow) -> str:
        """Injected into tutor system prompt: question text must match bank; do not reveal answers."""
        return (
            "[튜터 퀴즈 출제 컨텍스트]\n"
            f"- question_id: {row.question_id}\n"
            f"- 질문 원문(변경 금지): {row.question_ko}\n"
            f"- 참고 힌트(정답처럼 읽지 말 것): {row.reference_snippet}\n"
            "- 출제할 때는 질문 원문 한 번만 말합니다.\n"
            "- 정답, 정답 후보, 채점 기준은 절대 말하지 않습니다.\n"
            "- 직전 대사에 같은 질문이 이미 있으면 반복하지 말고 짧은 반응 뒤 다음 질문만 말합니다."
        )
