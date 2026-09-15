#!/usr/bin/env python3
"""Emergency: exit Play Mode then qa_recover."""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

UNITY = Path(os.environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"
PROJ = Path("disputatio").resolve()
OWNER = "qa-20260730-replay"


def unity(*args: str, timeout: int = 60) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [str(UNITY), "--project", str(PROJ), *args],
        capture_output=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
    )


def strip(s: str) -> str:
    return (s or "").split("Update available")[0].strip()


def main() -> int:
    print("STATUS:", strip(unity("status").stdout))
    # Exit play mode
    code = (
        "if (UnityEditor.EditorApplication.isPlaying) { "
        "UnityEditor.EditorApplication.isPlaying = false; "
        "return \"stopping\"; } "
        "return \"already-stopped\";"
    )
    try:
        r = unity("exec", code, timeout=90)
        print("EXEC exit play:", strip(r.stdout), strip(r.stderr), "code", r.returncode)
    except subprocess.TimeoutExpired as e:
        print("EXEC timeout:", e)

    import time

    time.sleep(3)
    print("STATUS2:", strip(unity("status", timeout=30).stdout))

    try:
        r = unity(
            "qa_recover",
            "--params",
            json.dumps({"owner_id": OWNER}),
            timeout=90,
        )
        print("RECOVER:", strip(r.stdout), strip(r.stderr), "code", r.returncode)
    except subprocess.TimeoutExpired as e:
        print("RECOVER timeout:", e)

    print("STATUS3:", strip(unity("status", timeout=30).stdout))
    print("QAS:", strip(unity("qa_status", timeout=30).stdout))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
