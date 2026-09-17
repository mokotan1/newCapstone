"""Cross-process file lease for the live QA runner.

인메모리 ``preflight.acquire_lease``는 pytest 안에서만 동작한다. 라이브 경로는
``docs/qa/runs/_lease.json``에 owner+pid를 남겨 다른 프로세스가 같은 Editor를
잡지 못하게 한다. 죽은 pid의 리스만 회수하고, 살아 있는 다른 owner는 ownership으로 막는다.
"""

from __future__ import annotations

import json
import os
from collections.abc import Callable
from pathlib import Path
from typing import Any

LEASE_OWNER_DEFAULT = "qa-tool"


def pid_is_alive(pid: int) -> bool:
    """프로세스가 존재하면 True. Windows에서 signal 0 프로브가 실패하면 죽은 것으로 본다."""
    if pid <= 0:
        return False
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False
    return True


def _load_lease(path: Path) -> dict[str, Any] | None:
    """리스 파일을 읽는다. 없거나 JSON이 아니면 None(회수 대상)."""
    if not path.is_file():
        return None
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if not isinstance(payload, dict):
        return None
    return payload


def _write_lease(path: Path, *, owner: str, pid: int, now: str) -> None:
    """owner/pid/acquiredAt을 원자적으로 가까운 방식으로 덮어쓴다."""
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {"owner": owner, "pid": pid, "acquiredAt": now}
    path.write_text(
        json.dumps(payload, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


def acquire_file_lease(
    path: Path,
    *,
    owner: str,
    pid: int,
    pid_alive: Callable[[int], bool],
    now: str,
) -> dict[str, Any]:
    """파일을 한 owner만 갖게 한다. 다른 살아 있는 owner는 ownership으로 막는다.

    입력: 리스 경로, 요청 owner/pid, pid 생존 검사, 시각 문자열.
    출력: executionStatus acquired|blocked. 죽은 pid를 덮어쓰면 recoveredFrom을 남긴다.
    """
    existing = _load_lease(path)
    recovered_from: dict[str, Any] | None = None
    if existing is None and path.is_file():
        recovered_from = {"owner": "", "pid": None}
    elif existing is not None:
        current_owner = str(existing.get("owner") or "")
        try:
            current_pid = int(existing.get("pid"))
        except (TypeError, ValueError):
            current_pid = 0
        same_owner = current_owner == owner
        alive = pid_alive(current_pid) if current_pid else False
        if current_owner and not same_owner and alive:
            return {
                "executionStatus": "blocked",
                "reasonCode": "ownership",
                "owner": current_owner,
            }
        if current_owner and not same_owner:
            recovered_from = {"owner": current_owner, "pid": current_pid or None}
    _write_lease(path, owner=owner, pid=pid, now=now)
    result: dict[str, Any] = {
        "executionStatus": "acquired",
        "reasonCode": "ok",
        "owner": owner,
        "pid": pid,
        "acquiredAt": now,
    }
    if recovered_from is not None:
        result["recoveredFrom"] = recovered_from
    return result


def release_file_lease(path: Path, *, owner: str) -> bool:
    """자기 owner의 리스만 삭제한다. 다른 owner 파일이면 False."""
    existing = _load_lease(path)
    if existing is None:
        return False
    if str(existing.get("owner") or "") != owner:
        return False
    try:
        path.unlink()
    except OSError:
        return False
    return True
