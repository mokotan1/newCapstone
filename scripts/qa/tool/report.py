"""JSON and Markdown reports from the same normalized run (design AC18)."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

from scripts.qa.autorun.report import sanitize_for_report


def build_report(run: Mapping[str, Any]) -> tuple[dict[str, Any], str]:
    """JSON과 Markdown 보고서를 같은 판정·제외·복원·리뷰 값으로 만든다."""
    payload = sanitize_for_report(dict(run))
    if not isinstance(payload, dict):
        payload = dict(run)

    lines = [
        f"# QA Tool Report — {payload.get('runId', 'unknown')}",
        "",
        f"- Plan: `{payload.get('planId', 'unknown')}`",
        f"- Run verdict: `{payload.get('runVerdict', 'NOT_RUN')}`",
        f"- Cleanup: `{payload.get('cleanupStatus', 'unknown')}`",
    ]
    review = payload.get("review") if isinstance(payload.get("review"), dict) else {}
    lines.append(f"- Review: `{review.get('status', 'missing')}`")
    counts = payload.get("counts") if isinstance(payload.get("counts"), dict) else {}
    if counts:
        lines.append(
            "- Counts: "
            f"required={counts.get('required', 0)} "
            f"executed={counts.get('executed', 0)} "
            f"passed={counts.get('passed', 0)} "
            f"failed={counts.get('failed', 0)} "
            f"blocked={counts.get('blocked', 0)} "
            f"notRun={counts.get('notRun', 0)} "
            f"excluded={counts.get('excluded', 0)}"
        )
    lines.append("")
    lines.append("## Cases")
    cases = payload.get("cases") if isinstance(payload.get("cases"), list) else []
    for case in cases:
        if not isinstance(case, dict):
            continue
        scenario_id = case.get("scenarioId", "unknown")
        verdict = case.get("scenarioVerdict", "NOT_RUN")
        lines.append(f"- `{scenario_id}`: `{verdict}`")
        reasons = case.get("reasonCodes") or []
        if reasons:
            lines.append(f"  - Reasons: {', '.join(str(item) for item in reasons)}")
        evidence_paths = case.get("evidencePaths") or []
        for path in evidence_paths:
            lines.append(f"  - Evidence: `{path}`")
        if case.get("excluded"):
            lines.append(f"  - Excluded: `{case.get('exclusionReason', '')}`")
    lines.append("")
    return payload, "\n".join(lines)
