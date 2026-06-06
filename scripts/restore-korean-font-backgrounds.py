#!/usr/bin/env python3
"""Restore Image m_Color alpha values cleared by KoreanFontProjectApplier in ce35c632."""

from __future__ import annotations

import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
BASE_COMMIT = "ce35c632^"
TARGET_COMMIT = "ce35c632"


def git_diff() -> str:
    return subprocess.check_output(
        [
            "git",
            "diff",
            BASE_COMMIT,
            TARGET_COMMIT,
            "--",
            "*.unity",
            "*.prefab",
        ],
        cwd=REPO_ROOT,
        text=True,
        errors="replace",
    )


def parse_replacements(diff: str) -> dict[str, list[tuple[str, str]]]:
    """Map repo-relative path -> [(new_line, old_line), ...] for m_Color full lines."""
    by_file: dict[str, list[tuple[str, str]]] = defaultdict(list)
    current_file: str | None = None
    pending_old: str | None = None

    for line in diff.splitlines():
        if line.startswith("diff --git"):
            match = re.search(r"b/(disputatio/.+\.(?:unity|prefab))", line)
            current_file = match.group(1) if match else None
            pending_old = None
            continue

        if current_file is None:
            continue

        if line.startswith("-  m_Color:"):
            pending_old = line[2:]
            continue

        if line.startswith("+  m_Color:") and pending_old:
            old_line = pending_old
            new_line = line[2:]
            old_a = re.search(r"a:\s*([0-9.]+)", old_line)
            new_a = re.search(r"a:\s*([0-9.]+)", new_line)
            if old_a and new_a and float(new_a.group(1)) <= 0.001 and float(old_a.group(1)) > 0.001:
                by_file[current_file].append((new_line, old_line))
            pending_old = None

    return dict(by_file)


def apply_replacements(path: str, pairs: list[tuple[str, str]]) -> int:
    file_path = REPO_ROOT / path
    if not file_path.exists():
        return 0

    text = file_path.read_text(encoding="utf-8")
    original = text
    applied = 0

    for new_line, old_line in pairs:
        if new_line not in text:
            continue
        text = text.replace(new_line, old_line, 1)
        applied += 1

    if text != original:
        file_path.write_text(text, encoding="utf-8", newline="\n")

    return applied


def main() -> int:
    diff = git_diff()
    replacements = parse_replacements(diff)
    if not replacements:
        print("No m_Color alpha restorations found in diff.")
        return 1

    total_applied = 0
    files_changed = 0
    for path in sorted(replacements):
        applied = apply_replacements(path, replacements[path])
        if applied:
            files_changed += 1
            total_applied += applied
            print(f"{path}: restored {applied} background(s)")

    print(f"Done: {total_applied} backgrounds restored across {files_changed} files.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
