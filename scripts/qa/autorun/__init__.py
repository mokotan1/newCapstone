"""External self-extending QA autorun orchestrator package."""

from __future__ import annotations

from .checkpoint import Checkpoint, load_checkpoint, save_checkpoint
from .classify import FailureClass, classify
from .git_isolation import GitIsolationSession, UnownedDirtyChangesError
from .orchestrator import (
    MAX_ATTEMPTS_PER_SIGNATURE,
    AutorunOrchestrator,
    OrchestratorState,
)
from .report import render_report, sanitize_for_report

__all__ = [
    "AutorunOrchestrator",
    "Checkpoint",
    "FailureClass",
    "GitIsolationSession",
    "MAX_ATTEMPTS_PER_SIGNATURE",
    "OrchestratorState",
    "UnownedDirtyChangesError",
    "classify",
    "load_checkpoint",
    "render_report",
    "sanitize_for_report",
    "save_checkpoint",
]
