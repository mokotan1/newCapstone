"""Report JSON and Markdown stay aligned (design AC18)."""

from __future__ import annotations

import json

from scripts.qa.tool.report import build_report


def test_json_and_markdown_agree_on_case_verdicts_and_exclusions() -> None:
    run = {
        "runId": "run-1",
        "planId": "hall-to-kitchen.v1",
        "runVerdict": "BLOCKED",
        "cleanupStatus": "restored",
        "review": {"status": "missing", "blocksVerified": True},
        "counts": {
            "required": 1,
            "executed": 1,
            "passed": 0,
            "failed": 0,
            "blocked": 1,
            "notRun": 0,
            "excluded": 1,
        },
        "cases": [
            {
                "scenarioId": "qa.tool.hall-to-kitchen",
                "scenarioVerdict": "BLOCKED",
                "reasonCodes": ["missing-required-step"],
                "evidencePaths": ["attempts/a1/events.jsonl"],
                "required": True,
                "excluded": False,
            },
            {
                "scenarioId": "player-input.hall-to-kitchen",
                "scenarioVerdict": "NOT_RUN",
                "reasonCodes": ["excluded"],
                "evidencePaths": [],
                "required": False,
                "excluded": True,
                "exclusionReason": "phase-2-player-input",
            },
        ],
    }

    payload, markdown = build_report(run)
    assert payload["runVerdict"] == "BLOCKED"
    assert payload["cleanupStatus"] == "restored"
    assert payload["review"]["status"] == "missing"
    assert payload["counts"]["excluded"] == 1
    assert payload["counts"]["blocked"] == 1
    hall = payload["cases"][0]
    assert hall["scenarioVerdict"] == "BLOCKED"
    assert "attempts/a1/events.jsonl" in hall["evidencePaths"]

    assert "BLOCKED" in markdown
    assert "qa.tool.hall-to-kitchen" in markdown
    assert "missing-required-step" in markdown
    assert "player-input.hall-to-kitchen" in markdown
    assert "NOT_RUN" in markdown
    assert "phase-2-player-input" in markdown
    assert "restored" in markdown
    assert "review" in markdown.lower() or "missing" in markdown
    json.dumps(payload)
