"""Execute versioned QA scenario JSON through the stable gateway contract.

The Unity Editor remains the owner of gameplay state.  This module only loads
and validates scenario data, dispatches one gateway command at a time, and
records the observed responses.  It deliberately does not invent targets or
fall back to hierarchy paths.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Protocol, Sequence

from .report import render_report, sanitize_for_report
from scripts.qa.tool.verdict import aggregate_run


class ScenarioGateway(Protocol):
    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        """Dispatch a documented QA command and return its structured result."""

    def run_scenario(self, scenario_id: str, timeout_ms: int = 120000) -> dict[str, Any]:
        """Execute the complete scenario inside Unity's QaCommandGateway."""


class ScenarioValidationError(ValueError):
    """Scenario cannot be executed without guessing at a command or target."""


@dataclass(frozen=True)
class Scenario:
    schema_version: int
    scenario_id: str
    scene: str
    payload: Mapping[str, Any]
    path: Path


def load_scenario(path: Path | str) -> Scenario:
    source = Path(path)
    try:
        payload = json.loads(source.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ScenarioValidationError(f"unable to read scenario {source}: {exc}") from exc
    if not isinstance(payload, dict):
        raise ScenarioValidationError("scenario JSON must be an object")
    if payload.get("schemaVersion") != 1:
        raise ScenarioValidationError("schemaVersion must be 1")
    scenario_id = payload.get("id")
    scene = payload.get("scene") or payload.get("roomId")
    steps = payload.get("steps")
    if not isinstance(scenario_id, str) or not scenario_id.strip():
        raise ScenarioValidationError("id must be a non-empty string")
    if not isinstance(scene, str) or not scene.strip():
        raise ScenarioValidationError("scene or roomId must be a non-empty string")
    if not isinstance(steps, list) or not steps:
        raise ScenarioValidationError("steps must be a non-empty list")
    seen: set[str] = set()
    for index, step in enumerate(steps):
        if not isinstance(step, dict):
            raise ScenarioValidationError(f"steps[{index}] must be an object")
        step_id = step.get("id", step.get("stepId"))
        if not isinstance(step_id, str) or not step_id.strip():
            raise ScenarioValidationError(f"steps[{index}].id must be non-empty")
        if step_id in seen:
            raise ScenarioValidationError(f"duplicate step id: {step_id}")
        seen.add(step_id)
        timeout = step.get("timeoutMs", 30000)
        if not isinstance(timeout, int) or timeout <= 0:
            raise ScenarioValidationError(f"steps[{index}].timeoutMs must be positive")
        _validate_step_command(step, index)
    return Scenario(1, scenario_id, scene, payload, source)


def _validate_step_command(step: Mapping[str, Any], index: int) -> None:
    if "command" in step:
        command = step["command"]
        if not isinstance(command, str) or "." not in command:
            raise ScenarioValidationError(
                f"steps[{index}].command must be a dotted command such as interaction.pointer"
            )
        family, name = command.split(".", 1)
        if not family or not name:
            raise ScenarioValidationError(f"steps[{index}] has invalid command")
        targetless = {"state.assert", "evidence.capture", "console.read", "scene.waitReady", "session.end", "session.abort"}
        if command not in targetless and not isinstance(
            step.get("target", step.get("targetId")), str
        ):
            raise ScenarioValidationError(f"steps[{index}] requires a stable target")
        return
    if isinstance(step.get("family"), str) and isinstance(step.get("name"), str):
        if step.get("family", "").strip() and step.get("name", "").strip():
            target = step.get("targetId", step.get("target", ""))
            if not isinstance(target, str):
                raise ScenarioValidationError(f"steps[{index}].targetId must be a string")
            return
    raise ScenarioValidationError(f"steps[{index}] has no supported command contract")


def discover_scenarios(root: Path | str, scenario_ids: Sequence[str] | None = None) -> list[Scenario]:
    """Load all JSON scenarios, optionally selecting exact stable IDs."""
    base = Path(root)
    requested = set(scenario_ids or ())
    scenarios: list[Scenario] = []
    for path in sorted(base.rglob("*.json")):
        if requested:
            try:
                candidate = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError):
                continue
            if not isinstance(candidate, dict) or candidate.get("id") not in requested:
                continue
        else:
            # Catalogs/manifests share the Resources tree but are not scenario
            # documents. A scenario always declares schemaVersion and id.
            try:
                candidate = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError):
                continue
            if not isinstance(candidate, dict) or "schemaVersion" not in candidate or "id" not in candidate:
                continue
            # Room manifests use the same version marker but are consumed by
            # the room schema validator; they are not executable gateway runs.
            candidate_steps = candidate.get("steps")
            if not isinstance(candidate_steps, list) or not any(
                isinstance(item, dict) and ("command" in item or ("family" in item and "name" in item))
                for item in candidate_steps
            ):
                continue
            candidate_scene = candidate.get("scene") or candidate.get("roomId")
            if not isinstance(candidate_scene, str) or not candidate_scene.strip():
                continue
        scenario = load_scenario(path)
        if not requested or scenario.scenario_id in requested:
            scenarios.append(scenario)
    if not scenarios:
        scope = ", ".join(sorted(requested)) if requested else str(base)
        raise ScenarioValidationError(f"no executable scenarios found for {scope}")
    if requested:
        found = {item.scenario_id for item in scenarios}
        missing = sorted(requested - found)
        if missing:
            raise ScenarioValidationError(f"scenario IDs not found: {', '.join(missing)}")
    return scenarios


def _step_command(step: Mapping[str, Any]) -> tuple[str, str, str]:
    if "command" in step:
        family, name = str(step["command"]).split(".", 1)
        target = str(step.get("target", step.get("targetId", "")))
        return family, name, target
    return str(step["family"]), str(step["name"]), str(step.get("targetId", step.get("target", "")))


def _response_ok(response: Mapping[str, Any]) -> bool:
    if not response:
        return False
    code = str(response.get("code", response.get("resultCode", "Ok")))
    # CLI command success is deliberately narrow. Unknown/missing codes are
    # environment or contract failures and must never become a gameplay PASS.
    return code.lower() in {"ok", "success"} and response.get("ok", True) is not False


_EXECUTION_DISPATCH_REAL_INPUT = "real-input"
_EXECUTION_DISPATCH_CAPABILITY_API = "capability-api"
_EXECUTION_DISPATCH_API_ONLY = "api-only"


def _step_id(step: Mapping[str, Any]) -> str:
    return str(step.get("id", step.get("stepId", ""))).strip()


def _step_requires_real_input(step: Mapping[str, Any]) -> bool:
    if str(step.get("command", "")) == "interaction.pointer":
        return True
    if step.get("family") == "interaction" and step.get("name") == "pointer":
        mode = (step.get("parameters") or {}).get("mode")
        return mode is None or str(mode).lower() == "realinput"
    return False


def _step_is_capability_invoke(step: Mapping[str, Any]) -> bool:
    if step.get("family") == "interaction" and step.get("name") == "invoke":
        return True
    command = str(step.get("command", ""))
    return command.startswith("interaction.") and command not in {
        "interaction.pointer",
        "interaction.drag",
        "interaction.key",
    }


def _load_evidence_events(resolved: Path, manifest: Mapping[str, Any]) -> list[Mapping[str, Any]]:
    events_name = str(manifest.get("EventsFileName", "events.jsonl") or "events.jsonl")
    candidates = [resolved / events_name, resolved / "journal.jsonl", resolved / "events.jsonl"]
    events: list[Mapping[str, Any]] = []
    for path in candidates:
        if not path.is_file():
            continue
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            line = line.strip()
            if not line:
                continue
            try:
                payload = json.loads(line)
            except json.JSONDecodeError:
                continue
            if isinstance(payload, dict):
                events.append(payload)
        if events:
            break
    return events


def _event_step_id(event: Mapping[str, Any]) -> str:
    command_id = str(event.get("CommandId", "")).strip()
    data = event.get("Data")
    if isinstance(data, Mapping):
        from_data = str(data.get("stepId", "")).strip()
        if from_data:
            return from_data
    return command_id


def _event_data(event: Mapping[str, Any]) -> Mapping[str, Any]:
    data = event.get("Data")
    return data if isinstance(data, Mapping) else {}


def validate_unity_scenario_evidence(
    path_value: Any,
    scenario_payload: Mapping[str, Any] | None,
    *,
    expected_scenario_id: str | None = None,
) -> dict[str, Any]:
    """Validate Unity manifest + per-step evidence for autorun PASS."""
    base = _validate_unity_evidence_manifest(path_value)
    if not base.get("ok"):
        return base
    resolved = Path(str(base["path"]))
    manifest = base["manifest"]
    scenario_id = str(scenario_payload.get("id", "")).strip() if isinstance(scenario_payload, Mapping) else ""
    if expected_scenario_id and scenario_id and scenario_id != expected_scenario_id:
        return {
            "ok": False,
            "reason": "scenario-id-mismatch",
            "path": str(resolved),
            "expectedScenarioId": expected_scenario_id,
            "observedScenarioId": scenario_id,
        }
    manifest_scenario = ""
    if isinstance(manifest, dict):
        manifest_scenario = str(manifest.get("ScenarioId", manifest.get("scenarioId", ""))).strip()
    if expected_scenario_id and manifest_scenario and manifest_scenario != expected_scenario_id:
        return {
            "ok": False,
            "reason": "stale-evidence-scenario",
            "path": str(resolved),
            "expectedScenarioId": expected_scenario_id,
            "observedScenarioId": manifest_scenario,
        }
    if not isinstance(scenario_payload, Mapping):
        return base
    steps = scenario_payload.get("steps")
    if not isinstance(steps, list) or not steps:
        return {"ok": False, "reason": "missing-scenario-steps", "path": str(resolved)}
    required_ids = [_step_id(step) for step in steps if isinstance(step, dict) and _step_id(step)]
    if not required_ids:
        return {"ok": False, "reason": "missing-scenario-steps", "path": str(resolved)}

    events = _load_evidence_events(resolved, manifest if isinstance(manifest, dict) else {})
    if not events:
        return {"ok": False, "reason": "missing-evidence-events", "path": str(resolved)}

    observed: dict[str, Mapping[str, Any]] = {}
    for event in events:
        if str(event.get("Type", "")) not in {"CommandResult", "Assertion"}:
            continue
        step_id = _event_step_id(event)
        if step_id:
            observed[step_id] = _event_data(event)

    missing_steps = [step_id for step_id in required_ids if step_id not in observed]
    if missing_steps:
        return {
            "ok": False,
            "reason": "missing-required-step-evidence",
            "path": str(resolved),
            "missingStepIds": missing_steps,
        }

    for step in steps:
        if not isinstance(step, dict):
            continue
        step_id = _step_id(step)
        if not step_id:
            continue
        data = observed.get(step_id, {})
        dispatch = str(data.get("executionDispatch", "")).lower()
        input_mode = str(data.get("inputMode", "")).lower()
        if _step_requires_real_input(step):
            if dispatch != _EXECUTION_DISPATCH_REAL_INPUT or input_mode != "realinput":
                return {
                    "ok": False,
                    "reason": "missing-real-input-evidence",
                    "path": str(resolved),
                    "stepId": step_id,
                    "observed": dict(data),
                }
        elif _step_is_capability_invoke(step):
            capability_id = str(step.get("targetId", step.get("target", ""))).strip()
            csharp_dispatch = str(data.get("csharpDispatch", "")).strip()
            if not csharp_dispatch:
                return {
                    "ok": False,
                    "reason": "missing-csharp-dispatch-evidence",
                    "path": str(resolved),
                    "stepId": step_id,
                    "observed": dict(data),
                }
            if dispatch != _EXECUTION_DISPATCH_CAPABILITY_API:
                return {
                    "ok": False,
                    "reason": "missing-capability-dispatch-evidence",
                    "path": str(resolved),
                    "stepId": step_id,
                    "observed": dict(data),
                }
            recorded_capability = str(data.get("capabilityId", "")).strip()
            if capability_id and recorded_capability and recorded_capability != capability_id:
                return {
                    "ok": False,
                    "reason": "stale-step-evidence",
                    "path": str(resolved),
                    "stepId": step_id,
                }
        elif dispatch == _EXECUTION_DISPATCH_API_ONLY and _step_requires_real_input(step):
            return {"ok": False, "reason": "missing-real-input-evidence", "path": str(resolved), "stepId": step_id}

    return {**base, "requiredStepIds": required_ids, "observedStepIds": sorted(observed.keys())}


def _validate_unity_evidence_manifest(path_value: Any) -> dict[str, Any]:
    """Validate Unity's immutable manifest shell before step-level checks."""
    if not isinstance(path_value, str) or not path_value.strip():
        return {"ok": False, "reason": "missing-evidence-path"}
    evidence = Path(path_value)
    if not evidence.is_absolute():
        evidence = Path.cwd() / evidence
    try:
        resolved = evidence.resolve()
    except OSError:
        return {"ok": False, "reason": "invalid-evidence-path"}
    parts = [item.lower() for item in resolved.parts]
    try:
        index = parts.index("docs")
        if parts[index + 1:index + 3] != ["qa", "runs"]:
            return {"ok": False, "reason": "evidence-outside-docs-qa-runs"}
    except (ValueError, IndexError):
        return {"ok": False, "reason": "evidence-outside-docs-qa-runs"}
    manifest_path = resolved / "manifest.json"
    if not manifest_path.is_file():
        return {"ok": False, "reason": "missing-evidence-manifest", "path": str(resolved)}
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {"ok": False, "reason": "invalid-evidence-manifest", "path": str(resolved)}
    if not isinstance(manifest, dict):
        return {"ok": False, "reason": "invalid-evidence-manifest", "path": str(resolved)}
    if str(manifest.get("RunDirectoryName", "")) != resolved.name or not manifest.get("EndedAtUtc"):
        return {"ok": False, "reason": "stale-evidence-manifest", "path": str(resolved), "manifest": manifest}
    if str(manifest.get("Verdict", "")).lower() != "pass":
        return {"ok": False, "reason": "unity-evidence-not-pass", "path": str(resolved), "manifest": manifest}
    try:
        assertion_count = int(manifest.get("AssertionPassedCount", 0) or 0)
        screenshot_count = int(manifest.get("ScreenshotCount", 0) or 0)
    except (TypeError, ValueError):
        return {"ok": False, "reason": "invalid-evidence-counts", "path": str(resolved)}
    if assertion_count <= 0 or screenshot_count <= 0 or manifest.get("ConsoleRecorded") is not True:
        return {"ok": False, "reason": "incomplete-evidence", "path": str(resolved), "manifest": manifest}
    report = resolved / str(manifest.get("ReportFileName", "report.md"))
    if not report.is_file():
        return {"ok": False, "reason": "missing-evidence-report", "path": str(resolved)}
    return {"ok": True, "path": str(resolved), "manifest": manifest}


def _validate_unity_evidence(
    path_value: Any,
    scenario_payload: Mapping[str, Any] | None = None,
    *,
    expected_scenario_id: str | None = None,
) -> dict[str, Any]:
    """Validate Unity evidence manifest and optional scenario step markers."""
    return validate_unity_scenario_evidence(
        path_value,
        scenario_payload,
        expected_scenario_id=expected_scenario_id,
    )


class ScenarioRunner:
    """Run scenarios sequentially and produce an evidence bundle."""

    def __init__(self, gateway: ScenarioGateway, run_root: Path | str) -> None:
        self.gateway = gateway
        self.run_root = Path(run_root)

    def run(self, scenario: Scenario) -> dict[str, Any]:
        self.run_root.mkdir(parents=True, exist_ok=True)
        calls: list[dict[str, Any]] = []
        executed: list[str] = []
        failure: dict[str, Any] | None = None
        for step in scenario.payload["steps"]:
            step_id = str(step.get("id", step.get("stepId")))
            family, name, target = _step_command(step)
            try:
                response = self.gateway.invoke(family, name, target)
            except Exception as exc:  # gateway failures are evidence, not silent skips
                response = {"code": "GatewayException", "message": str(exc)}
            safe_response = sanitize_for_report(response if isinstance(response, Mapping) else {"value": response})
            record = {"stepId": step_id, "family": family, "name": name, "target": target, "response": safe_response}
            calls.append(record)
            if not isinstance(response, Mapping) or not _response_ok(response):
                failure = {"stepId": step_id, "result_code": safe_response.get("code", "InvalidResponse")}
                break
            executed.append(step_id)
        verdict = "PASS" if failure is None and len(executed) == len(scenario.payload["steps"]) else "FAIL"
        summary: dict[str, Any] = {
            "run_id": self.run_root.name,
            "scenario_id": scenario.scenario_id,
            "scene": scenario.scene,
            "verdict": verdict,
            "state": "completed" if verdict == "PASS" else "failed",
            "executed_step_ids": executed,
            "required_step_ids": [str(item.get("id", item.get("stepId"))) for item in scenario.payload["steps"]],
            "calls": calls,
        }
        if failure:
            summary["failure"] = failure
        (self.run_root / "manifest.json").write_text(
            json.dumps(sanitize_for_report(summary), indent=2, sort_keys=True) + "\n", encoding="utf-8"
        )
        (self.run_root / "events.jsonl").write_text(
            "".join(json.dumps(item, sort_keys=True) + "\n" for item in calls), encoding="utf-8"
        )
        (self.run_root / "report.md").write_text(render_report(summary) + "\n", encoding="utf-8")
        return summary

    def run_via_gateway(self, scenario: Scenario) -> dict[str, Any]:
        """Use Unity's atomic qa_run operation when the live gateway provides it."""
        self.run_root.mkdir(parents=True, exist_ok=True)
        timeout_ms = max(120000, sum(int(step.get("timeoutMs", 30000)) for step in scenario.payload["steps"]))
        try:
            response = self.gateway.run_scenario(scenario.scenario_id, timeout_ms)
        except Exception as exc:
            response = {"code": "GatewayException", "message": str(exc)}
        safe = sanitize_for_report(response if isinstance(response, Mapping) else {"value": response})
        data = safe.get("data") if isinstance(safe.get("data"), Mapping) else {}
        outcome = str(safe.get("outcomeCode") or data.get("outcomeCode", ""))
        operation_ok = _response_ok(response if isinstance(response, Mapping) else {})
        response_scenario_id = str(
            safe.get("scenarioId") or data.get("scenarioId") or ""
        ).strip()
        if response_scenario_id and response_scenario_id != scenario.scenario_id:
            operation_ok = False
        evidence = _validate_unity_evidence(
            safe.get("evidenceRunDirectoryPath"),
            scenario.payload,
            expected_scenario_id=scenario.scenario_id,
        )
        status_info = safe.get("qaStatus") if isinstance(safe.get("qaStatus"), Mapping) else {}
        status_clean = not any(
            bool(status_info.get(key))
            for key in ("activeRunId", "activeScenarioId", "isQaProfileActive", "isScenarioRunning")
        )
        verdict = "PASS" if operation_ok and outcome.lower() == "passed" and evidence["ok"] and status_clean else "BLOCKED"
        summary: dict[str, Any] = {
            "run_id": self.run_root.name,
            "scenario_id": scenario.scenario_id,
            "scene": scenario.scene,
            "verdict": verdict,
            "state": "completed" if verdict == "PASS" else "failed",
            "gateway": "qa_run",
            "response": safe,
            "evidence": evidence,
            "statusClean": status_clean,
        }
        (self.run_root / "manifest.json").write_text(json.dumps(safe, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        (self.run_root / "events.jsonl").write_text(json.dumps(safe, sort_keys=True) + "\n", encoding="utf-8")
        (self.run_root / "report.md").write_text(render_report(summary) + "\n", encoding="utf-8")
        return summary


def run_scenarios(
    gateway: ScenarioGateway,
    scenario_root: Path | str,
    run_root: Path | str,
    scenario_ids: Sequence[str] | None = None,
) -> dict[str, Any]:
    """Execute the selected catalog entries in deterministic path order."""
    root = Path(run_root)
    scenarios = discover_scenarios(scenario_root, scenario_ids)
    cases: list[dict[str, Any]] = []
    for scenario in scenarios:
        case_root = root / scenario.scenario_id.replace("/", "_").replace("\\", "_")
        runner = ScenarioRunner(gateway, case_root)
        cases.append(
            runner.run_via_gateway(scenario)
            if callable(getattr(gateway, "run_scenario", None))
            else runner.run(scenario)
        )
    aggregate = aggregate_run(
        [
            {"required": True, "excluded": False, "scenarioVerdict": case["verdict"]}
            for case in cases
        ]
    )
    summary: dict[str, Any] = {
        "run_id": root.name,
        "verdict": aggregate["runVerdict"],
        "state": "completed",
        "scenario_ids": [case["scenario_id"] for case in cases],
        "cases": cases,
        "counts": aggregate["counts"],
    }
    root.mkdir(parents=True, exist_ok=True)
    (root / "manifest.json").write_text(
        json.dumps(sanitize_for_report(summary), indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )
    (root / "report.md").write_text(render_report(summary) + "\n", encoding="utf-8")
    return summary
