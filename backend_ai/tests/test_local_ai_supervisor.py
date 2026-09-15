from __future__ import annotations

import json
from pathlib import Path

from services.local_ai.session_identity import (
    PROTOCOL_VERSION,
    build_launch_argv,
    can_reconnect,
    session_file_name,
)


def _identity(**overrides: object) -> dict[str, object]:
    base: dict[str, object] = {
        "protocol_version": PROTOCOL_VERSION,
        "session_id": "sess-1",
        "instance_id": "inst-1",
        "parent_pid": 4242,
        "parent_start": "2026-09-15T01:00:00Z",
        "project_id": "disputatio",
        "base_url": "http://127.0.0.1:8123/",
    }
    base.update(overrides)
    return base


def test_can_reconnect_requires_full_identity() -> None:
    live = _identity()
    assert can_reconnect(claimed=_identity(), live=live) is True


def test_can_reconnect_rejects_port_only_match() -> None:
    live = _identity()
    claimed = _identity(
        session_id="other",
        instance_id="other",
        parent_pid=1,
        parent_start="other",
        base_url=live["base_url"],
    )
    assert can_reconnect(claimed=claimed, live=live) is False


def test_can_reconnect_rejects_pid_only_match() -> None:
    live = _identity()
    claimed = _identity(session_id="other", instance_id="other", parent_start="other")
    assert can_reconnect(claimed=claimed, live=live) is False


def test_can_reconnect_rejects_protocol_mismatch() -> None:
    live = _identity()
    claimed = _identity(protocol_version="0")
    assert can_reconnect(claimed=claimed, live=live) is False


def test_launch_argv_uses_argument_array_without_token() -> None:
    argv = build_launch_argv(
        python_exe=r"C:\Python\python.exe",
        backend_dir=r"D:\Capstone\newCapstone\backend_ai",
        session_id="sess-1",
        session_dir=r"C:\Users\user\AppData\Local\Disputatio\local-ai\sessions",
        parent_pid=4242,
        parent_start="2026-09-15T01:00:00Z",
        project_id="disputatio",
    )
    assert argv[0].endswith("python.exe")
    assert "-m" in argv
    assert "services.local_ai.supervisor" in argv
    joined = " ".join(argv)
    assert "token" not in joined.lower()
    assert "--reload" not in argv


def test_session_file_name_is_stable() -> None:
    assert session_file_name("sess-1") == "sess-1.json"


def test_write_session_file_keeps_token_out_of_payload(tmp_path: Path) -> None:
    from services.local_ai.supervisor import write_session_file

    path = tmp_path / "sess-1.json"
    write_session_file(path, {"session_id": "sess-1", "base_url": "http://127.0.0.1:9/"})
    payload = json.loads(path.read_text(encoding="utf-8"))
    raw = path.read_text(encoding="utf-8")
    assert "token" not in payload
    assert payload["token_file"] == "sess-1.token"
    token = (tmp_path / "sess-1.token").read_text(encoding="utf-8").strip()
    assert token
    assert token not in raw

