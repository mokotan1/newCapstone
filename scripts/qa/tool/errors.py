"""Errors for the QA tool integration contracts."""

from __future__ import annotations


class PlanError(ValueError):
    """Raised when a QA execution plan is missing required fields or has duplicate IDs."""
