"""Autorun state machine skeleton with per-signature retry budget (design §7)."""

from __future__ import annotations

from enum import Enum
from typing import Any, Mapping


MAX_ATTEMPTS_PER_SIGNATURE = 3


class OrchestratorState(str, Enum):
    PREFLIGHT = "PREFLIGHT"
    RUNNING = "RUNNING"
    CLASSIFYING = "CLASSIFYING"
    PATCHING_QA = "PATCHING_QA"
    PATCHING_PRODUCT = "PATCHING_PRODUCT"
    BLOCKED = "BLOCKED"
    COMPILING = "COMPILING"
    FOCUSED_TEST = "FOCUSED_TEST"
    REGRESSION_TEST = "REGRESSION_TEST"
    COMMITTING = "COMMITTING"
    RESUMING = "RESUMING"
    PASS = "PASS"
    FAIL = "FAIL"


class AutorunOrchestrator:
    """Explicit-state repair loop; patch apply is supplied by callers/fixtures."""

    def __init__(self, max_attempts: int = MAX_ATTEMPTS_PER_SIGNATURE) -> None:
        if max_attempts < 1:
            raise ValueError("max_attempts must be >= 1")
        self._max_attempts = max_attempts
        self._state = OrchestratorState.PREFLIGHT
        self._attempts: dict[str, int] = {}
        self._transitions: list[OrchestratorState] = [OrchestratorState.PREFLIGHT]

    @property
    def state(self) -> OrchestratorState:
        return self._state

    @property
    def transitions(self) -> list[OrchestratorState]:
        """Ordered state visits (including PREFLIGHT); used by E2E assertions."""
        return list(self._transitions)

    def _set_state(self, state: OrchestratorState) -> OrchestratorState:
        self._state = state
        self._transitions.append(state)
        return self._state

    def start(self) -> OrchestratorState:
        return self._set_state(OrchestratorState.RUNNING)

    def attempt_count(self, signature: str) -> int:
        return self._attempts.get(signature, 0)

    def normalize_failure_signature(self, evidence: Mapping[str, Any]) -> str:
        """Build a stable signature from classification-relevant fields only."""
        result_code = str(evidence.get("result_code", "Unknown"))
        parts = [result_code]
        for key in (
            "missing_capability_id",
            "missing_probe_id",
            "missing_preset_id",
            "capability_id",
            "assertion_id",
            "scene",
        ):
            value = evidence.get(key)
            if value:
                parts.append(f"{key}={value}")
        return "|".join(parts)

    def handle_failure(self, classification: str, signature: str) -> OrchestratorState:
        if not signature.strip():
            raise ValueError("signature must be non-empty")

        self._set_state(OrchestratorState.CLASSIFYING)
        count = self._attempts.get(signature, 0) + 1
        self._attempts[signature] = count

        if classification == "EnvironmentBlocked":
            return self._set_state(OrchestratorState.BLOCKED)

        if count > self._max_attempts:
            return self._set_state(OrchestratorState.BLOCKED)

        if classification == "MissingQaCapability":
            return self._set_state(OrchestratorState.PATCHING_QA)

        if classification == "ProductDefect":
            return self._set_state(OrchestratorState.PATCHING_PRODUCT)

        if classification == "InvalidScenario":
            # Scenario-only fixes are not auto-patched in this skeleton.
            return self._set_state(OrchestratorState.BLOCKED)

        raise ValueError(f"unknown classification: {classification}")

    def begin_compile(self) -> OrchestratorState:
        if self._state not in {
            OrchestratorState.PATCHING_QA,
            OrchestratorState.PATCHING_PRODUCT,
        }:
            raise RuntimeError(f"cannot begin compile from state {self._state}")
        return self._set_state(OrchestratorState.COMPILING)

    def begin_focused_test(self) -> OrchestratorState:
        if self._state != OrchestratorState.COMPILING:
            raise RuntimeError(f"cannot begin focused test from state {self._state}")
        return self._set_state(OrchestratorState.FOCUSED_TEST)

    def complete_focused_test(self, *, passed: bool) -> OrchestratorState:
        if self._state != OrchestratorState.FOCUSED_TEST:
            raise RuntimeError(f"cannot complete focused test from state {self._state}")
        if not passed:
            return self._set_state(OrchestratorState.FAIL)
        return self._set_state(OrchestratorState.REGRESSION_TEST)

    def complete_regression_test(self, *, passed: bool) -> OrchestratorState:
        if self._state != OrchestratorState.REGRESSION_TEST:
            raise RuntimeError(
                f"cannot complete regression test from state {self._state}"
            )
        if not passed:
            return self._set_state(OrchestratorState.FAIL)
        return self._set_state(OrchestratorState.COMMITTING)

    def resume_after_patch(self) -> OrchestratorState:
        if self._state not in {
            OrchestratorState.PATCHING_QA,
            OrchestratorState.PATCHING_PRODUCT,
            OrchestratorState.COMMITTING,
            OrchestratorState.RESUMING,
        }:
            raise RuntimeError(f"cannot resume from state {self._state}")
        self._set_state(OrchestratorState.RESUMING)
        return self._set_state(OrchestratorState.RUNNING)

    def mark_pass(self) -> OrchestratorState:
        if self._state != OrchestratorState.RUNNING:
            raise RuntimeError(f"cannot mark PASS from state {self._state}")
        return self._set_state(OrchestratorState.PASS)

    def mark_fail(self) -> OrchestratorState:
        if self._state not in {
            OrchestratorState.RUNNING,
            OrchestratorState.FOCUSED_TEST,
            OrchestratorState.REGRESSION_TEST,
        }:
            raise RuntimeError(f"cannot mark FAIL from state {self._state}")
        return self._set_state(OrchestratorState.FAIL)
