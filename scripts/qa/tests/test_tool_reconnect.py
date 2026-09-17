"""Reconnect after domain reload (design AC22)."""

from __future__ import annotations

from scripts.qa.tool.reconnect import confirm_reconnect


def test_stale_pid_is_not_success() -> None:
    result = confirm_reconnect(
        previous={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
        current={"editorPid": 222, "leaseId": "lease-a", "editorConnected": True},
    )
    assert result["ok"] is False
    assert result["reasonCode"] == "stale-pid"


def test_stale_lease_is_not_success() -> None:
    result = confirm_reconnect(
        previous={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
        current={"editorPid": 111, "leaseId": "lease-b", "editorConnected": True},
    )
    assert result["ok"] is False
    assert result["reasonCode"] == "stale-lease"


def test_matching_pid_and_lease_allows_continue() -> None:
    result = confirm_reconnect(
        previous={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
        current={"editorPid": 111, "leaseId": "lease-a", "editorConnected": True},
    )
    assert result["ok"] is True
    assert result["reasonCode"] == "ok"
