"""Policy case review for design AC10-AC14 (static, not Unity behavior)."""

from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def _policy() -> str:
    return (ROOT / ".harness/unity-policy.md").read_text(encoding="utf-8")


def test_spec_work_requires_requirement_mapping_and_cannot_verify_with_gaps() -> None:
    text = _policy()
    assert "요구사항 대응표" in text
    assert "누락된 요구" in text
    assert "verified" in text


def test_structure_change_needs_recorded_decision_before_dependent_edits() -> None:
    text = _policy()
    assert "구조 변경" in text
    assert "사용자 결정" in text


def test_button_and_layout_edits_must_change_assets_not_runtime_fixups() -> None:
    text = _policy()
    assert "런타임 보정" in text
    assert "기존 에셋" in text


def test_one_shot_editor_automation_must_survive_reload() -> None:
    text = _policy()
    assert "재로드" in text
    assert "일회성" in text or "일회성 Editor" in text


def test_spec_and_quality_reviews_are_distinct_checks() -> None:
    text = _policy()
    assert "명세 리뷰" in text
    assert "품질 리뷰" in text
