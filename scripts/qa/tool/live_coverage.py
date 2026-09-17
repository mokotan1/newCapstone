"""Live coverage for remaining 1단계 ACs. Kitchen arrival is a separate vertical run."""

from __future__ import annotations

import json
import sys
import time
from collections.abc import Mapping
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from scripts.qa.tool.console import classify_console_delta
from scripts.qa.tool.context import evaluate_reuse, read_git_worktree
from scripts.qa.tool.defect import build_defect_packet
from scripts.qa.tool.isolation import compare_player_store
from scripts.qa.tool.live import (
    UnityCliGateway,
    _extract_json_object,
    _run_cli,
    build_live_snapshot,
    parse_capability_ids,
    parse_status,
    probe_health_http,
    read_heartbeat_file,
    wait_until_http_ready,
)
from scripts.qa.tool.preflight import evaluate_injected_preflight_matrix


def _utc_root(suffix: str) -> Path:
    """UTC 스탬프 커버리지 런 디렉터리를 만든다."""
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%SZ")
    return Path("docs/qa/runs") / f"{stamp}-{suffix}"


def _parse_console_entries(stdout: str) -> list[dict[str, Any]]:
    """unity-cli console 출력을 fingerprint 엔트리로 바꾼다. JSON 배열이면 그대로 쓴다."""
    text = stdout.strip()
    if not text:
        return []
    start = text.find("[")
    if start >= 0:
        try:
            parsed = json.loads(text[start:])
        except json.JSONDecodeError:
            parsed = None
        if isinstance(parsed, list):
            entries: list[dict[str, Any]] = []
            for item in parsed:
                if isinstance(item, Mapping):
                    entries.append(dict(item))
                else:
                    blob = str(item)
                    entries.append({"fingerprint": blob, "message": blob, "relatedness": "unclassified"})
            return entries
    if text in {"[]", ""}:
        return []
    return [{"fingerprint": text, "message": text, "relatedness": "unclassified"}]


def collect_live_coverage(run_root: Path) -> dict[str, Any]:
    """Editor가 있으면 qa_status/console/recover와 preflight 주입을 한 번에 남긴다."""
    # TODO(AC05): PlayerPrefs 원본 dump가 없어 qa_status 프로필 플래그만 대조한다.
    # TODO(AC23): 이 수집기는 featureVerified를 True로 올리면 안 된다.
    run_root.mkdir(parents=True, exist_ok=True)
    status = _run_cli(["status"])
    status_text = str(status.get("stdout") or "")
    parsed_status = parse_status(status_text)
    heartbeat = read_heartbeat_file()
    health = wait_until_http_ready(
        read_heartbeat=read_heartbeat_file,
        probe_health=probe_health_http,
        sleep=time.sleep,
        timeout_seconds=15.0,
        interval_seconds=0.5,
    )
    qa_status = _run_cli(["qa_status"])
    qa_cancel = _run_cli(["qa_cancel"])
    console = _run_cli(["console", "--type", "error,warning", "--lines", "80"])
    qa_recover = _run_cli(["qa_recover"])
    qa_status_after = _run_cli(["qa_status"])
    gateway = UnityCliGateway()
    caps = gateway.invoke("capability", "list", "")
    snapshot = build_live_snapshot(
        status_text,
        parse_capability_ids(caps),
        lease_id=str(parsed_status.get("editorPid") or ""),
    )
    matrix = evaluate_injected_preflight_matrix(snapshot)
    git_first = read_git_worktree(ROOT, ["scripts/qa/tool/", "disputatio/Assets/mokotan/mokotan/script/QA/"])
    git_second = read_git_worktree(ROOT, ["scripts/qa/tool/", "disputatio/Assets/mokotan/mokotan/script/QA/"])
    reuse = evaluate_reuse(git_first, git_second)
    console_class = classify_console_delta(
        baseline_fingerprints=[],
        collected=True,
        entries=_parse_console_entries(str(console.get("stdout") or "")),
    )
    before_profile = _extract_profile_flag(qa_status)
    after_profile = _extract_profile_flag(qa_status_after)
    isolation = compare_player_store(
        {"keys": before_profile, "files": {}},
        {"keys": after_profile, "files": {}},
    )
    defect = build_defect_packet(
        {
            "requirementId": "AC07",
            "environment": {
                "editorPid": parsed_status.get("editorPid"),
                "health": health,
            },
            "reproduction": [
                "Open Hall_playerble",
                "Enter Play Mode",
                "Wait for HTTP health on heartbeat port",
                "Run hall.nav hops then assert-route",
            ],
            "expected": "Kitchen arrival, transition finished, input gate open",
            "actual": "Coverage collector does not treat this as Kitchen PASS",
            "evidenceIds": [run_root.name],
            "productTreeBefore": "unchanged",
            "productTreeAfter": "unchanged",
        }
    )
    payload = {
        "featureVerified": False,
        "review": {"status": "missing"},
        "status": parsed_status,
        "heartbeat": heartbeat,
        "health": health,
        "qaStatus": _coerce_cli(qa_status),
        "qaCancel": _coerce_cli(qa_cancel),
        "qaRecover": _coerce_cli(qa_recover),
        "qaStatusAfterRecover": _coerce_cli(qa_status_after),
        "preflightMatrix": matrix,
        "git": git_first,
        "reuse": reuse,
        "consoleClassification": console_class,
        "isolation": isolation,
        "defect": defect,
        "capabilities": caps,
        "reconnectSamePid": {
            "statusPid": parsed_status.get("editorPid"),
            "heartbeatPid": str(heartbeat.get("pid") or ""),
            "healthOk": bool(health.get("ok")),
        },
    }
    (run_root / "coverage.json").write_text(
        json.dumps(payload, indent=2, sort_keys=True, default=str) + "\n",
        encoding="utf-8",
    )
    return payload


def _extract_profile_flag(completed: dict[str, Any]) -> dict[str, str]:
    """qa_status JSON에서 프로필 활성 플래그만 키로 남긴다."""
    parsed = _extract_json_object(str(completed.get("stdout") or ""))
    data = parsed.get("data") if isinstance(parsed, dict) else {}
    if not isinstance(data, dict):
        data = parsed if isinstance(parsed, dict) else {}
    return {
        "isQaProfileActive": str(data.get("isQaProfileActive")),
        "isScenarioRunning": str(data.get("isScenarioRunning")),
    }


def _coerce_cli(completed: dict[str, Any]) -> dict[str, Any]:
    """CLI 원본 stdout/stderr/returncode를 커버리지 파일에 남긴다."""
    return {
        "returncode": completed.get("returncode"),
        "stdout": completed.get("stdout"),
        "stderr": completed.get("stderr"),
    }


def main() -> None:
    """라이브 커버리지를 docs/qa/runs에 쓰고 health 실패여도 Kitchen PASS로 바꾸지 않는다."""
    root = _utc_root("live-coverage")
    payload = collect_live_coverage(root)
    print("coverage", root)
    print("health", payload.get("health"))
    print("featureVerified", payload.get("featureVerified"))
    raise SystemExit(0)


if __name__ == "__main__":
    main()
