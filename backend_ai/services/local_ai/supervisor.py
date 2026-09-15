"""Owned FastAPI launcher. Unity never holds the Windows Job handle."""

from __future__ import annotations

import argparse
import json
import os
import secrets
import sys
import threading
import time
import uuid
from pathlib import Path

import uvicorn

from services.local_ai.job_object import ensure_kill_on_close_job
from services.local_ai.paths import default_local_ai_dir
from services.local_ai.session_identity import PROTOCOL_VERSION, session_file_path

_START_TIMEOUT_SECONDS = 30


def default_session_dir() -> Path:
    return default_local_ai_dir() / "sessions"


def write_session_file(
    path: Path,
    payload: dict[str, object],
    token: str | None = None,
) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    token_path = path.with_suffix(".token")
    resolved = token
    if not resolved:
        resolved = secrets.token_urlsafe(32)
        token_path.write_text(resolved, encoding="utf-8")
    body = dict(payload)
    body["token_file"] = token_path.name
    path.write_text(json.dumps(body, ensure_ascii=False), encoding="utf-8")
    return resolved


def parent_is_alive(pid: int) -> bool:
    if pid <= 0:
        return False
    if sys.platform != "win32":
        try:
            os.kill(pid, 0)
        except OSError:
            return False
        return True
    import ctypes

    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    handle = kernel32.OpenProcess(0x1000, False, pid)
    if not handle:
        return False
    kernel32.CloseHandle(handle)
    return True


def wait_until_started(server: uvicorn.Server, timeout_seconds: float) -> int:
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if server.started and server.servers:
            sock = server.servers[0].sockets[0]
            return int(sock.getsockname()[1])
        time.sleep(0.05)
    raise TimeoutError("uvicorn_bind_timeout")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Start an owned local FastAPI instance.")
    parser.add_argument("--session-id", required=True)
    parser.add_argument("--session-dir", default=str(default_session_dir()))
    parser.add_argument("--parent-pid", type=int, default=os.getppid())
    parser.add_argument("--parent-start", default="")
    parser.add_argument("--project-id", default="disputatio")
    args = parser.parse_args(argv)

    os.environ["AI_PROVIDER"] = "local"
    ensure_kill_on_close_job()
    session_path = session_file_path(args.session_dir, args.session_id)
    token = write_session_file(
        session_path,
        {
            "protocol_version": PROTOCOL_VERSION,
            "session_id": args.session_id,
            "instance_id": "pending",
            "parent_pid": args.parent_pid,
            "parent_start": args.parent_start,
            "project_id": args.project_id,
            "base_url": "http://127.0.0.1:0/",
            "supervisor_pid": os.getpid(),
            "state": "Starting",
        },
    )
    os.environ["CHAT_API_TOKEN"] = token
    os.environ["LOCAL_AI_CONTROL_TOKEN"] = token
    config = uvicorn.Config(
        "main:app",
        host="127.0.0.1",
        port=0,
        reload=False,
        log_level="warning",
    )
    server = uvicorn.Server(config)
    thread = threading.Thread(target=server.run, name="local-ai-uvicorn", daemon=True)
    thread.start()
    try:
        port = wait_until_started(server, _START_TIMEOUT_SECONDS)
    except TimeoutError:
        server.should_exit = True
        return 3

    instance_id = str(uuid.uuid4())
    os.environ["LOCAL_AI_INSTANCE_ID"] = instance_id
    session_path = session_file_path(args.session_dir, args.session_id)
    write_session_file(
        session_path,
        {
            "protocol_version": PROTOCOL_VERSION,
            "session_id": args.session_id,
            "instance_id": instance_id,
            "parent_pid": args.parent_pid,
            "parent_start": args.parent_start,
            "project_id": args.project_id,
            "supervisor_pid": os.getpid(),
            "base_url": f"http://127.0.0.1:{port}/",
            "state": "Starting",
        },
        token=token,
    )
    while parent_is_alive(args.parent_pid) and thread.is_alive():
        time.sleep(2)
    server.should_exit = True
    thread.join(timeout=10)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
