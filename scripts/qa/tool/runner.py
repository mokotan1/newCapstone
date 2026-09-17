"""Hall→Kitchen vertical runner (design AC19, AC23). Stub output cannot PASS."""

from __future__ import annotations

import json
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any, Protocol

from scripts.qa.tool.hall_route import compare_plan_to_wiring, judge_hall_attempt
from scripts.qa.tool.plan import build_hall_to_kitchen_plan
from scripts.qa.tool.preflight import evaluate_preflight
from scripts.qa.tool.reconnect import confirm_reconnect
from scripts.qa.tool.report import build_report
from scripts.qa.tool.verdict import aggregate_run, judge_scenario

REQUIRED_LAYERS: tuple[str, ...] = ("api", "event-system")
CLICK_TARGET = "hall.nav.click-kitchen-entry"
POINTER_TARGET = "hall.kitchen-entry"
FRONT_TARGET = "hall.nav.execute-front"
DOOR_TARGET = "hall.nav.execute-door"
RESET_TARGET = "hall.nav.reset-to-hall"
ASSERT_TARGET = "hall.nav.assert-route"
PROBE_TARGET = "hall.nav.probe"
HOP_WAIT_ATTEMPTS = 1
SCENE_AFTER_CLICK = "Hall_Left"
SCENE_AFTER_FRONT = "Hall_Left2"
SCENE_AFTER_DOOR = "Kitchen"


class VerticalGateway(Protocol):
    """홀 수직 실행이 호출하는 게이트웨이 계약. kind가 live가 아니면 stub-forbidden이다."""

    kind: str

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        """DeveloperQa family/name/target 한 건을 실행하고 응답 dict를 반환한다."""
        ...


def _truthy(value: Any) -> bool:
    """adapter 문자열 불리언(True/1/yes)을 Python bool로 해석한다."""
    return str(value).lower() in {"true", "1", "yes"}


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


def _wait_for_scene(
    gateway: VerticalGateway,
    *,
    expected: str,
    calls: list[dict[str, Any]],
    layer: str,
    wait_attempts: int,
    wait_sleep: Callable[[float], None],
) -> Mapping[str, Any]:
    """LoadScene이 비동기라 probe로 기대 씬이 올 때까지 폴링한다."""
    last: Mapping[str, Any] = {}
    attempts = max(1, wait_attempts)
    for index in range(attempts):
        probe = gateway.invoke("interaction", "invoke", PROBE_TARGET)
        calls.append({"layer": layer, "target": PROBE_TARGET, "response": probe})
        last = probe
        if _active_scene(probe) == expected:
            return probe
        if index + 1 < attempts:
            wait_sleep(0.3)
    return last


def run_hall_to_kitchen(
    *,
    run_root: Path,
    gateway: VerticalGateway,
    snapshot: Mapping[str, Any],
    previous_connection: Mapping[str, Any] | None = None,
    wait_attempts: int = HOP_WAIT_ATTEMPTS,
    wait_sleep: Callable[[float], None] | None = None,
) -> dict[str, Any]:
    """고정 Hall→Kitchen 계획을 실행한다. stub 출력으로는 AC19를 완료할 수 없다."""
    # TODO(AC19): screenshot 아티팩트 검증과 console delta 분류를 러너 안으로 넣을 것.
    # TODO(AC06): Hall_Left/Hall_Left2는 C# target id가 없어 execute-front/door가 fungus block이다.
    # TODO(AC23): 독립 리뷰 전에는 featureVerified를 True로 올리지 말 것.
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

    plan = build_hall_to_kitchen_plan()
    wiring = compare_plan_to_wiring(plan)
    if not wiring.get("ok"):
        return _blocked(run_root, str(wiring.get("reasonCode") or "spec-mismatch"))

    sleeper = wait_sleep or (lambda _seconds: None)

    calls: list[dict[str, Any]] = []
    attempts: list[dict[str, Any]] = []
    executed_steps: list[str] = []
    executed_layers: list[str] = []

    for layer in REQUIRED_LAYERS:
        reset = gateway.invoke("interaction", "invoke", RESET_TARGET)
        calls.append({"layer": layer, "target": RESET_TARGET, "response": reset})
        executed_steps.append(f"reset-{layer}")
        _wait_for_scene(
            gateway,
            expected="Hall_playerble",
            calls=calls,
            layer=layer,
            wait_attempts=wait_attempts,
            wait_sleep=sleeper,
        )
        hop_responses: list[Mapping[str, Any]] = []
        if layer == "event-system":
            click = gateway.invoke("interaction", "pointer", POINTER_TARGET)
            calls.append({"layer": layer, "target": POINTER_TARGET, "response": click})
        else:
            click = gateway.invoke("interaction", "invoke", CLICK_TARGET)
            calls.append({"layer": layer, "target": CLICK_TARGET, "response": click})
        hop_responses.append(click)
        executed_steps.append(f"navigate-{layer}")
        hop_responses.append(
            _wait_for_scene(
                gateway,
                expected=SCENE_AFTER_CLICK,
                calls=calls,
                layer=layer,
                wait_attempts=wait_attempts,
                wait_sleep=sleeper,
            )
        )
        for hop_target, expected_scene in (
            (FRONT_TARGET, SCENE_AFTER_FRONT),
            (DOOR_TARGET, SCENE_AFTER_DOOR),
        ):
            hop = gateway.invoke("interaction", "invoke", hop_target)
            calls.append({"layer": layer, "target": hop_target, "response": hop})
            hop_responses.append(hop)
            executed_steps.append(f"hop-{hop_target}-{layer}")
            hop_responses.append(
                _wait_for_scene(
                    gateway,
                    expected=expected_scene,
                    calls=calls,
                    layer=layer,
                    wait_attempts=wait_attempts,
                    wait_sleep=sleeper,
                )
            )
        assertion = gateway.invoke("interaction", "invoke", ASSERT_TARGET)
        calls.append({"layer": layer, "target": ASSERT_TARGET, "response": assertion})
        executed_steps.append(f"assert-kitchen-{layer}")
        executed_layers.append(layer)
        attempt = _attempt_from_response(layer=layer, response=assertion)
        visited = _collect_visited(hop_responses + [assertion])
        if visited:
            attempt["scenesVisited"] = visited
        if layer == "event-system":
            attempt["driver"] = str(click.get("driver") or "event-system")
        attempt["attemptId"] = f"attempt-{layer}"
        attempts.append(attempt)

    judged_attempts = [judge_hall_attempt(item) for item in attempts]
    case = judge_scenario(
        {
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
            "evidenceValid": True,
            "cleanupStatus": "not-applicable",
            "consoleClassification": "missing",
            "requiredArtifactKinds": ["screenshot"],
            "validatedArtifactKinds": [],
        }
    )
    aggregate = aggregate_run(
        [
            {
                "required": True,
                "excluded": False,
                "scenarioVerdict": case["scenarioVerdict"],
            }
        ]
    )
    payload = {
        "runId": run_root.name,
        "planId": plan["planId"],
        "runVerdict": aggregate["runVerdict"],
        "reasonCode": "ok" if aggregate["runVerdict"] == "PASS" else "vertical-incomplete",
        "featureVerified": False,
        "review": {"status": "missing"},
        "cleanupStatus": "not-applicable",
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
    (run_root / "report.json").write_text(
        json.dumps(json_payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (run_root / "report.md").write_text(markdown + "\n", encoding="utf-8")
    (run_root / "events.jsonl").write_text(
        "".join(json.dumps(item, sort_keys=True) + "\n" for item in calls),
        encoding="utf-8",
    )
    return json_payload
