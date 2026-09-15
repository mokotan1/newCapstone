#!/usr/bin/env python3
"""Relaunch Unity Editor for QA recovery after hung connector."""
from __future__ import annotations

import os
import subprocess
import sys
import time
from pathlib import Path

PROJ = Path(r"C:\Users\user\Documents\GitHub\newCapstone\disputatio")
LOG = Path(
    r"C:\Users\user\Documents\GitHub\newCapstone\docs\qa\runs\2026-07-30T01-13-38Z-run-9843d454\replay\unity-relaunch.log"
)
UNITY_CLI = Path(os.environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"


def find_unity_exe() -> Path | None:
    # Prefer running process path
    try:
        r = subprocess.run(
            [
                "powershell",
                "-NoProfile",
                "-Command",
                "(Get-Process -Name Unity -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Path)",
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=30,
        )
        p = (r.stdout or "").strip()
        if p and Path(p).exists():
            return Path(p)
    except Exception:
        pass
    candidates = [
        Path(r"C:\Program Files\Unity\Hub\Editor\6000.0.36f1\Editor\Unity.exe"),
        Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
        / "Unity"
        / "Hub"
        / "Editor"
        / "6000.0.36f1"
        / "Editor"
        / "Unity.exe",
    ]
    hub = Path(r"C:\Program Files\Unity\Hub\Editor")
    if hub.exists():
        for d in sorted(hub.iterdir(), reverse=True):
            exe = d / "Editor" / "Unity.exe"
            if exe.exists():
                candidates.append(exe)
    for c in candidates:
        if c.exists():
            return c
    return None


def kill_unity() -> None:
    subprocess.run(
        ["taskkill", "/IM", "Unity.exe", "/F"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    time.sleep(5)


def wait_ready(max_s: int = 300) -> bool:
    deadline = time.time() + max_s
    while time.time() < deadline:
        try:
            r = subprocess.run(
                [
                    str(UNITY_CLI),
                    "--ignore-version-mismatch",
                    "--project",
                    str(PROJ),
                    "status",
                ],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=30,
            )
            out = (r.stdout or "").split("Update available")[0].strip()
            line = out.splitlines()[0] if out else ""
            print(f"status: {line}", flush=True)
            if line.startswith("Unity: ready"):
                # probe qa_status
                r2 = subprocess.run(
                    [
                        str(UNITY_CLI),
                        "--ignore-version-mismatch",
                        "--project",
                        str(PROJ),
                        "qa_status",
                    ],
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=60,
                )
                body = (r2.stdout or "").split("Update available")[0].strip()
                print(f"qa_status: {body[:300]}", flush=True)
                if "isScenarioRunning" in body:
                    return True
                print(f"qa_status stderr: {(r2.stderr or '')[:200]}", flush=True)
        except Exception as e:
            print(f"wait err: {e}", flush=True)
        time.sleep(8)
    return False


def main() -> int:
    exe = find_unity_exe()
    print(f"Unity exe: {exe}", flush=True)
    if not exe:
        return 1
    print("Killing Unity...", flush=True)
    kill_unity()
    LOG.parent.mkdir(parents=True, exist_ok=True)
    print("Launching...", flush=True)
    subprocess.Popen(
        [str(exe), "-projectPath", str(PROJ), "-logFile", str(LOG)],
        cwd=str(PROJ),
    )
    print("Waiting for ready + qa_status...", flush=True)
    ok = wait_ready(420)
    print("READY" if ok else "NOT READY", flush=True)
    return 0 if ok else 2


if __name__ == "__main__":
    raise SystemExit(main())
