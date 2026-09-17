"""Live unity-cli gateway for Hall→Kitchen vertical runs (design AC19)."""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import time
import urllib.error
import urllib.request
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any

CLI_PROJECT = ("--project", "disputatio")
CLI_TIMEOUT_SECONDS = 90
DEFAULT_PROJECT_PATH = str(
    Path(__file__).resolve().parents[3] / "disputatio"
).replace("\\", "/")
REQUIRED_HALL_CAPABILITY_IDS: tuple[str, ...] = (
    "hall.nav.click-kitchen-entry",
    "hall.nav.execute-front",
    "hall.nav.execute-door",
    "hall.nav.reset-to-hall",
    "hall.nav.assert-route",
    "fungus.dialogue.probe",
    "fungus.dialogue.advance",
    "fungus.dialogue.choose",
)


def heartbeat_path(project_path: str) -> Path:
    """unity-cli connector가 쓰는 ~/.unity-cli/instances/<md5>.json 경로를 계산한다."""
    digest = hashlib.md5(project_path.encode("utf-8")).hexdigest()[:16]
    return Path.home() / ".unity-cli" / "instances" / f"{digest}.json"


def parse_heartbeat(payload: Mapping[str, Any]) -> dict[str, Any]:
    """하트비트 JSON을 port/pid/state/listener 스냅샷으로 정규화한다."""
    try:
        port = int(payload.get("port") or 0)
    except (TypeError, ValueError):
        port = 0
    pid = payload.get("pid")
    listener = payload.get("listenerRunning")
    return {
        "port": port,
        "pid": "" if pid is None else str(pid),
        "state": str(payload.get("state") or ""),
        "listenerRunning": None if listener is None else bool(listener),
        "raw": dict(payload),
    }


def wait_until_http_ready(
    *,
    read_heartbeat: Callable[[], Mapping[str, Any]],
    probe_health: Callable[[int], bool],
    sleep: Callable[[float], None],
    timeout_seconds: float,
    interval_seconds: float = 0.5,
) -> dict[str, Any]:
    """Play Mode status가 아니라 GET /health가 200일 때만 CLI를 준비된 것으로 본다."""
    # TODO(AC22): 공식 Pipeline 7800 포트는 이 대기 경로에 아직 없다.
    deadline = time.monotonic() + timeout_seconds
    last_port = 0
    last_state = ""
    while True:
        beat = parse_heartbeat(read_heartbeat())
        port = int(beat.get("port") or 0)
        last_port = port
        last_state = str(beat.get("state") or "")
        listener = beat.get("listenerRunning")
        if listener is False:
            pass
        elif port > 0 and probe_health(port):
            return {
                "ok": True,
                "reasonCode": "ok",
                "port": port,
                "pid": beat.get("pid"),
                "state": last_state,
            }
        if time.monotonic() >= deadline:
            return {
                "ok": False,
                "reasonCode": "http-listener-down",
                "port": last_port,
                "state": last_state,
            }
        sleep(interval_seconds)


def read_heartbeat_file(project_path: str = DEFAULT_PROJECT_PATH) -> dict[str, Any]:
    """디스크 하트비트 파일을 읽는다. 없거나 깨지면 빈 dict다."""
    path = heartbeat_path(project_path)
    if not path.is_file():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    return payload if isinstance(payload, dict) else {}


def probe_health_detail(
    port: int,
    *,
    opener: Callable[..., Any] | None = None,
    timeout: float = 2.0,
) -> dict[str, Any]:
    """GET /health의 HTTP 상태 코드를 남긴다. 연결 실패는 status_code=None이다."""
    url = f"http://127.0.0.1:{port}/health" if port > 0 else ""
    if port <= 0:
        return {"ok": False, "status_code": None, "error": "no-port", "url": url}
    request = urllib.request.Request(url, method="GET")
    open_url = opener or urllib.request.urlopen
    try:
        with open_url(request, timeout=timeout) as response:
            code = int(getattr(response, "status", 0) or 0)
            return {
                "ok": 200 <= code < 300,
                "status_code": code,
                "error": "",
                "url": url,
            }
    except urllib.error.HTTPError as exc:
        code = int(exc.code)
        return {
            "ok": 200 <= code < 300,
            "status_code": code,
            "error": f"http-{code}",
            "url": url,
        }
    except (urllib.error.URLError, TimeoutError, OSError, ValueError) as exc:
        reason = str(getattr(exc, "reason", exc) or exc).lower()
        error = "timeout" if "timed out" in reason or "timeout" in reason else "unreachable"
        return {"ok": False, "status_code": None, "error": error, "url": url}


def probe_health_http(
    port: int,
    *,
    opener: Callable[..., Any] | None = None,
    timeout: float = 2.0,
) -> bool:
    """127.0.0.1:{port}/health 가 2xx인지 본다. CLI 캐시 포트가 아니라 하트비트 포트를 친다."""
    return bool(probe_health_detail(port, opener=opener, timeout=timeout)["ok"])


def format_health_line(detail: Mapping[str, Any]) -> str:
    """상태 CMD/hop 로그에 넣을 GET /health 한 줄."""
    url = str(detail.get("url") or "/health")
    code = detail.get("status_code")
    status = f"HTTP {code}" if isinstance(code, int) else "HTTP ---"
    error = str(detail.get("error") or "").strip()
    if error:
        return f"{status}  GET {url}  ({error})"
    return f"{status}  GET {url}"


def should_fetch_unity_console(health_ok: bool) -> bool:
    """리스너가 죽은 동안 unity-cli console을 치면 90초 hang이 난다."""
    return bool(health_ok)


def parse_status(text: str) -> dict[str, Any]:
    """unity-cli status 텍스트를 재연결 스냅샷(연결 여부·PID)으로 바꾼다."""
    # TODO(AC22): dirtyScene/leaseId는 status 텍스트에 없다. qa_status와 합쳐야 한다.
    connected = any(
        token in text
        for token in ("Unity: ready", "Unity: compiling", "Unity: playing", "Unity: reloading")
    )
    pid = ""
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("PID:"):
            pid = stripped.split(":", 1)[1].strip()
    return {
        "editorConnected": connected,
        "editorPid": pid,
        "raw": text,
    }


def parse_capability_ids(payload: Mapping[str, Any]) -> list[str]:
    """capability.list JSON의 current_capabilities CSV를 id 목록으로 쪼갠다."""
    data = payload.get("data") if isinstance(payload.get("data"), Mapping) else {}
    raw = str(data.get("current_capabilities") or "")
    return [item.strip() for item in raw.split(",") if item.strip()]


def build_live_snapshot(
    status_text: str,
    capability_ids: Sequence[str],
    *,
    lease_id: str = "",
) -> dict[str, Any]:
    """status + 라이브 capability를 preflight가 읽는 스냅샷 형태로 조립한다."""
    parsed = parse_status(status_text)
    pid = str(parsed.get("editorPid") or "")
    return {
        "editorConnected": bool(parsed.get("editorConnected")),
        "projectPath": "disputatio",
        "expectedProjectPath": "disputatio",
        "compiling": "Unity: compiling" in status_text,
        "dirtyScene": False,
        "currentLeaseOwner": "qa-tool",
        "requestedOwner": "qa-tool",
        "requiredCapabilityIds": list(REQUIRED_HALL_CAPABILITY_IDS),
        "liveCapabilityIds": [str(item) for item in capability_ids],
        "editorPid": pid,
        "leaseId": lease_id or pid,
    }


class UnityCliGateway:
    """DeveloperQa를 qa_dev_exec로 호출하는 라이브 게이트웨이. stub이 아니다."""

    kind = "live"

    def __init__(self, runner: Any | None = None, progress: Any | None = None) -> None:
        """실제 CLI 또는 테스트용 runner를 붙인다. progress가 있으면 각 호출을 즉시 로그한다."""
        self._runner = runner or _run_cli
        self._progress = progress
        self.calls: list[dict[str, Any]] = []

    def invoke(self, family: str, name: str, target: str) -> dict[str, Any]:
        """family/name/target 한 건을 qa_dev_exec로 보내고 정규화 JSON을 돌려준다."""
        # TODO(AC19): Play Mode에서 이 호출이 90s timeout이면 exec로 재시도하지 말 것.
        # TODO(AC19): Play Mode 진입 후 health endpoint가 죽으면 Edit Mode에서 호출하거나 Editor를 재시작한다.
        if self._progress is not None:
            self._progress(f"cli {family}.{name} {target or '-'} start")
        args = ["qa_dev_exec", "--family", family, "--name", name]
        if target:
            args.extend(["--target", target])
        completed = self._runner(args)
        payload = _coerce_payload(completed)
        if self._progress is not None:
            code = str(payload.get("code") or payload.get("returncode") or "?")
            scene = ""
            data = payload.get("data")
            if isinstance(data, Mapping):
                scene = str(data.get("activeScene") or "")
            suffix = f" scene={scene}" if scene else ""
            self._progress(f"cli {family}.{name} {target or '-'} {code}{suffix}")
        self.calls.append(
            {
                "family": family,
                "name": name,
                "target": target,
                "payload": payload,
            }
        )
        return payload


def _cli_executable() -> str:
    """로컬에 설치된 unity-cli.exe 경로를 반환한다. cmd.exe 재파싱을 피하기 위함이다."""
    return str(Path(os.environ.get("LOCALAPPDATA", "")) / "unity-cli" / "unity-cli.exe")


def _decode(blob: object) -> str:
    """CLI 바이트를 cp949가 깨지지 않게 UTF-8(replace) 문자열로 디코드한다."""
    if blob is None:
        return ""
    if isinstance(blob, bytes):
        return blob.decode("utf-8", errors="replace")
    return str(blob)


def _run_cli(args: Sequence[str]) -> dict[str, Any]:
    """unity-cli.exe를 disputatio 프로젝트에 대해 한 번 실행한다."""
    # TODO(AC19): timeout 시 Editor stop/qa_recover가 아직 이 함수 안에 없다.
    command = [_cli_executable(), *CLI_PROJECT, *[str(item) for item in args]]
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            check=False,
            timeout=CLI_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as exc:
        return {
            "returncode": 124,
            "stdout": _decode(exc.stdout),
            "stderr": _decode(exc.stderr) or "unity-cli timed out",
        }
    return {
        "returncode": completed.returncode,
        "stdout": _decode(completed.stdout),
        "stderr": _decode(completed.stderr),
    }


def _extract_json_object(text: str) -> Any:
    """stdout에서 첫 JSON 객체를 꺼낸다. 'Update available' 꼬리 문구를 무시한다."""
    start = text.find("{")
    if start < 0:
        return None
    try:
        parsed, _end = json.JSONDecoder().raw_decode(text[start:])
    except json.JSONDecodeError:
        return None
    return parsed


def _coerce_payload(completed: Mapping[str, Any]) -> dict[str, Any]:
    """CLI 종료코드+stdout/stderr를 ok/code/data 계약으로 정규화한다. 명령 성공 ≠ 게임플레이 PASS."""
    stdout = str(completed.get("stdout") or "").strip()
    stderr = str(completed.get("stderr") or "").strip()
    parsed = _extract_json_object(stdout) if stdout else None
    if not isinstance(parsed, dict) and stderr:
        parsed = _extract_json_object(stderr)
    if isinstance(parsed, dict):
        nested = parsed.get("data") if isinstance(parsed.get("data"), dict) else {}
        code = str(parsed.get("code") or "")
        ok = completed.get("returncode") == 0 and code == "Ok"
        return {
            "ok": bool(ok),
            "code": code or "Error",
            "message": parsed.get("message"),
            "data": dict(nested),
            "rawStdout": stdout,
            "rawStderr": stderr,
        }
    return {
        "ok": False,
        "code": "EnvironmentBlocked",
        "message": stdout or stderr or "unity-cli failed",
        "rawStdout": stdout,
        "returncode": completed.get("returncode"),
    }
