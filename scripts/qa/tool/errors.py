"""Errors for the QA tool integration contracts."""

from __future__ import annotations


class PlanError(ValueError):
    """실행 계획이 필수 필드를 빠뜨리거나 ID가 중복일 때 올린다."""
