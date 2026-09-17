"""Player save and settings isolation (design AC05)."""

from __future__ import annotations

from collections.abc import Mapping
from typing import Any

ALLOWED_KEY_PREFIXES: tuple[str, ...] = ("qa.",)
ALLOWED_FILE_PREFIXES: tuple[str, ...] = ("docs/qa/runs/",)


def _is_allowed(name: str, prefixes: tuple[str, ...]) -> bool:
    """이 이름 변경이 QA 허용 prefix 아래인지 본다."""
    return any(name.startswith(prefix) for prefix in prefixes)


def _changed_names(
    before: Mapping[str, Any],
    after: Mapping[str, Any],
    prefixes: tuple[str, ...],
) -> list[str]:
    """허용 prefix가 아닌 키/파일 중 값이 달라진 이름을 모은다."""
    names = set(before) | set(after)
    changed: list[str] = []
    for name in sorted(names):
        if _is_allowed(name, prefixes):
            continue
        if before.get(name) != after.get(name):
            changed.append(name)
    return changed


def compare_player_store(
    before: Mapping[str, Any],
    after: Mapping[str, Any],
) -> dict[str, Any]:
    """일반 세이브/설정이 QA 전후 같은지 본다. qa. prefix와 docs/qa/runs 만 달라도 된다."""
    changed_keys = _changed_names(
        dict(before.get("keys") or {}),
        dict(after.get("keys") or {}),
        ALLOWED_KEY_PREFIXES,
    )
    changed_files = _changed_names(
        dict(before.get("files") or {}),
        dict(after.get("files") or {}),
        ALLOWED_FILE_PREFIXES,
    )
    preserved = not changed_keys and not changed_files
    reason_codes = [] if preserved else ["unauthorized-player-change"]
    return {
        "preserved": preserved,
        "reasonCodes": reason_codes,
        "changedKeys": changed_keys,
        "changedFiles": changed_files,
    }
