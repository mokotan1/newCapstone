from __future__ import annotations

import json
from pathlib import Path

import pytest

from autorun.scenario_runner import (
    ScenarioRunner,
    ScenarioValidationError,
    discover_scenarios,
    load_scenario,
    run_scenarios,
)


class RecordingGateway:
    def __init__(self, failing_target: str | None = None) -> None:
        self.calls: list[tuple[str, str, str]] = []
        self.failing_target = failing_target

    def invoke(self, family: str, name: str, target: str) -> dict[str, object]:
        self.calls.append((family, name, target))
        if target == self.failing_target:
            return {"code": "InvalidTarget", "target": target}
        return {"code": "Ok", "target": target}


class AtomicGateway(RecordingGateway):
    def __init__(self, evidence_path: Path | None = None) -> None:
        super().__init__()
        self.evidence_path = evidence_path

    def run_scenario(self, scenario_id: str, timeout_ms: int = 120000) -> dict[str, object]:
        return {"code": "Ok", "data": {"scenarioId": scenario_id, "outcomeCode": "Passed", "timeoutMs": timeout_ms}, "evidenceRunDirectoryPath": str(self.evidence_path) if self.evidence_path else ""}


def _write_evidence(path: Path, verdict: str = "Pass", step_ids: list[str] | None = None) -> None:
    path.mkdir(parents=True)
    (path / "manifest.json").write_text(json.dumps({
        "Verdict": verdict,
        "RunDirectoryName": path.name,
        "EndedAtUtc": "2026-09-28T10:00:00Z",
        "AssertionPassedCount": 1,
        "ScreenshotCount": 1,
        "ConsoleRecorded": True,
        "ReportFileName": "report.md",
        "EventsFileName": "events.jsonl",
    }), encoding="utf-8")
    (path / "report.md").write_text("# evidence", encoding="utf-8")
    events = []
    for step_id in step_ids or ("capture",):
        events.append(
            {
                "Type": "CommandResult",
                "CommandId": step_id,
                "Code": "Success",
                "Data": {
                    "stepId": step_id,
                    "executionDispatch": "capability-api",
                    "capabilityId": step_id,
                    "csharpDispatch": "TestCapability.Handler",
                },
            }
        )
    (path / "events.jsonl").write_text(
        "".join(json.dumps(item, sort_keys=True) + "\n" for item in events),
        encoding="utf-8",
    )


def _write(path: Path, payload: dict[str, object]) -> Path:
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def test_runner_executes_both_supported_scenario_command_shapes(tmp_path: Path) -> None:
    path = _write(
        tmp_path / "scenario.json",
        {
            "schemaVersion": 1,
            "id": "test.scene",
            "scene": "TestScene",
            "steps": [
                {"id": "pointer", "command": "interaction.pointer", "target": "scene.target"},
                {"id": "capture", "family": "evidence", "name": "capture"},
            ],
        },
    )
    scenario = load_scenario(path)
    gateway = RecordingGateway()
    summary = ScenarioRunner(gateway, tmp_path / "run").run(scenario)
    assert summary["verdict"] == "PASS"
    assert gateway.calls == [
        ("interaction", "pointer", "scene.target"),
        ("evidence", "capture", ""),
    ]
    assert (tmp_path / "run" / "events.jsonl").is_file()
    assert (tmp_path / "run" / "report.md").read_text(encoding="utf-8").find("PASS") >= 0


def test_runner_stops_on_failed_gateway_response(tmp_path: Path) -> None:
    path = _write(
        tmp_path / "scenario.json",
        {
            "schemaVersion": 1,
            "id": "test.failure",
            "scene": "TestScene",
            "steps": [
                {"id": "bad", "command": "interaction.pointer", "target": "missing"},
                {"id": "never", "command": "evidence.capture"},
            ],
        },
    )
    summary = ScenarioRunner(RecordingGateway("missing"), tmp_path / "run").run(load_scenario(path))
    assert summary["verdict"] == "FAIL"
    assert summary["executed_step_ids"] == []


def test_invalid_scenario_and_exact_discovery(tmp_path: Path) -> None:
    _write(
        tmp_path / "valid.json",
        {"schemaVersion": 1, "id": "wanted", "scene": "Scene", "steps": [{"id": "x", "command": "evidence.capture"}]},
    )
    _write(
        tmp_path / "invalid.json",
        {"schemaVersion": 2, "id": "ignored", "scene": "Scene", "steps": [{"id": "x", "command": "evidence.capture"}]},
    )
    assert [item.scenario_id for item in discover_scenarios(tmp_path, ["wanted"])] == ["wanted"]
    with pytest.raises(ScenarioValidationError, match="schemaVersion"):
        load_scenario(tmp_path / "invalid.json")


def test_run_scenarios_aggregates_selected_cases(tmp_path: Path) -> None:
    for scenario_id in ("b.case", "a.case"):
        _write(
            tmp_path / f"{scenario_id}.json",
            {"schemaVersion": 1, "id": scenario_id, "scene": "Scene", "steps": [{"id": "capture", "command": "evidence.capture"}]},
        )
    summary = run_scenarios(RecordingGateway(), tmp_path, tmp_path / "run", ["a.case", "b.case"])
    assert summary["verdict"] == "PASS"
    assert summary["scenario_ids"] == ["a.case", "b.case"]


def test_run_scenarios_prefers_atomic_unity_qa_run(tmp_path: Path) -> None:
    path = _write(
        tmp_path / "scenario.json",
        {"schemaVersion": 1, "id": "atomic.case", "scene": "Scene", "steps": [{"id": "x", "command": "evidence.capture"}]},
    )
    evidence = tmp_path / "docs" / "qa" / "runs" / "evidence"
    _write_evidence(evidence, step_ids=["x"])
    summary = run_scenarios(AtomicGateway(evidence), tmp_path, tmp_path / "run", ["atomic.case"])
    assert summary["verdict"] == "PASS"
    assert summary["cases"][0]["gateway"] == "qa_run"
    assert summary["cases"][0]["evidence"]["ok"] is True


def test_atomic_run_without_valid_evidence_is_blocked(tmp_path: Path) -> None:
    path = _write(
        tmp_path / "scenario.json",
        {"schemaVersion": 1, "id": "atomic.missing-evidence", "scene": "Scene", "steps": [{"id": "x", "command": "evidence.capture"}]},
    )
    summary = run_scenarios(AtomicGateway(), tmp_path, tmp_path / "run", ["atomic.missing-evidence"])
    assert summary["verdict"] == "BLOCKED"


def test_room_scenarios_use_room_id_and_unknown_response_is_not_success(tmp_path: Path) -> None:
    path = _write(
        tmp_path / "room.json",
        {
            "schemaVersion": 1,
            "id": "room.case",
            "roomId": "kitchen",
            "tier": "smoke",
            "steps": [{"id": "probe", "family": "interaction", "name": "invoke", "targetId": "kitchen.probe"}],
        },
    )
    scenario = load_scenario(path)
    assert scenario.scene == "kitchen"
    summary = ScenarioRunner(type("UnknownGateway", (), {"invoke": lambda *_args: {"code": "Mystery"}})(), tmp_path / "run").run(scenario)
    assert summary["verdict"] == "FAIL"


def test_empty_discovery_is_an_error(tmp_path: Path) -> None:
    with pytest.raises(ScenarioValidationError, match="no executable"):
        discover_scenarios(tmp_path)
