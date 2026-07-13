from __future__ import annotations

from pathlib import Path

import pytest

from config import Settings
from models.requests import TutorGradeRequest
from services.quiz_bank import QuizBank
from services.tutor_grade import grade_tutor_answer


@pytest.fixture
def bank(tmp_path: Path) -> QuizBank:
    p = tmp_path / "b.csv"
    p.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "Q002,거인?,巨人は?,Who was the giant?,"
        "골리앗|골리엇,ゴリアテ,Goliath|Goliath the Philistine,"
        "다윗과 골리앗,ダビデとゴリアテ,David and Goliath,,\n",
        encoding="utf-8",
    )
    return QuizBank.load(p)


def test_grade_correct_goliath(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q002", user_answer="골리앗", correct_count_before=1),
        bank,
        Settings(),
    )
    assert r.is_correct is True
    assert r.unknown_question is False
    assert "다윗" in r.reference_snippet
    assert r.quiz_complete_after is False


def test_grade_wrong(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q002", user_answer="삼손", correct_count_before=1),
        bank,
        Settings(),
    )
    assert r.is_correct is False
    assert r.quiz_complete_after is False


def test_quiz_complete_after_fifth(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q002", user_answer="골리앗", correct_count_before=4, quiz_target=5),
        bank,
        Settings(),
    )
    assert r.is_correct is True
    assert r.quiz_complete_after is True


def test_unknown_question(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q999", user_answer="x", correct_count_before=0),
        bank,
        Settings(),
    )
    assert r.unknown_question is True
    assert r.is_correct is False


def test_empty_user_answer_is_wrong(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q002", user_answer="", correct_count_before=0),
        bank,
        Settings(),
    )
    assert r.is_correct is False
    assert r.unknown_question is False


def test_high_correct_count_before_validates(bank: QuizBank) -> None:
    """Fungus Integer가 비정상적으로 크면 예전 스키마(le=100)에서 422가 났음."""
    req = TutorGradeRequest(question_id="Q002", user_answer="골리앗", correct_count_before=500)
    assert req.correct_count_before == 500
    r = grade_tutor_answer(req, bank, Settings())
    assert r.is_correct is True


def test_grade_request_locale_defaults_ko() -> None:
    req = TutorGradeRequest(question_id="Q002", user_answer="x")
    assert req.locale == "ko"


def test_grade_request_locale_normalizes_en_us() -> None:
    req = TutorGradeRequest(question_id="Q002", user_answer="x", locale="en-US")
    assert req.locale == "en"


def test_grade_correct_english_answer(bank: QuizBank) -> None:
    r = grade_tutor_answer(
        TutorGradeRequest(
            question_id="Q002",
            user_answer="Goliath",
            correct_count_before=0,
            locale="en",
        ),
        bank,
        Settings(),
    )
    assert r.is_correct is True
    assert "David" in r.reference_snippet


def test_grade_korean_answer_wrong_when_locale_en(bank: QuizBank) -> None:
    """EN grading uses EN aliases only (independent columns; no KO alias mix)."""
    r = grade_tutor_answer(
        TutorGradeRequest(
            question_id="Q002",
            user_answer="골리앗",
            correct_count_before=0,
            locale="en",
        ),
        bank,
        Settings(),
    )
    assert r.is_correct is False


def test_grade_empty_en_answers_fall_back_to_ko(tmp_path: Path) -> None:
    p = tmp_path / "fb.csv"
    p.write_text(
        "question_id,question_ko,question_ja,question_en,"
        "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
        "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
        "difficulty,tags\n"
        "Q1,질문,,,정답,,,힌트KO,,,1,t\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(p)
    r = grade_tutor_answer(
        TutorGradeRequest(question_id="Q1", user_answer="정답", locale="en"),
        bank,
        Settings(),
    )
    assert r.is_correct is True
    assert r.reference_snippet == "힌트KO"
