"""Cross-process file lease: one live owner, dead-pid recovery, explicit release."""

from __future__ import annotations

import json
from pathlib import Path

from scripts.qa.tool.lease import acquire_file_lease, release_file_lease


def test_acquire_writes_owner_and_pid(tmp_path: Path) -> None:
    path = tmp_path / "_lease.json"
    result = acquire_file_lease(path, owner="qa-tool", pid=4242, pid_alive=lambda _pid: True, now="t0")
    assert result["executionStatus"] == "acquired"
    saved = json.loads(path.read_text(encoding="utf-8"))
    assert saved["owner"] == "qa-tool"
    assert saved["pid"] == 4242
    assert saved["acquiredAt"] == "t0"


def test_live_other_owner_blocks_with_ownership(tmp_path: Path) -> None:
    path = tmp_path / "_lease.json"
    acquire_file_lease(path, owner="qa-playtester", pid=1, pid_alive=lambda _pid: True, now="t0")
    result = acquire_file_lease(path, owner="qa-tool", pid=2, pid_alive=lambda _pid: True, now="t1")
    assert result["executionStatus"] == "blocked"
    assert result["reasonCode"] == "ownership"
    assert result["owner"] == "qa-playtester"
    assert json.loads(path.read_text(encoding="utf-8"))["owner"] == "qa-playtester"


def test_dead_pid_lease_is_recovered_and_reported(tmp_path: Path) -> None:
    path = tmp_path / "_lease.json"
    acquire_file_lease(path, owner="qa-playtester", pid=1, pid_alive=lambda _pid: True, now="t0")
    result = acquire_file_lease(path, owner="qa-tool", pid=2, pid_alive=lambda pid: pid != 1, now="t1")
    assert result["executionStatus"] == "acquired"
    assert result["recoveredFrom"] == {"owner": "qa-playtester", "pid": 1}
    assert json.loads(path.read_text(encoding="utf-8"))["owner"] == "qa-tool"


def test_release_only_removes_own_lease(tmp_path: Path) -> None:
    path = tmp_path / "_lease.json"
    acquire_file_lease(path, owner="qa-tool", pid=2, pid_alive=lambda _pid: True, now="t0")
    assert release_file_lease(path, owner="qa-playtester") is False
    assert path.is_file()
    assert release_file_lease(path, owner="qa-tool") is True
    assert not path.exists()


def test_corrupt_lease_file_is_treated_as_recoverable(tmp_path: Path) -> None:
    path = tmp_path / "_lease.json"
    path.write_text("{not json", encoding="utf-8")
    result = acquire_file_lease(path, owner="qa-tool", pid=2, pid_alive=lambda _pid: True, now="t0")
    assert result["executionStatus"] == "acquired"
    assert result["recoveredFrom"] == {"owner": "", "pid": None}
