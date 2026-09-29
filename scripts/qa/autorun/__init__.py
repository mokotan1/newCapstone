"""External self-extending QA autorun orchestrator package."""

from __future__ import annotations

from .capability_fixture import (
    PLACE_BOOKMARK_CAPABILITY_ID,
    InMemoryCapabilityRegistry,
    apply_fixture_capability_patch,
)
from .checkpoint import Checkpoint, load_checkpoint, save_checkpoint
from .classify import FailureClass, classify
from .git_isolation import GitIsolationSession, UnownedDirtyChangesError
from .orchestrator import (
    MAX_ATTEMPTS_PER_SIGNATURE,
    AutorunOrchestrator,
    OrchestratorState,
)
from .report import render_report, sanitize_for_report
from .scenario_runner import (
    Scenario,
    ScenarioGateway,
    ScenarioRunner,
    ScenarioValidationError,
    discover_scenarios,
    load_scenario,
    run_scenarios,
)

__all__ = [
    "AutorunOrchestrator",
    "Checkpoint",
    "FailureClass",
    "GitIsolationSession",
    "InMemoryCapabilityRegistry",
    "MAX_ATTEMPTS_PER_SIGNATURE",
    "OrchestratorState",
    "PLACE_BOOKMARK_CAPABILITY_ID",
    "UnownedDirtyChangesError",
    "apply_fixture_capability_patch",
    "classify",
    "load_checkpoint",
    "render_report",
    "sanitize_for_report",
    "save_checkpoint",
    "Scenario",
    "ScenarioGateway",
    "ScenarioRunner",
    "ScenarioValidationError",
    "discover_scenarios",
    "load_scenario",
    "run_scenarios",
]
