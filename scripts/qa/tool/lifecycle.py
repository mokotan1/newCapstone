"""Live Editor lifecycle: isolation snapshot, cancel, recover, screenshot, console.

라이브 경로만 이 모듈을 쓴다. HTTP listener가 죽어 있으면 CLI를 띄우지 않고
TransportDown을 돌려 90초 timeout을 막는다.
"""

from __future__ import annotations

import json
from collections.abc import Callable, Mapping
from pathlib import Path
from typing import Any

from scripts.qa.tool.live import _extract_json_object
from scripts.qa.tool.transport import TRANSPORT_DOWN_CODE

_THREW_MARKERS = ("threw", "failed")


def cleanup_status_from_recover(result: Mapping[str, Any]) -> str:
    """qa_recover 응답을 restored/failed/uncertain 문자열로 정규화한다."""
    if str(result.get("code") or "") == TRANSPORT_DOWN_CODE:
        return "uncertain"
    if result.get("uncertain"):
        return "uncertain"
    if result.get("ok"):
        return "restored"
    return "failed"


def _profile_store(payload: Mapping[str, Any]) -> dict[str, Any]:
    """qa_status JSON에서 프로필 플래그만 격리 대조 키로 남긴다."""
    parsed = payload
    data = parsed.get("data") if isinstance(parsed.get("data"), Mapping) else parsed
    if not isinstance(data, Mapping):
        data = {}
    return {
        "keys": {
            "isQaProfileActive": str(data.get("isQaProfileActive")),
            "isScenarioRunning": str(data.get("isScenarioRunning")),
        },
        "files": {},
    }


def _transport_down(reason: str) -> dict[str, Any]:
    """health 실패로 CLI를 생략했을 때의 응답."""
    return {
        "ok": False,
        "code": TRANSPORT_DOWN_CODE,
        "refused": True,
        "reason": reason,
        "uncertain": True,
    }


class UnityCliLifecycle:
    """unity-cli qa_status/qa_cancel/qa_recover/screenshot/console를 감싼다."""

    def __init__(
        self,
        *,
        runner: Callable[[list[str]], Mapping[str, Any]],
        health_probe: Callable[[], bool],
    ) -> None:
        """runner는 _run_cli 계약(args → returncode/stdout/stderr). health가 False면 호출하지 않는다."""
        self._runner = runner
        self._health_probe = health_probe

    def _run(self, args: list[str]) -> dict[str, Any]:
        """health가 살아 있을 때만 CLI를 실행하고 stdout JSON을 정규화한다."""
        if not self._health_probe():
            return _transport_down("health-down")
        completed = dict(self._runner(args))
        stdout = str(completed.get("stdout") or "")
        parsed = _extract_json_object(stdout) if stdout else None
        payload: dict[str, Any] = {
            "ok": completed.get("returncode") == 0,
            "returncode": completed.get("returncode"),
            "stdout": stdout,
            "stderr": completed.get("stderr"),
        }
        if isinstance(parsed, dict):
            payload["code"] = str(parsed.get("code") or "Ok")
            payload["message"] = parsed.get("message")
            payload["data"] = parsed.get("data") if isinstance(parsed.get("data"), dict) else parsed
            if payload["code"] != "Ok":
                payload["ok"] = False
        else:
            payload["code"] = "Ok" if payload["ok"] else "Error"
            payload["message"] = stdout
        return payload

    def snapshot_player_store(self) -> dict[str, Any]:
        """qa_status 프로필 플래그를 isolation 대조용 store로 반환한다."""
        payload = self._run(["qa_status"])
        if payload.get("code") == TRANSPORT_DOWN_CODE:
            return {"keys": {}, "files": {}, "transportDown": True}
        source = payload.get("data") if isinstance(payload.get("data"), Mapping) else payload
        return _profile_store(source if isinstance(source, Mapping) else {})

    def recover(self) -> dict[str, Any]:
        """qa_recover를 호출한다. 메시지에 threw/failed가 있으면 uncertain이다."""
        payload = self._run(["qa_recover"])
        if payload.get("code") == TRANSPORT_DOWN_CODE:
            return payload
        message = str(payload.get("message") or payload.get("stdout") or "").lower()
        threw = any(marker in message for marker in _THREW_MARKERS)
        payload["uncertain"] = threw
        if threw:
            payload["ok"] = False
        return payload

    def cancel(self) -> dict[str, Any]:
        """활성 QA run이 있으면 qa_cancel을 보낸다."""
        return self._run(["qa_cancel"])

    def capture_screenshot(self, output_path: Path) -> dict[str, Any]:
        """Game View PNG를 output_path에 저장하라고 unity-cli에 요청한다."""
        payload = self._run(
            ["screenshot", "--view", "game", "--output_path", str(output_path)]
        )
        payload["path"] = str(output_path)
        return payload

    def capture_console(self) -> dict[str, Any]:
        """error/warning 콘솔 엔트리를 fingerprint 목록으로 모은다."""
        payload = self._run(["console", "--type", "error,warning", "--lines", "80"])
        if payload.get("code") == TRANSPORT_DOWN_CODE:
            return payload
        stdout = str(payload.get("stdout") or "").strip()
        entries: list[dict[str, Any]] = []
        if stdout and stdout not in {"[]"}:
            start = stdout.find("[")
            parsed = None
            if start >= 0:
                try:
                    parsed = json.loads(stdout[start:])
                except json.JSONDecodeError:
                    parsed = None
            if isinstance(parsed, list):
                for item in parsed:
                    blob = str(item)
                    entries.append({"fingerprint": blob, "message": blob, "relatedness": "unclassified"})
            else:
                entries.append(
                    {"fingerprint": stdout, "message": stdout, "relatedness": "unclassified"}
                )
        payload["ok"] = True
        payload["entries"] = entries
        return payload
