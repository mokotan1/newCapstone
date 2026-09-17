"""Hall→Kitchen vertical runner (design AC19, AC23). Stub output cannot PASS."""

from __future__ import annotations

import json
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any, Protocol

from scripts.qa.tool.console import classify_console_delta
from scripts.qa.tool.evidence import validate_artifact
from scripts.qa.tool.hall_route import (
    compare_plan_to_wiring,
    hop_capabilities_from_plan,
    judge_hall_attempt,
)
from scripts.qa.tool.isolation import compare_player_store
from scripts.qa.tool.lease import acquire_file_lease, release_file_lease
from scripts.qa.tool.lifecycle import cleanup_status_from_recover
from scripts.qa.tool.plan import build_hall_to_kitchen_plan
from scripts.qa.tool.preflight import evaluate_preflight
from scripts.qa.tool.reconnect import confirm_reconnect
from scripts.qa.tool.report import build_report
from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE, is_transport_failure
from scripts.qa.tool.verdict import aggregate_run, judge_scenario

REQUIRED_LAYERS: tuple[str, ...] = ("api", "event-system")
CLICK_TARGET = "hall.nav.click-kitchen-entry"
POINTER_TARGET = "hall.kitchen-entry"
RESET_TARGET = "hall.nav.reset-to-hall"
ASSERT_TARGET = "hall.nav.assert-route"
PROBE_TARGET = "hall.nav.probe"
HOP_WAIT_ATTEMPTS = 1
SCENE_AFTER_CLICK = "Hall_Left"


class VerticalGateway(Protocol):
    """홀 수직 실행이 호출하는 게이트웨이 계약. kind가 live가 아니면 stub-forbidden이다."""

    kind: str

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        """DeveloperQa family/name/target 한 건을 실행하고 응답 dict를 반환한다."""
        ...


class RunLifecycle(Protocol):
    """라이브 경로의 격리 스냅샷·복원·증거 수집 계약."""

    def snapshot_player_store(self) -> dict[str, Any]:
        """QA 전 PlayerPrefs/프로필 스냅샷."""
        ...

    def recover(self) -> dict[str, Any]:
        """qa_recover. 실패면 ok=False 또는 uncertain=True."""
        ...

    def cancel(self) -> dict[str, Any]:
        """qa_cancel."""
        ...

    def capture_screenshot(self, output_path: Path) -> dict[str, Any]:
        """Game View PNG를 지정 경로에 저장한다."""
        ...

    def capture_console(self) -> dict[str, Any]:
        """콘솔 delta용 엔트리 목록."""
        ...


def _truthy(value: Any) -> bool:
    """adapter 문자열 불리언(True/1/yes)을 Python bool로 해석한다."""
    return str(value).lower() in {"true", "1", "yes"}


def _is_transport_down(payload: Mapping[str, Any], gateway: Any) -> bool:
    """게이트웨이가 잠겼거나 이번 응답이 TransportDown인지 본다."""
    if bool(getattr(gateway, "tripped", False)):
        return True
    return str(payload.get("code") or "") == TRANSPORT_DOWN_CODE or is_transport_failure(payload)


def _blocked(run_root: Path, reason_code: str, **extra: Any) -> dict[str, Any]:
    """실행을 시작하지 못한 경우를 BLOCKED 보고서로 남긴다. featureVerified는 항상 False."""
    payload: dict[str, Any] = {
        "runId": run_root.name,
        "planId": "hall-to-kitchen.v1",
        "runVerdict": "BLOCKED",
        "reasonCode": reason_code,
        "featureVerified": False,
        "review": {"status": "missing"},
        "cleanupStatus": extra.get("cleanupStatus", "not-applicable"),
        "cases": extra.get("cases", []),
        "counts": extra.get("counts", {}),
        "gatewayCalls": extra.get("gatewayCalls", []),
    }
    payload.update(extra)
    json_payload, markdown = build_report(payload)
    (run_root / "report.json").write_text(
        json.dumps(json_payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (run_root / "report.md").write_text(markdown + "\n", encoding="utf-8")
    return json_payload


def _attempt_from_response(
    *,
    layer: str,
    response: Mapping[str, Any],
) -> dict[str, Any]:
    """assert-route 응답 한 건을 hall_route 판정기가 읽는 attempt 레코드로 변환한다."""
    data = dict(response.get("data") or {})
    destination = str(data.get("activeScene") or response.get("destinationScene") or "")
    visited = [str(item) for item in (response.get("scenesVisited") or [])]
    if not visited:
        visited = ["Hall_playerble"]
        if destination and destination not in visited:
            visited.append(destination)
    return {
        "inputLayer": layer,
        "driver": str(response.get("driver") or layer),
        "initialScene": "Hall_playerble",
        "scenesVisited": visited,
        "destinationScene": destination,
        "transitionPending": _truthy(data.get("transitionPending")),
        "inputGateBlocked": _truthy(data.get("inputGateBlocked")),
        "controllerFound": _truthy(data.get("controllerFound")),
        "rawCode": response.get("code"),
    }


def _collect_visited(responses: Sequence[Mapping[str, Any]]) -> list[str]:
    """hop 응답들의 activeScene을 순서 유지한 방문 목록으로 모은다."""
    visited: list[str] = ["Hall_playerble"]
    for response in responses:
        data = dict(response.get("data") or {})
        scene = str(data.get("activeScene") or response.get("destinationScene") or "")
        if scene and scene not in visited:
            visited.append(scene)
        extra = response.get("scenesVisited") or []
        for item in extra:
            name = str(item)
            if name and name not in visited:
                visited.append(name)
    return visited


def _active_scene(response: Mapping[str, Any]) -> str:
    """응답 data.activeScene을 읽는다."""
    data = dict(response.get("data") or {})
    return str(data.get("activeScene") or response.get("destinationScene") or "")


def _record(
    calls: list[dict[str, Any]],
    *,
    layer: str,
    target: str,
    response: Mapping[str, Any],
) -> Mapping[str, Any]:
    """게이트웨이 호출을 events.jsonl용 목록에 남긴다."""
    calls.append({"layer": layer, "target": target, "response": dict(response)})
    return response


def _wait_for_scene(
    gateway: VerticalGateway,
    *,
    expected: str,
    calls: list[dict[str, Any]],
    layer: str,
    wait_attempts: int,
    wait_sleep: Callable[[float], None],
) -> Mapping[str, Any]:
    """LoadScene이 비동기라 probe로 기대 씬이 올 때까지 폴링한다. 전송 장애면 즉시 반환한다."""
    last: Mapping[str, Any] = {}
    attempts = max(1, wait_attempts)
    for index in range(attempts):
        probe = gateway.invoke("interaction", "invoke", PROBE_TARGET)
        _record(calls, layer=layer, target=PROBE_TARGET, response=probe)
        last = probe
        if _is_transport_down(probe, gateway):
            return probe
        if _active_scene(probe) == expected:
            return probe
        if index + 1 < attempts:
            wait_sleep(0.3)
    return last


def _collect_screenshot(
    *,
    lifecycle: RunLifecycle | None,
    run_root: Path,
    hop_id: str,
    artifacts: list[dict[str, Any]],
    gateway: Any,
) -> bool:
    """hop 스크린샷을 evidence/ 아래 저장하고 검증한다. 전송 장애면 False."""
    if lifecycle is None:
        return True
    if bool(getattr(gateway, "tripped", False)):
        return False
    relative = f"evidence/{hop_id}.png"
    output = run_root / relative
    output.parent.mkdir(parents=True, exist_ok=True)
    shot = lifecycle.capture_screenshot(output)
    if str(shot.get("code") or "") == TRANSPORT_DOWN_CODE:
        return False
    artifact = {"id": hop_id, "kind": "screenshot", "relativePath": relative}
    artifacts.append(artifact)
    return True


def run_hall_to_kitchen(
    *,
    run_root: Path,
    gateway: VerticalGateway,
    snapshot: Mapping[str, Any],
    previous_connection: Mapping[str, Any] | None = None,
    wait_attempts: int = HOP_WAIT_ATTEMPTS,
    wait_sleep: Callable[[float], None] | None = None,
    lease_path: Path | None = None,
    lease_owner: str = "qa-tool",
    pid_alive: Callable[[int], bool] | None = None,
    now: str = "",
    coordinator: Any | None = None,
    lifecycle: RunLifecycle | None = None,
    settle_dialogue: Callable[..., dict[str, Any]] | None = None,
) -> dict[str, Any]:
    """고정 Hall→Kitchen 계획을 실행한다. stub 출력으로는 AC19를 완료할 수 없다."""
    run_root.mkdir(parents=True, exist_ok=True)
    if getattr(gateway, "kind", "stub") != "live":
        return _blocked(run_root, "stub-forbidden")

    if previous_connection is not None:
        reconnect = confirm_reconnect(previous_connection, snapshot)
        if not reconnect["ok"]:
            return _blocked(run_root, str(reconnect["reasonCode"]))

    preflight = evaluate_preflight(snapshot)
    if preflight.get("executionStatus") == "blocked":
        return _blocked(run_root, str(preflight.get("reasonCode") or "preflight"))

    if lease_path is not None:
        raw_pid = snapshot.get("editorPid") or 0
        try:
            pid = int(raw_pid)
        except (TypeError, ValueError):
            pid = 0
        alive = pid_alive or (lambda _pid: True)
        acquired = acquire_file_lease(
            lease_path,
            owner=lease_owner,
            pid=pid,
            pid_alive=alive,
            now=now,
        )
        if acquired.get("executionStatus") == "blocked":
            return _blocked(run_root, str(acquired.get("reasonCode") or "ownership"))

    if coordinator is not None:
        started = coordinator.start_run(snapshot)
        if started.get("executionStatus") == "blocked":
            if lease_path is not None:
                release_file_lease(lease_path, owner=lease_owner)
            return _blocked(run_root, str(started.get("reasonCode") or "preflight"))
        coordinator.enter_running()

    plan = build_hall_to_kitchen_plan()
    wiring = compare_plan_to_wiring(plan)
    if not wiring.get("ok"):
        if lease_path is not None:
            release_file_lease(lease_path, owner=lease_owner)
        return _blocked(run_root, str(wiring.get("reasonCode") or "spec-mismatch"))

    sleeper = wait_sleep or (lambda _seconds: None)
    fungus_hops = hop_capabilities_from_plan(plan)

    calls: list[dict[str, Any]] = []
    attempts: list[dict[str, Any]] = []
    executed_steps: list[str] = []
    executed_layers: list[str] = []
    artifacts: list[dict[str, Any]] = []
    transport_down = False
    dialogue_status = "settled" if settle_dialogue is not None else None
    isolation_preserved: bool | None = None
    player_before: dict[str, Any] | None = None
    console_class = {"classification": "missing", "reasonCodes": ["console-missing"]}

    if lifecycle is not None:
        player_before = lifecycle.snapshot_player_store()
        baseline = lifecycle.capture_console()
        if str(baseline.get("code") or "") == TRANSPORT_DOWN_CODE:
            transport_down = True
        baseline_fps = [
            str(item.get("fingerprint") or "")
            for item in (baseline.get("entries") or [])
            if isinstance(item, Mapping)
        ]
    else:
        baseline_fps = []

    def _settle(layer_name: str) -> None:
        nonlocal transport_down, dialogue_status
        if settle_dialogue is None or transport_down:
            return
        settled = settle_dialogue(
            gateway,
            wait_sleep=sleeper,
            calls=calls,
            layer=layer_name,
        )
        if settled.get("status") == "transport":
            transport_down = True
        elif settled.get("status") == "unsettled":
            dialogue_status = "unsettled"

    for layer in REQUIRED_LAYERS:
        if transport_down:
            break
        reset = gateway.invoke("interaction", "invoke", RESET_TARGET)
        _record(calls, layer=layer, target=RESET_TARGET, response=reset)
        executed_steps.append(f"reset-{layer}")
        if _is_transport_down(reset, gateway):
            transport_down = True
            break
        waited = _wait_for_scene(
            gateway,
            expected="Hall_playerble",
            calls=calls,
            layer=layer,
            wait_attempts=wait_attempts,
            wait_sleep=sleeper,
        )
        if _is_transport_down(waited, gateway):
            transport_down = True
            break
        hop_responses: list[Mapping[str, Any]] = []
        if layer == "event-system":
            click = gateway.invoke("interaction", "pointer", POINTER_TARGET)
            _record(calls, layer=layer, target=POINTER_TARGET, response=click)
        else:
            click = gateway.invoke("interaction", "invoke", CLICK_TARGET)
            _record(calls, layer=layer, target=CLICK_TARGET, response=click)
        hop_responses.append(click)
        executed_steps.append(f"navigate-{layer}")
        if _is_transport_down(click, gateway):
            transport_down = True
            break
        _settle(layer)
        if transport_down:
            break
        after_click = _wait_for_scene(
            gateway,
            expected=SCENE_AFTER_CLICK,
            calls=calls,
            layer=layer,
            wait_attempts=wait_attempts,
            wait_sleep=sleeper,
        )
        hop_responses.append(after_click)
        if _is_transport_down(after_click, gateway):
            transport_down = True
            break
        _settle(layer)
        if not _collect_screenshot(
            lifecycle=lifecycle,
            run_root=run_root,
            hop_id=f"{layer}-click",
            artifacts=artifacts,
            gateway=gateway,
        ):
            transport_down = True
            break
        layer_halted = False
        for hop_target, expected_scene in fungus_hops:
            hop = gateway.invoke("interaction", "invoke", hop_target)
            _record(calls, layer=layer, target=hop_target, response=hop)
            hop_responses.append(hop)
            executed_steps.append(f"hop-{hop_target}-{layer}")
            if _is_transport_down(hop, gateway):
                transport_down = True
                layer_halted = True
                break
            _settle(layer)
            if transport_down:
                layer_halted = True
                break
            after_hop = _wait_for_scene(
                gateway,
                expected=expected_scene,
                calls=calls,
                layer=layer,
                wait_attempts=wait_attempts,
                wait_sleep=sleeper,
            )
            hop_responses.append(after_hop)
            if _is_transport_down(after_hop, gateway):
                transport_down = True
                layer_halted = True
                break
            _settle(layer)
            if not _collect_screenshot(
                lifecycle=lifecycle,
                run_root=run_root,
                hop_id=f"{layer}-{expected_scene}",
                artifacts=artifacts,
                gateway=gateway,
            ):
                transport_down = True
                layer_halted = True
                break
        if layer_halted or transport_down:
            break
        assertion = gateway.invoke("interaction", "invoke", ASSERT_TARGET)
        _record(calls, layer=layer, target=ASSERT_TARGET, response=assertion)
        executed_steps.append(f"assert-kitchen-{layer}")
        if _is_transport_down(assertion, gateway):
            transport_down = True
            break
        executed_layers.append(layer)
        attempt = _attempt_from_response(layer=layer, response=assertion)
        visited = _collect_visited(hop_responses + [assertion])
        if visited:
            attempt["scenesVisited"] = visited
        if layer == "event-system":
            attempt["driver"] = str(click.get("driver") or "event-system")
        attempt["attemptId"] = f"attempt-{layer}"
        attempts.append(attempt)

    if lifecycle is not None and player_before is not None:
        player_after = lifecycle.snapshot_player_store()
        isolation = compare_player_store(player_before, player_after)
        isolation_preserved = bool(isolation.get("preserved"))

    cleanup_status = "not-applicable"
    recovery_status = None
    if coordinator is not None:
        if transport_down:
            failed = coordinator.fail_run("transport-down")
            cleanup_status = str(failed.get("cleanupStatus") or "uncertain")
        else:
            finished = coordinator.finish_run()
            cleanup_status = str(finished.get("cleanupStatus") or "not-applicable")
            if finished.get("executionStatus") == "recovery-failed":
                recovery_status = "failed"
        if cleanup_status in {"failed", "uncertain"}:
            recovery_status = "failed"
    elif lifecycle is not None:
        recovered = lifecycle.recover()
        cleanup_status = cleanup_status_from_recover(recovered)
        if cleanup_status in {"failed", "uncertain"}:
            recovery_status = "failed"
        elif not transport_down:
            recovery_status = "restored"

    if lifecycle is not None and not transport_down:
        end_console = lifecycle.capture_console()
        if str(end_console.get("code") or "") != TRANSPORT_DOWN_CODE:
            console_class = classify_console_delta(
                baseline_fingerprints=baseline_fps,
                collected=True,
                entries=list(end_console.get("entries") or []),
            )

    validated_kinds: list[str] = []
    evidence_valid = True
    for artifact in artifacts:
        checked = validate_artifact(run_root, artifact)
        if checked.get("verdict") != "PASS":
            evidence_valid = False
        elif artifact.get("kind") == "screenshot" and "screenshot" not in validated_kinds:
            validated_kinds.append("screenshot")
    if artifacts and not validated_kinds:
        evidence_valid = False

    judged_attempts = [judge_hall_attempt(item) for item in attempts]
    case_record: dict[str, Any] = {
        "scenarioId": "qa.tool.hall-to-kitchen",
        "started": True,
        "requiredStepIds": [
            "reset-api",
            "navigate-api",
            "hop-hall.nav.execute-front-api",
            "hop-hall.nav.execute-door-api",
            "assert-kitchen-api",
            "reset-event-system",
            "navigate-event-system",
            "hop-hall.nav.execute-front-event-system",
            "hop-hall.nav.execute-door-event-system",
            "assert-kitchen-event-system",
        ],
        "executedStepIds": executed_steps,
        "requiredInputLayers": list(REQUIRED_LAYERS),
        "executedInputLayers": executed_layers,
        "assertions": [
            {
                "id": item["attemptId"],
                "expected": "Kitchen",
                "observed": next(
                    (
                        raw["destinationScene"]
                        for raw in attempts
                        if raw.get("attemptId") == item["attemptId"]
                    ),
                    "",
                ),
                "verdict": item["attemptVerdict"],
            }
            for item in judged_attempts
        ],
        "evidenceValid": evidence_valid if artifacts else True,
        "cleanupStatus": cleanup_status,
        "consoleClassification": console_class.get("classification", "missing"),
        "requiredArtifactKinds": ["screenshot"],
        "validatedArtifactKinds": validated_kinds,
    }
    if isolation_preserved is not None:
        case_record["isolationPreserved"] = isolation_preserved
    if recovery_status is not None:
        case_record["recoveryStatus"] = recovery_status
    if transport_down:
        case_record["transportStatus"] = "down"
    if dialogue_status is not None:
        case_record["dialogueStatus"] = dialogue_status
    case = judge_scenario(case_record)
    aggregate = aggregate_run(
        [
            {
                "required": True,
                "excluded": False,
                "scenarioVerdict": case["scenarioVerdict"],
            }
        ]
    )
    if transport_down:
        reason_code = "transport-down"
    elif aggregate["runVerdict"] == "PASS":
        reason_code = "ok"
    elif recovery_status == "failed":
        reason_code = "recovery-failed"
    else:
        reason_code = "vertical-incomplete"
    payload = {
        "runId": run_root.name,
        "planId": plan["planId"],
        "runVerdict": aggregate["runVerdict"],
        "reasonCode": reason_code,
        "featureVerified": False,
        "review": {"status": "missing"},
        "cleanupStatus": cleanup_status,
        "cases": [case],
        "counts": aggregate["counts"],
        "attempts": judged_attempts,
        "gatewayCalls": calls,
        "phase1Scope": "Hall_playerble → Hall_Left → Hall_Left2 → Kitchen",
        "followOnNotComplete": ["AC24-AC32", "independent-review"],
    }
    json_payload, markdown = build_report(payload)
    json_payload["featureVerified"] = False
    json_payload["review"] = {"status": "missing"}
    json_payload["attempts"] = judged_attempts
    json_payload["gatewayCalls"] = calls
    json_payload["reasonCode"] = payload["reasonCode"]
    json_payload["cleanupStatus"] = cleanup_status
    (run_root / "report.json").write_text(
        json.dumps(json_payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (run_root / "report.md").write_text(markdown + "\n", encoding="utf-8")
    (run_root / "events.jsonl").write_text(
        "".join(json.dumps(item, sort_keys=True) + "\n" for item in calls),
        encoding="utf-8",
    )
    if lease_path is not None:
        release_file_lease(lease_path, owner=lease_owner)
    return json_payload
