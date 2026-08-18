"""Quiz bank locale selection and Korean fallback for empty cells."""

from __future__ import annotations

from pathlib import Path

from services.quiz_bank import QuizBank


_NEW_HEADER = (
    "question_id,question_ko,question_ja,question_en,"
    "acceptable_answers_ko,acceptable_answers_ja,acceptable_answers_en,"
    "reference_snippet_ko,reference_snippet_ja,reference_snippet_en,"
    "difficulty,tags\n"
)


def test_format_bank_context_block_uses_en_question(tmp_path: Path) -> None:
    p = tmp_path / "bank.csv"
    p.write_text(
        _NEW_HEADER
        + "Q1,한국어질문?,日本語の質問?,Who defeated the giant?,"
        "골리앗,ゴリアテ,Goliath|Goliath the giant,"
        "한글힌트,日本語ヒント,Think of David and the giant.,"
        "1,ot\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(p)
    row = bank.get("Q1")
    assert row is not None
    block = bank.format_bank_context_block(row, locale="en")
    assert "Who defeated the giant?" in block
    assert "한국어질문?" not in block
    assert "Think of David and the giant." in block
    assert "Goliath" not in block  # answers must not leak


def test_format_bank_context_block_en_chrome_has_no_hangul_headers(
    tmp_path: Path,
) -> None:
    """EN locale must localize chrome labels; question_id key stays English."""
    p = tmp_path / "bank.csv"
    p.write_text(
        _NEW_HEADER
        + "Q1,한국어질문?,日本語の質問?,Who defeated the giant?,"
        "골리앗,ゴリアテ,Goliath,"
        "한글힌트,日本語ヒント,Think of David and the giant.,"
        "1,ot\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(p)
    row = bank.get("Q1")
    assert row is not None
    block = bank.format_bank_context_block(row, locale="en")
    assert "question_id:" in block
    assert "Q1" in block
    # Hangul chrome from the Korean template must not appear
    assert "튜터 퀴즈" not in block
    assert "질문 원문" not in block
    assert "참고 힌트" not in block
    assert "출제할 때" not in block
    # English chrome markers
    lower = block.lower()
    assert "quiz" in lower or "tutor" in lower
    assert "question" in lower
    assert "Who defeated the giant?" in block


def test_empty_en_question_falls_back_to_ko(tmp_path: Path) -> None:
    p = tmp_path / "bank.csv"
    # Columns: id, ko, ja, en, ans_ko, ans_ja, ans_en, snip_ko, snip_ja, snip_en, diff, tags
    p.write_text(
        _NEW_HEADER
        + "Q1,한국어질문,,,골리앗,,,한글힌트,,,1,ot\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(p)
    row = bank.get("Q1")
    assert row is not None
    assert row.question_for("en") == "한국어질문"
    assert row.acceptable_answers_for("en") == ("골리앗",)
    assert row.reference_snippet_for("en") == "한글힌트"
    block = bank.format_bank_context_block(row, locale="en")
    assert "한국어질문" in block


def test_loader_accepts_legacy_column_names(tmp_path: Path) -> None:
    p = tmp_path / "legacy.csv"
    p.write_text(
        "question_id,question_ko,acceptable_answers,reference_snippet,difficulty,tags\n"
        "Q9,옛질문?,정답|답,옛참고,1,t\n",
        encoding="utf-8",
    )
    bank = QuizBank.load(p)
    row = bank.get("Q9")
    assert row is not None
    assert row.question_ko == "옛질문?"
    assert row.acceptable_answers_for("ko") == ("정답", "답")
    assert row.reference_snippet_for("ko") == "옛참고"


def test_production_bank_has_en_question_for_q001() -> None:
    csv_path = Path(__file__).resolve().parents[1] / "data" / "tutor_quiz" / "quiz_bank.csv"
    bank = QuizBank.load(csv_path)
    row = bank.get("Q001")
    assert row is not None
    en_q = row.question_for("en")
    assert en_q
    assert en_q != row.question_ko
    assert "?" in en_q or en_q.endswith("?")
