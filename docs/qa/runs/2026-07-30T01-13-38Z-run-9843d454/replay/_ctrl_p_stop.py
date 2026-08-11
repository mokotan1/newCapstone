#!/usr/bin/env python3
"""Toggle Unity Play Mode via Ctrl+P to the Editor window (recovery)."""
from __future__ import annotations

import ctypes
import subprocess
import sys
import time
from ctypes import wintypes

user32 = ctypes.windll.user32
kernel32 = ctypes.windll.kernel32

VK_CONTROL = 0x11
VK_P = 0x50
KEYEVENTF_KEYUP = 0x0002
WM_KEYDOWN = 0x0100
WM_KEYUP = 0x0101


def enum_unity_hwnds() -> list[int]:
    hwnds: list[int] = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)
    def callback(hwnd: int, _lp: int) -> bool:
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        title = buf.value
        if "Unity" in title and "newCapstone" in title.replace("\\", "/") or (
            "Unity" in title and "disputatio" in title.lower()
        ):
            hwnds.append(hwnd)
            print(f"FOUND hwnd={hwnd} title={title}")
        elif "Unity" in title:
            hwnds.append(hwnd)
            print(f"CANDIDATE hwnd={hwnd} title={title}")
        return True

    user32.EnumWindows(callback, 0)
    return hwnds


def send_ctrl_p(hwnd: int) -> None:
    user32.ShowWindow(hwnd, 9)  # SW_RESTORE
    user32.SetForegroundWindow(hwnd)
    time.sleep(0.5)
    # keybd_event Ctrl down, P down, P up, Ctrl up
    user32.keybd_event(VK_CONTROL, 0, 0, 0)
    user32.keybd_event(VK_P, 0, 0, 0)
    time.sleep(0.05)
    user32.keybd_event(VK_P, 0, KEYEVENTF_KEYUP, 0)
    user32.keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0)
    print(f"Sent Ctrl+P to hwnd={hwnd}")


def main() -> int:
    hwnds = enum_unity_hwnds()
    if not hwnds:
        print("No Unity window found")
        return 1
    # Prefer disputatio/newCapstone title
    target = hwnds[0]
    for h in hwnds:
        # re-get title
        length = user32.GetWindowTextLengthW(h)
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(h, buf, length + 1)
        t = buf.value.lower()
        if "disputatio" in t or "newcapstone" in t:
            target = h
            break
    send_ctrl_p(target)
    time.sleep(5)
    r = subprocess.run(
        [
            str(__import__("pathlib").Path(__import__("os").environ["LOCALAPPDATA"]) / "unity-cli" / "unity-cli.exe"),
            "--project",
            str(__import__("pathlib").Path("disputatio").resolve()),
            "status",
        ],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=60,
    )
    print((r.stdout or "").split("Update available")[0].strip())
    print((r.stderr or "")[:300])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
