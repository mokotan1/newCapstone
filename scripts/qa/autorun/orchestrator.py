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
    """Explicit-state repair loop; patch wiring is intentionally out of scope."""

    def __init__(self, max_attempts: int = MAX_ATTEMPTS_PER_SIGNATURE) -> None:
        if max_attempts < 1:
            raise ValueError("max_attempts must be >= 1")
        self._max_attempts = max_attempts
        self._state = OrchestratorState.PREFLIGHT
        self._attempts: dict[str, int] = {}

    @property
    def state(self) -> OrchestratorState:
        return self._state

    def start(self) -> OrchestratorState:
        self._state = OrchestratorState.RUNNING
        return self._state

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

        self._state = OrchestratorState.CLASSIFYING
        count = self._attempts.get(signature, 0) + 1
        self._attempts[signature] = count

        if classification == "EnvironmentBlocked":
            self._state = OrchestratorState.BLOCKED
            return self._state

        if count > self._max_attempts:
            self._state = OrchestratorState.BLOCKED
            return self._state

        if classification == "MissingQaCapability":
            self._state = OrchestratorState.PATCHING_QA
            return self._state

        if classification == "ProductDefect":
            self._state = OrchestratorState.PATCHING_PRODUCT
            return self._state

        if classification == "InvalidScenario":
            # Scenario-only fixes are not auto-patched in this skeleton.
            self._state = OrchestratorState.BLOCKED
            return self._state

        raise ValueError(f"unknown classification: {classification}")

    def resume_after_patch(self) -> OrchestratorState:
        if self._state not in {
            OrchestratorState.PATCHING_QA,
            OrchestratorState.PATCHING_PRODUCT,
            OrchestratorState.COMMITTING,
            OrchestratorState.RESUMING,
        }:
            raise RuntimeError(f"cannot resume from state {self._state}")
        self._state = OrchestratorState.RESUMING
        self._state = OrchestratorState.RUNNING
        return self._state
