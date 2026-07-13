#!/usr/bin/env python3
"""Validate Cheshire prompt Resources for ko/ja/en completeness and light Hangul leakage.

Checks:
  - Required stable keys exist and are non-empty UTF-8 for ko, ja, en
  - HintPolicy_* / Fragment_* present in any locale exist in all three, non-empty
  - EN/JA files are not Hangul-dominated (control-tag Hangul is allowed under threshold)

Exit codes: 0 = OK, 1 = validation failure.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

_BACKEND_DIR = Path(__file__).resolve().parent.parent
_REPO_ROOT = _BACKEND_DIR.parent

_DEFAULT_PROMPTS_ROOT = (
    _REPO_ROOT / "disputatio" / "Assets" / "Resources" / "CheshirePrompts"
)

_LOCALES: tuple[str, ...] = ("ko", "ja", "en")

_REQUIRED_KEYS: tuple[str, ...] = (
    "BaseSystem",
    "ChesterVoiceCommon",
    "introPrompt",
    "KitchenPrompt",
    "MainBedroomPrompt",
    "SonRoomPrompt",
    "StudyRoomPrompt",
    "TutorRoomPrompt",
    "WifeRoomPrompt",
    "ParrotPrompt",
)

# Hangul syllables — used for dominance scan on en/ja only.
_HANGUL_RE = re.compile(r"[\uAC00-\uD7A3]")

# Fraction of Hangul characters vs file length above which a file fails.
# Intentional KO control tags in EN/JA (e.g. [시스템: …]) stay well under this.
_DEFAULT_HANGUL_DOMINANCE_RATIO = 0.08


def _prompt_path(root: Path, locale: str, key: str) -> Path:
    return root / locale / f"{key}.txt"


def _list_keys(root: Path, locale: str) -> set[str]:
    locale_dir = root / locale
    if not locale_dir.is_dir():
        return set()
    return {p.stem for p in locale_dir.glob("*.txt")}


def _read_utf8(path: Path) -> tuple[str | None, str | None]:
    """Return (text, error). error is set on failure."""
    try:
        raw = path.read_bytes()
    except OSError as exc:
        return None, f"unreadable: {exc}"
    if len(raw) == 0:
        return None, "empty file"
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        return None, f"not valid UTF-8: {exc}"
    if not text.strip():
        return None, "whitespace-only"
    return text, None


def _hangul_ratio(text: str) -> float:
    if not text:
        return 0.0
    return sum(1 for _ in _HANGUL_RE.finditer(text)) / len(text)


def validate_cheshire_prompts(
    root: Path,
    *,
    hangul_dominance_ratio: float = _DEFAULT_HANGUL_DOMINANCE_RATIO,
) -> list[str]:
    """Return a list of human-readable validation errors (empty = OK)."""
    errors: list[str] = []

    if not root.is_dir():
        return [f"prompts root missing: {root}"]

    for locale in _LOCALES:
        locale_dir = root / locale
        if not locale_dir.is_dir():
            errors.append(f"locale directory missing: {locale_dir}")

    # Required stable keys
    for key in _REQUIRED_KEYS:
        for locale in _LOCALES:
            path = _prompt_path(root, locale, key)
            if not path.is_file():
                errors.append(f"missing required key: {locale}/{key}.txt")
                continue
            _text, err = _read_utf8(path)
            if err:
                errors.append(f"invalid required key {locale}/{key}.txt: {err}")

    # HintPolicy_* / Fragment_* must be mirrored across locales when present
    optional_prefixes = ("HintPolicy_", "Fragment_")
    union_optional: set[str] = set()
    for locale in _LOCALES:
        for key in _list_keys(root, locale):
            if key.startswith(optional_prefixes):
                union_optional.add(key)

    for key in sorted(union_optional):
        for locale in _LOCALES:
            path = _prompt_path(root, locale, key)
            if not path.is_file():
                errors.append(f"missing optional key present elsewhere: {locale}/{key}.txt")
                continue
            _text, err = _read_utf8(path)
            if err:
                errors.append(f"invalid optional key {locale}/{key}.txt: {err}")

    # Light Hangul dominance scan for EN/JA
    for locale in ("en", "ja"):
        for key in sorted(_list_keys(root, locale)):
            path = _prompt_path(root, locale, key)
            text, err = _read_utf8(path)
            if err or text is None:
                continue
            ratio = _hangul_ratio(text)
            if ratio > hangul_dominance_ratio:
                errors.append(
                    f"Hangul-dominated {locale}/{key}.txt: "
                    f"{ratio:.1%} Hangul chars "
                    f"(threshold {hangul_dominance_ratio:.0%}; "
                    f"allow intentional KO control tags only)"
                )

    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Validate CheshirePrompts Resources (ko/ja/en)."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=_DEFAULT_PROMPTS_ROOT,
        help="Path to CheshirePrompts directory",
    )
    parser.add_argument(
        "--hangul-dominance-ratio",
        type=float,
        default=_DEFAULT_HANGUL_DOMINANCE_RATIO,
        help="Max Hangul char fraction for en/ja files (default: 0.08)",
    )
    args = parser.parse_args(argv)

    root = args.root.resolve()
    errs = validate_cheshire_prompts(
        root,
        hangul_dominance_ratio=args.hangul_dominance_ratio,
    )
    if errs:
        print("Validation failed:", file=sys.stderr)
        for e in errs:
            print(f"  - {e}", file=sys.stderr)
        return 1

    key_count = len(_REQUIRED_KEYS)
    print(
        f"OK: {root} - {key_count} required keys x "
        f"{len(_LOCALES)} locales; HintPolicy_/Fragment_ mirrored; "
        f"en/ja Hangul dominance <= {args.hangul_dominance_ratio:.0%}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
