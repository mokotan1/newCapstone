"""Run lifecycle: qa_status isolation, recover, screenshot, console — skip CLI when health is down."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from scripts.qa.tool.lifecycle import UnityCliLifecycle
from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE


class _Runner:
    def __init__(self, responses: list[dict[str, Any]] | None = None) -> None:
        self.calls: list[list[str]] = []
        self._responses = list(responses or [])

    def __call__(self, args: list[str]) -> dict[str, Any]:
        self.calls.append(list(args))
        if self._responses:
            return dict(self._responses.pop(0))
        return {"returncode": 0, "stdout": "{}", "stderr": ""}


def test_lifecycle_skips_cli_when_health_is_down(tmp_path: Path) -> None:
    runner = _Runner()
    lifecycle = UnityCliLifecycle(runner=runner, health_probe=lambda: False)
    shot = lifecycle.capture_screenshot(tmp_path / "hop.png")
    console = lifecycle.capture_console()
    recover = lifecycle.recover()
    assert shot["code"] == TRANSPORT_DOWN_CODE
    assert console["code"] == TRANSPORT_DOWN_CODE
    assert recover["code"] == TRANSPORT_DOWN_CODE
    assert runner.calls == []


def test_snapshot_player_store_reads_qa_status_profile_flags() -> None:
    runner = _Runner(
        [
            {
                "returncode": 0,
                "stdout": (
                    '{"code":"Ok","data":{"isQaProfileActive":true,'
                    '"isScenarioRunning":false}}'
                ),
                "stderr": "",
            }
        ]
    )
    lifecycle = UnityCliLifecycle(runner=runner, health_probe=lambda: True)
    store = lifecycle.snapshot_player_store()
    assert store["keys"]["isQaProfileActive"] == "True"
    assert store["keys"]["isScenarioRunning"] == "False"
    assert store["files"] == {}
    assert runner.calls[0][0] == "qa_status"


def test_recover_parses_failure_notes_as_uncertain() -> None:
    runner = _Runner(
        [
            {
                "returncode": 0,
                "stdout": (
                    '{"code":"Ok","message":"Profile recovery threw Exception. | '
                    'Lease: recovery threw Exception."}'
                ),
                "stderr": "",
            }
        ]
    )
    lifecycle = UnityCliLifecycle(runner=runner, health_probe=lambda: True)
    result = lifecycle.recover()
    assert result["ok"] is False
    assert result["uncertain"] is True
    assert runner.calls[0][0] == "qa_recover"


def test_cancel_calls_qa_cancel() -> None:
    runner = _Runner(
        [{"returncode": 0, "stdout": '{"code":"Ok","message":"cancelled"}', "stderr": ""}]
    )
    lifecycle = UnityCliLifecycle(runner=runner, health_probe=lambda: True)
    result = lifecycle.cancel()
    assert result["ok"] is True
    assert runner.calls[0][0] == "qa_cancel"


def test_screenshot_passes_game_view_and_output_path(tmp_path: Path) -> None:
    output = tmp_path / "hop-api.png"
    runner = _Runner([{"returncode": 0, "stdout": str(output), "stderr": ""}])
    lifecycle = UnityCliLifecycle(runner=runner, health_probe=lambda: True)
    result = lifecycle.capture_screenshot(output)
    assert result["ok"] is True
    assert "--view" in runner.calls[0]
    assert "game" in runner.calls[0]
    assert str(output) in runner.calls[0]
