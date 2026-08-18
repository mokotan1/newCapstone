from __future__ import annotations

import csv
import logging
from dataclasses import dataclass
from pathlib import Path

from services.locale_support import normalize_locale

logger = logging.getLogger(__name__)

# Locale chrome only — question_id key and bank field selectors stay invariant.
_BANK_CONTEXT_CHROME: dict[str, dict[str, str]] = {
    "ko": {
        "header": "[튜터 퀴즈 출제 컨텍스트]",
        "question_label": "질문 원문(변경 금지)",
        "hint_label": "참고 힌트(정답처럼 읽지 말 것)",
        "rule_once": "출제할 때는 질문 원문 한 번만 말합니다.",
        "rule_no_answers": "정답, 정답 후보, 채점 기준은 절대 말하지 않습니다.",
        "rule_no_repeat": (
            "직전 대사에 같은 질문이 이미 있으면 반복하지 말고 "
            "짧은 반응 뒤 다음 질문만 말합니다."
        ),
    },
    "ja": {
        "header": "[チュータークイズ出題コンテキスト]",
        "question_label": "質問原文（変更禁止）",
        "hint_label": "参考ヒント（正解として読まないこと）",
        "rule_once": "出題時は質問原文を一度だけ述べます。",
        "rule_no_answers": "正解、正解候補、採点基準は絶対に言いません。",
        "rule_no_repeat": (
            "直前の台詞に同じ質問がある場合は繰り返さず、"
            "短い反応の後に次の質問だけ述べます。"
        ),
    },
    "en": {
        "header": "[Tutor quiz context]",
        "question_label": "Question text (do not change)",
        "hint_label": "Reference hint (do not read as the answer)",
        "rule_once": "When asking, state the question text only once.",
        "rule_no_answers": (
            "Never reveal the answer, answer candidates, or grading criteria."
        ),
        "rule_no_repeat": (
            "If the previous line already asked the same question, do not repeat it; "
            "give a short reaction then ask only the next question."
        ),
    },
}


def _split_acceptable(raw: str) -> tuple[str, ...]:
    parts = [p.strip() for p in raw.split("|") if p.strip()]
    return tuple(parts)


def _pick_text(primary: str, fallback: str) -> str:
    primary = (primary or "").strip()
    if primary:
        return primary
    return (fallback or "").strip()


def _pick_answers(primary: tuple[str, ...], fallback: tuple[str, ...]) -> tuple[str, ...]:
    if primary:
        return primary
    return fallback


@dataclass(frozen=True)
class QuizRow:
    question_id: str
    question_ko: str
    question_ja: str
    question_en: str
    acceptable_answers_ko: tuple[str, ...]
    acceptable_answers_ja: tuple[str, ...]
    acceptable_answers_en: tuple[str, ...]
    reference_snippet_ko: str
    reference_snippet_ja: str
    reference_snippet_en: str
    difficulty: str
    tags: str

    def question_for(self, locale: str) -> str:
        key = normalize_locale(locale)
        if key == "ja":
            return _pick_text(self.question_ja, self.question_ko)
        if key == "en":
            return _pick_text(self.question_en, self.question_ko)
        return self.question_ko

    def acceptable_answers_for(self, locale: str) -> tuple[str, ...]:
        key = normalize_locale(locale)
        if key == "ja":
            return _pick_answers(self.acceptable_answers_ja, self.acceptable_answers_ko)
        if key == "en":
            return _pick_answers(self.acceptable_answers_en, self.acceptable_answers_ko)
        return self.acceptable_answers_ko

    def reference_snippet_for(self, locale: str) -> str:
        key = normalize_locale(locale)
        if key == "ja":
            return _pick_text(self.reference_snippet_ja, self.reference_snippet_ko)
        if key == "en":
            return _pick_text(self.reference_snippet_en, self.reference_snippet_ko)
        return self.reference_snippet_ko

    # Backward-compatible aliases (Korean).
    @property
    def acceptable_answers(self) -> tuple[str, ...]:
        return self.acceptable_answers_ko

    @property
    def reference_snippet(self) -> str:
        return self.reference_snippet_ko


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
                answers_ko = _split_acceptable(
                    (r.get("acceptable_answers_ko") or r.get("acceptable_answers") or "")
                )
                snippet_ko = (
                    r.get("reference_snippet_ko") or r.get("reference_snippet") or ""
                ).strip()
                rows[qid] = QuizRow(
                    question_id=qid,
                    question_ko=(r.get("question_ko") or "").strip(),
                    question_ja=(r.get("question_ja") or "").strip(),
                    question_en=(r.get("question_en") or "").strip(),
                    acceptable_answers_ko=answers_ko,
                    acceptable_answers_ja=_split_acceptable(
                        r.get("acceptable_answers_ja") or ""
                    ),
                    acceptable_answers_en=_split_acceptable(
                        r.get("acceptable_answers_en") or ""
                    ),
                    reference_snippet_ko=snippet_ko,
                    reference_snippet_ja=(r.get("reference_snippet_ja") or "").strip(),
                    reference_snippet_en=(r.get("reference_snippet_en") or "").strip(),
                    difficulty=(r.get("difficulty") or "").strip(),
                    tags=(r.get("tags") or "").strip(),
                )
        logger.info("Loaded %d quiz bank rows from %s", len(rows), path)
        return cls(rows)

    def get(self, question_id: str) -> QuizRow | None:
        return self._rows.get(question_id.strip())

    def format_bank_context_block(self, row: QuizRow, locale: str = "ko") -> str:
        """Injected into tutor system prompt: question text must match bank; do not reveal answers."""
        key = normalize_locale(locale)
        chrome = _BANK_CONTEXT_CHROME[key]
        question = row.question_for(locale)
        snippet = row.reference_snippet_for(locale)
        return (
            f"{chrome['header']}\n"
            f"- question_id: {row.question_id}\n"
            f"- {chrome['question_label']}: {question}\n"
            f"- {chrome['hint_label']}: {snippet}\n"
            f"- {chrome['rule_once']}\n"
            f"- {chrome['rule_no_answers']}\n"
            f"- {chrome['rule_no_repeat']}"
        )
