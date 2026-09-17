"""Settle Fungus Say/Menu with real probe→advance/choose hops."""

from __future__ import annotations

from typing import Any

from scripts.qa.tool.dialogue import settle_dialogue
from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE


class _Gateway:
    kind = "live"

    def __init__(self, responses: list[dict[str, Any]]) -> None:
        self.calls: list[tuple[str, str]] = []
        self._responses = list(responses)

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        self.calls.append((name, target))
        return dict(self._responses.pop(0))


def _probe(**flags: str) -> dict[str, Any]:
    data = {
        "sayActive": "False",
        "waitingForInput": "False",
        "writing": "False",
        "menuActive": "False",
        "optionCount": "0",
    }
    data.update(flags)
    return {"ok": True, "code": "Ok", "data": data}


def test_idle_dialogue_is_settled_without_advance() -> None:
    gateway = _Gateway([_probe()])
    result = settle_dialogue(gateway)
    assert result["status"] == "settled"
    assert gateway.calls == [("invoke", "fungus.dialogue.probe")]


def test_waiting_say_is_advanced_until_idle() -> None:
    gateway = _Gateway(
        [
            _probe(sayActive="True", waitingForInput="True"),
            {"ok": True, "code": "Ok", "data": {"advanced": "True"}},
            _probe(),
        ]
    )
    result = settle_dialogue(gateway)
    assert result["status"] == "settled"
    assert result["steps"] == ["advance"]
    assert [item[1] for item in gateway.calls] == [
        "fungus.dialogue.probe",
        "fungus.dialogue.advance",
        "fungus.dialogue.probe",
    ]


def test_active_menu_chooses_first_option() -> None:
    gateway = _Gateway(
        [
            _probe(menuActive="True", optionCount="2"),
            {"ok": True, "code": "Ok", "data": {"chosen": "True"}},
            _probe(),
        ]
    )
    result = settle_dialogue(gateway)
    assert result["status"] == "settled"
    assert result["steps"] == ["choose"]
    assert [item[1] for item in gateway.calls][1] == "fungus.dialogue.choose"


def test_missing_capability_is_unsettled_not_settled() -> None:
    gateway = _Gateway(
        [{"ok": False, "code": "MissingCapability", "message": "fungus.dialogue.probe"}]
    )
    result = settle_dialogue(gateway)
    assert result["status"] == "unsettled"
    assert result["reason"] == "missing-dialogue-capability"


def test_transport_down_stops_dialogue_loop() -> None:
    gateway = _Gateway(
        [
            _probe(sayActive="True", waitingForInput="True"),
            {"ok": False, "code": TRANSPORT_DOWN_CODE, "refused": False},
        ]
    )
    result = settle_dialogue(gateway)
    assert result["status"] == "transport"
    assert len(gateway.calls) == 2


def test_max_steps_without_idle_is_unsettled() -> None:
    waiting = _probe(sayActive="True", waitingForInput="True")
    advance = {"ok": True, "code": "Ok", "data": {"advanced": "True"}}
    gateway = _Gateway([waiting, advance] * 4)
    result = settle_dialogue(gateway, max_steps=3)
    assert result["status"] == "unsettled"
    assert result["reason"] == "still-active"
