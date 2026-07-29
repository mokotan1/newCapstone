"""Autorun report helpers — never emit secrets."""

from __future__ import annotations

from typing import Any, Mapping

_SECRET_KEY_FRAGMENTS = frozenset(
    {
        "api_key",
        "apikey",
        "authorization",
        "password",
        "secret",
        "token",
        "credential",
        "private_key",
    }
)


def _is_secret_key(key: str) -> bool:
    normalized = key.lower().replace("-", "_")
    return any(fragment in normalized for fragment in _SECRET_KEY_FRAGMENTS)


def sanitize_for_report(value: Any) -> Any:
    """Recursively redact secret-looking keys from mappings."""
    if isinstance(value, Mapping):
        cleaned: dict[str, Any] = {}
        for key, item in value.items():
            key_str = str(key)
            if _is_secret_key(key_str):
                cleaned[key_str] = "[REDACTED]"
            else:
                cleaned[key_str] = sanitize_for_report(item)
        return cleaned
    if isinstance(value, list):
        return [sanitize_for_report(item) for item in value]
    if isinstance(value, tuple):
        return tuple(sanitize_for_report(item) for item in value)
    return value


def render_report(run_summary: Mapping[str, Any]) -> str:
    """Render a short markdown report from a sanitized summary."""
    safe = sanitize_for_report(dict(run_summary))
    verdict = safe.get("verdict", "NOT_RUN")
    run_id = safe.get("run_id", "unknown")
    lines = [
        f"# QA Autorun Report — {run_id}",
        "",
        f"- Verdict: `{verdict}`",
        f"- State: `{safe.get('state', 'UNKNOWN')}`",
    ]
    classification = safe.get("classification")
    if classification:
        lines.append(f"- Classification: `{classification}`")
    signature = safe.get("failure_signature")
    if signature:
        lines.append(f"- Failure signature: `{signature}`")
    attempts = safe.get("attempts")
    if attempts is not None:
        lines.append(f"- Attempts: `{attempts}`")
    lines.append("")
    return "\n".join(lines)
