from __future__ import annotations

from pathlib import Path

from services.quiz_bank import QuizBank
from services.quiz_validation import validate_quiz_bank_csv


def test_validate_good_csv(tmp_path: Path) -> None:
    p = tmp_path / "good.csv"
    p.write_text(
        "question_id,question_ko,acceptable_answers,reference_snippet,difficulty,tags\n"
        "A1,질문?,정답|답,참고,1,t\n",
        encoding="utf-8",
    )
    assert validate_quiz_bank_csv(p) == []


def test_validate_duplicate_id(tmp_path: Path) -> None:
    p = tmp_path / "bad.csv"
    p.write_text(
        "question_id,question_ko,acceptable_answers,reference_snippet\n"
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


def test_reference_snippets_do_not_reveal_acceptable_answers() -> None:
    bank = QuizBank.load(Path("data/tutor_quiz/quiz_bank.csv"))
    leaks = []
    for row in bank._rows.values():
        for answer in row.acceptable_answers:
            if answer and answer in row.reference_snippet:
                leaks.append((row.question_id, answer, row.reference_snippet))

    assert leaks == []
