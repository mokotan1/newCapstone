"""Console delta classification (design AC11)."""

from __future__ import annotations

from scripts.qa.tool.console import classify_console_delta


def test_baseline_exception_is_not_new_related() -> None:
    result = classify_console_delta(
        baseline_fingerprints=["NullRef:BloodDripTitleDemo"],
        collected=True,
        entries=[
            {
                "fingerprint": "NullRef:BloodDripTitleDemo",
                "relatedness": "related",
            }
        ],
    )
    assert result["classification"] == "baseline"
    assert result["reasonCodes"] == []


def test_new_related_exception_is_fail_class() -> None:
    result = classify_console_delta(
        baseline_fingerprints=[],
        collected=True,
        entries=[
            {
                "fingerprint": "NullRef:HallQaAdapter",
                "relatedness": "related",
            }
        ],
    )
    assert result["classification"] == "new-related"
    assert "console-new-related" in result["reasonCodes"]


def test_unclassified_exception_is_blocked_class() -> None:
    result = classify_console_delta(
        baseline_fingerprints=[],
        collected=True,
        entries=[
            {
                "fingerprint": "Exception:Mystery",
                "relatedness": "unknown",
            }
        ],
    )
    assert result["classification"] == "unclassified"
    assert "console-unclassified" in result["reasonCodes"]


def test_missing_console_collection_is_blocked_class() -> None:
    result = classify_console_delta(
        baseline_fingerprints=[],
        collected=False,
        entries=[],
    )
    assert result["classification"] == "missing"
    assert "console-missing" in result["reasonCodes"]
