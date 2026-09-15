"""Scenario and run verdicts (design AC09, AC17, §6)."""

from __future__ import annotations

from collections.abc import Mapping, Sequence
from typing import Any


def judge_scenario(record: Mapping[str, Any]) -> dict[str, Any]:
    """Judge one scenario. FAIL beats missing evidence; PASS requires every required check."""
    scenario_id = str(record.get("scenarioId") or "")
    if record.get("legacyManifest"):
        return {
            "scenarioId": scenario_id,
            "scenarioVerdict": "BLOCKED",
            "reasonCodes": ["legacy-schema"],
        }
    if not record.get("started"):
        return {
            "scenarioId": scenario_id,
            "scenarioVerdict": "NOT_RUN",
            "reasonCodes": ["not-started"],
        }

    reason_codes: list[str] = []
    assertions = record.get("assertions") or []
    if any(
        isinstance(item, Mapping) and item.get("verdict") == "FAIL"
        for item in assertions
    ):
        reason_codes.append("assertion-failed")

    required_steps = [str(item) for item in (record.get("requiredStepIds") or [])]
    executed_steps = {str(item) for item in (record.get("executedStepIds") or [])}
    if any(step_id not in executed_steps for step_id in required_steps):
        reason_codes.append("missing-required-step")

    required_layers = [str(item) for item in (record.get("requiredInputLayers") or [])]
    executed_layers = {str(item) for item in (record.get("executedInputLayers") or [])}
    if any(layer not in executed_layers for layer in required_layers):
        reason_codes.append("missing-input-layer")

    required_kinds = [str(item) for item in (record.get("requiredArtifactKinds") or [])]
    validated_kinds = {str(item) for item in (record.get("validatedArtifactKinds") or [])}
    if any(kind not in validated_kinds for kind in required_kinds):
        reason_codes.append("missing-required-evidence")

    if record.get("evidenceValid") is False:
        reason_codes.append("evidence-invalid")

    cleanup_status = record.get("cleanupStatus")
    if cleanup_status == "failed":
        reason_codes.append("cleanup-failed")
    elif cleanup_status not in {"restored", "not-applicable", None}:
        reason_codes.append("cleanup-incomplete")

    console = record.get("consoleClassification")
    if console == "new-related":
        reason_codes.append("console-new-related")
    elif console == "unclassified":
        reason_codes.append("console-unclassified")
    elif console == "missing":
        reason_codes.append("console-missing")

    if record.get("cancelled"):
        reason_codes.append("cancelled")

    if not assertions:
        reason_codes.append("missing-assertion")

    fail_codes = {"assertion-failed", "console-new-related"}
    if fail_codes.intersection(reason_codes):
        verdict = "FAIL"
    elif reason_codes:
        verdict = "BLOCKED"
    else:
        verdict = "PASS"

    return {
        "scenarioId": scenario_id,
        "scenarioVerdict": verdict,
        "reasonCodes": reason_codes,
    }


def aggregate_run(cases: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    """Aggregate required cases. Exclusions are counted and never shrink the required denominator."""
    required = [
        case for case in cases if case.get("required") and not case.get("excluded")
    ]
    excluded = [case for case in cases if case.get("excluded")]

    def _count(verdict: str) -> int:
        return sum(1 for case in required if case.get("scenarioVerdict") == verdict)

    passed = _count("PASS")
    failed = _count("FAIL")
    blocked = _count("BLOCKED")
    not_run = _count("NOT_RUN")
    executed = sum(
        1
        for case in required
        if case.get("scenarioVerdict") in {"PASS", "FAIL", "BLOCKED"}
    )

    if failed:
        run_verdict = "FAIL"
    elif blocked or not_run:
        run_verdict = "BLOCKED"
    elif required and passed == len(required):
        run_verdict = "PASS"
    else:
        run_verdict = "BLOCKED"

    return {
        "runVerdict": run_verdict,
        "counts": {
            "required": len(required),
            "executed": executed,
            "passed": passed,
            "failed": failed,
            "blocked": blocked,
            "notRun": not_run,
            "excluded": len(excluded),
        },
    }
