"""Reconnect confirmation after domain reload (design AC22)."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any


def confirm_reconnect(
    previous: Mapping[str, Any],
    current: Mapping[str, Any],
) -> dict[str, Any]:
    """domain reload 이후 PID/lease가 같은지 확인한다. 불일치면 성공으로 치지 않는다."""
    if not current.get("editorConnected"):
        return {"ok": False, "reasonCode": "editor-disconnected"}
    if str(previous.get("editorPid") or "") != str(current.get("editorPid") or ""):
        return {"ok": False, "reasonCode": "stale-pid"}
    if str(previous.get("leaseId") or "") != str(current.get("leaseId") or ""):
        return {"ok": False, "reasonCode": "stale-lease"}
    return {"ok": True, "reasonCode": "ok"}
