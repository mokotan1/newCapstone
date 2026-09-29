from __future__ import annotations

import json
from pathlib import Path

import pytest

from autorun.scenario_runner import validate_unity_scenario_evidence


def _write_manifest(path: Path, *, run_dir_name: str, verdict: str = "Pass") -> None:
    path.mkdir(parents=True, exist_ok=True)
    (path / "manifest.json").write_text(
        json.dumps(
            {
                "Verdict": verdict,
                "RunDirectoryName": run_dir_name,
                "EndedAtUtc": "2026-09-28T10:00:00Z",
                "AssertionPassedCount": 1,
                "ScreenshotCount": 1,
                "ConsoleRecorded": True,
                "ReportFileName": "report.md",
                "EventsFileName": "events.jsonl",
            }
        ),
        encoding="utf-8",
    )
    (path / "report.md").write_text("# evidence\n", encoding="utf-8")


def _write_events(path: Path, lines: list[dict[str, object]]) -> None:
    path.write_text(
        "".join(json.dumps(line, sort_keys=True) + "\n" for line in lines),
        encoding="utf-8",
    )


def test_validate_evidence_requires_real_input_marker_for_pointer_step(tmp_path: Path) -> None:
    run_dir = tmp_path / "docs" / "qa" / "runs" / "run-real-input"
    _write_manifest(run_dir, run_dir_name=run_dir.name)
    _write_events(
        run_dir / "events.jsonl",
        [
            {
                "Type": "CommandResult",
                "CommandId": "click-faucet",
                "Code": "Success",
                "Data": {
                    "stepId": "click-faucet",
                    "executionDispatch": "api-only",
                    "inputMode": "Api",
                },
            }
        ],
    )
    scenario = {
        "steps": [
            {
                "id": "click-faucet",
                "family": "interaction",
                "name": "pointer",
                "targetId": "kitchen.sink.faucet",
                "parameters": {"mode": "realInput"},
            }
        ]
    }
    result = validate_unity_scenario_evidence(str(run_dir), scenario)
    assert result["ok"] is False
    assert result["reason"] == "missing-real-input-evidence"


def test_validate_evidence_accepts_real_input_and_required_steps(tmp_path: Path) -> None:
    run_dir = tmp_path / "docs" / "qa" / "runs" / "run-ok"
    _write_manifest(run_dir, run_dir_name=run_dir.name)
    _write_events(
        run_dir / "events.jsonl",
        [
            {
                "Type": "CommandResult",
                "CommandId": "click-faucet",
                "Code": "Success",
                "Data": {
                    "stepId": "click-faucet",
                    "executionDispatch": "real-input",
                    "inputMode": "RealInput",
                    "targetId": "kitchen.sink.faucet",
                },
            },
            {
                "Type": "CommandResult",
                "CommandId": "assert-exit",
                "Code": "Success",
                "Data": {
                    "stepId": "assert-exit",
                    "executionDispatch": "capability-api",
                    "capabilityId": "kitchen.exit.assert",
                    "csharpDispatch": "KitchenInteractionController.AssertExit",
                },
            },
        ],
    )
    scenario = {
        "steps": [
            {
                "id": "click-faucet",
                "family": "interaction",
                "name": "pointer",
                "targetId": "kitchen.sink.faucet",
                "parameters": {"mode": "realInput"},
            },
            {
                "id": "assert-exit",
                "family": "interaction",
                "name": "invoke",
                "targetId": "kitchen.exit.assert",
            },
        ]
    }
    result = validate_unity_scenario_evidence(str(run_dir), scenario)
    assert result["ok"] is True


def test_validate_evidence_blocks_when_required_step_missing(tmp_path: Path) -> None:
    run_dir = tmp_path / "docs" / "qa" / "runs" / "run-missing-step"
    _write_manifest(run_dir, run_dir_name=run_dir.name)
    _write_events(
        run_dir / "events.jsonl",
        [
            {
                "Type": "CommandResult",
                "CommandId": "click-faucet",
                "Code": "Success",
                "Data": {
                    "stepId": "click-faucet",
                    "executionDispatch": "real-input",
                    "inputMode": "RealInput",
                },
            }
        ],
    )
    scenario = {
        "steps": [
            {
                "id": "click-faucet",
                "family": "interaction",
                "name": "pointer",
                "targetId": "kitchen.sink.faucet",
                "parameters": {"mode": "realInput"},
            },
            {"id": "assert-exit", "family": "interaction", "name": "invoke", "targetId": "kitchen.exit.assert"},
        ]
    }
    result = validate_unity_scenario_evidence(str(run_dir), scenario)
    assert result["ok"] is False
    assert result["reason"] == "missing-required-step-evidence"


def test_validate_evidence_blocks_invoke_without_csharp_dispatch(tmp_path: Path) -> None:
    run_dir = tmp_path / "docs" / "qa" / "runs" / "run-no-csharp"
    _write_manifest(run_dir, run_dir_name=run_dir.name)
    _write_events(
        run_dir / "events.jsonl",
        [
            {
                "Type": "CommandResult",
                "CommandId": "assert-exit",
                "Code": "Success",
                "Data": {
                    "stepId": "assert-exit",
                    "executionDispatch": "capability-api",
                    "capabilityId": "kitchen.exit.assert",
                },
            }
        ],
    )
    scenario = {
        "steps": [
            {
                "id": "assert-exit",
                "family": "interaction",
                "name": "invoke",
                "targetId": "kitchen.exit.assert",
            }
        ]
    }
    result = validate_unity_scenario_evidence(str(run_dir), scenario)
    assert result["ok"] is False
    assert result["reason"] == "missing-csharp-dispatch-evidence"


def test_validate_evidence_rejects_stale_manifest_directory_name(tmp_path: Path) -> None:
    run_dir = tmp_path / "docs" / "qa" / "runs" / "run-stale"
    _write_manifest(run_dir, run_dir_name="different-directory")
    result = validate_unity_scenario_evidence(
        str(run_dir),
        {"steps": [{"id": "x", "command": "evidence.capture"}]},
    )
    assert result["ok"] is False
    assert result["reason"] == "stale-evidence-manifest"
