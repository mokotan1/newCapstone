"""Advance Fungus Say/Menu with real DeveloperQa hops (probe → advance/choose)."""

from __future__ import annotations

from collections.abc import Callable, Mapping
from typing import Any

from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE, is_transport_failure

PROBE_TARGET = "fungus.dialogue.probe"
ADVANCE_TARGET = "fungus.dialogue.advance"
CHOOSE_TARGET = "fungus.dialogue.choose"
DEFAULT_MAX_STEPS = 12


def _truthy(value: Any) -> bool:
    """adapter 문자열 불리언을 Python bool로 해석한다."""
    return str(value).lower() in {"true", "1", "yes"}


def _idle(data: Mapping[str, Any]) -> bool:
    """Say/Menu가 화면에 없으면 대사가 끝난 것이다."""
    if _truthy(data.get("menuActive")):
        return False
    if _truthy(data.get("waitingForInput")) or _truthy(data.get("writing")):
        return False
    return not _truthy(data.get("sayActive"))


def settle_dialogue(
    gateway: Any,
    *,
    max_steps: int = DEFAULT_MAX_STEPS,
    wait_sleep: Callable[[float], None] | None = None,
    calls: list[dict[str, Any]] | None = None,
    layer: str = "",
) -> dict[str, Any]:
    """활성 Say는 advance, Menu는 첫 선택지를 고른다. idle이면 settled.

    전송 장애는 즉시 중단한다. capability가 없으면 unsettled이지 settled가 아니다.
    """
    sleeper = wait_sleep or (lambda _seconds: None)
    steps: list[str] = []
    last_probe: Mapping[str, Any] = {}
    attempts = max(1, max_steps)
    for _index in range(attempts):
        probe = gateway.invoke("interaction", "invoke", PROBE_TARGET)
        if calls is not None:
            calls.append({"layer": layer, "target": PROBE_TARGET, "response": probe})
        last_probe = probe
        code = str(probe.get("code") or "")
        if code == TRANSPORT_DOWN_CODE or is_transport_failure(probe):
            return {"status": "transport", "steps": steps, "probe": probe}
        if code in {"MissingCapability", "UnsupportedCommand"}:
            return {
                "status": "unsettled",
                "reason": "missing-dialogue-capability",
                "steps": steps,
                "probe": probe,
            }
        data = dict(probe.get("data") or {})
        if _truthy(data.get("menuActive")):
            chosen = gateway.invoke("interaction", "invoke", CHOOSE_TARGET)
            if calls is not None:
                calls.append({"layer": layer, "target": CHOOSE_TARGET, "response": chosen})
            steps.append("choose")
            if str(chosen.get("code") or "") == TRANSPORT_DOWN_CODE or is_transport_failure(chosen):
                return {"status": "transport", "steps": steps, "probe": chosen}
            sleeper(0.05)
            continue
        if _truthy(data.get("waitingForInput")) or _truthy(data.get("writing")):
            advanced = gateway.invoke("interaction", "invoke", ADVANCE_TARGET)
            if calls is not None:
                calls.append({"layer": layer, "target": ADVANCE_TARGET, "response": advanced})
            steps.append("advance")
            if str(advanced.get("code") or "") == TRANSPORT_DOWN_CODE or is_transport_failure(advanced):
                return {"status": "transport", "steps": steps, "probe": advanced}
            sleeper(0.05)
            continue
        if _idle(data):
            return {"status": "settled", "steps": steps, "probe": probe}
        sleeper(0.05)
    return {
        "status": "unsettled",
        "reason": "still-active",
        "steps": steps,
        "probe": last_probe,
    }
