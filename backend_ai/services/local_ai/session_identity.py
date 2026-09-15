from __future__ import annotations

from pathlib import Path

PROTOCOL_VERSION = "1"
SUPERVISOR_MODULE = "services.local_ai.supervisor"


def can_reconnect(*, claimed: dict[str, object], live: dict[str, object]) -> bool:
    required = (
        "protocol_version",
        "session_id",
        "instance_id",
        "parent_pid",
        "parent_start",
        "project_id",
    )
    for key in required:
        if claimed.get(key) != live.get(key):
            return False
    return True


def session_file_name(session_id: str) -> str:
    return f"{session_id}.json"


def session_file_path(session_dir: str | Path, session_id: str) -> Path:
    return Path(session_dir) / session_file_name(session_id)


def build_launch_argv(
    *,
    python_exe: str,
    backend_dir: str,
    session_id: str,
    session_dir: str,
    parent_pid: int,
    parent_start: str,
    project_id: str,
) -> list[str]:
    del backend_dir
    return [
        python_exe,
        "-m",
        SUPERVISOR_MODULE,
        "--session-id",
        session_id,
        "--session-dir",
        session_dir,
        "--parent-pid",
        str(parent_pid),
        "--parent-start",
        parent_start,
        "--project-id",
        project_id,
    ]
