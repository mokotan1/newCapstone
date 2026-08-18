from __future__ import annotations

from pathlib import Path

from services.quiz_bank import QuizBank
from services.quiz_validation import validate_quiz_bank_csv


def test_validate_good_csv(tmp_path: Path) -> None:
    p = tmp_path / "good.csv"
    p.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "A1,질문?,質問?,Question?,정답|답,答え,Answer,참고,参考,Hint,1,t\n",
        encoding="utf-8",
    )
    assert validate_quiz_bank_csv(p) == []


def test_validate_legacy_columns_still_ok(tmp_path: Path) -> None:
    p = tmp_path / "legacy.csv"
    p.write_text(
        "question_id,question_ko,acceptable_answers,reference_snippet,difficulty,tags\n"
        "A1,질문?,정답|답,참고,1,t\n",
        encoding="utf-8",
    )
    assert validate_quiz_bank_csv(p) == []


def test_validate_duplicate_id(tmp_path: Path) -> None:
    p = tmp_path / "bad.csv"
    p.write_text(
        "question_id,question_ko,acceptable_answers_ko,reference_snippet_ko\n"
        "X,q,a,r\n"
        "X,q2,a2,r2\n",
        encoding="utf-8",
    )
    errs = validate_quiz_bank_csv(p)
    assert any("Duplicate" in e for e in errs)


def test_validate_missing_column(tmp_path: Path) -> None:
    p = tmp_path / "bad2.csv"
    p.write_text("question_id,question_ko\nA1,q\n", encoding="utf-8")
    errs = validate_quiz_bank_csv(p)
    assert any("Missing required column" in e for e in errs)


def test_validate_ja_en_optional_when_ko_present(tmp_path: Path) -> None:
    p = tmp_path / "ko_only.csv"
    p.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en\n"
        "A1,질문?,,,정답,,,,,\n",
        encoding="utf-8",
    )
    assert validate_quiz_bank_csv(p) == []


def test_reference_snippets_do_not_reveal_acceptable_answers() -> None:
    csv_path = Path(__file__).resolve().parents[1] / "data" / "tutor_quiz" / "quiz_bank.csv"
    bank = QuizBank.load(csv_path)
    leaks = []
    for row in bank._rows.values():
        for locale in ("ko", "ja", "en"):
            snippet = row.reference_snippet_for(locale)
            for answer in row.acceptable_answers_for(locale):
                if answer and answer in snippet:
                    leaks.append((row.question_id, locale, answer, snippet))

    assert leaks == []
