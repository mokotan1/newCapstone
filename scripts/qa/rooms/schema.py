"""Validators for room manifests, scenarios, and transitions."""

from __future__ import annotations

from typing import Any, Iterable, Mapping

ALLOWED_IMPLEMENTATION_STATUSES: frozenset[str] = frozenset(
    {
        "IMPLEMENTED",
        "PARTIAL",
        "NOT_IMPLEMENTED",
        "SPEC_MISMATCH",
    }
)

ALLOWED_SCENARIO_TIERS: frozenset[str] = frozenset(
    {"smoke", "happy-path", "guard"}
)

_REQUIRED_MANIFEST_KEYS: frozenset[str] = frozenset(
    {
        "schemaVersion",
        "roomId",
        "areaId",
        "unityScenes",
        "implementationStatus",
        "entryPreset",
        "requiredCapabilities",
        "scenarios",
        "exitContract",
    }
)

_REQUIRED_TRANSITION_KEYS: frozenset[str] = frozenset(
    {
        "schemaVersion",
        "id",
        "sourceRegion",
        "destinationRegion",
        "prerequisites",
        "lockedAssertions",
        "sourceExitContract",
        "destinationEntryContract",
        "checkpointContract",
    }
)


class SchemaError(ValueError):
    """Raised when a room-by-room QA document fails schema validation."""


def _require_mapping(data: Any, label: str) -> Mapping[str, Any]:
    if not isinstance(data, Mapping):
        raise SchemaError(f"{label} must be a JSON object")
    return data


def _require_keys(data: Mapping[str, Any], keys: Iterable[str], label: str) -> None:
    missing = [key for key in keys if key not in data]
    if missing:
        raise SchemaError(f"{label} missing required keys: {', '.join(missing)}")


def _require_list_of_str(data: Mapping[str, Any], key: str, label: str) -> list[str]:
    value = data[key]
    if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
        raise SchemaError(f"{label}.{key} must be a list of strings")
    return value


def _scenario_covers_smoke(scenarios: list[str]) -> bool:
    return any(".smoke" in scenario or scenario.endswith(".smoke") for scenario in scenarios)


def _scenario_covers_happy(scenarios: list[str]) -> bool:
    return any("happy-path" in scenario for scenario in scenarios)


def _scenario_covers_guard(scenarios: list[str]) -> bool:
    return any(".guard" in scenario or "guard." in scenario for scenario in scenarios)


def validate_room_manifest(data: Mapping[str, Any] | Any) -> None:
    """Validate a room pack manifest against design §8."""
    payload = _require_mapping(data, "manifest")
    _require_keys(payload, _REQUIRED_MANIFEST_KEYS, "manifest")

    if payload.get("schemaVersion") != 1:
        raise SchemaError("manifest.schemaVersion must be 1")

    for key in ("roomId", "areaId", "entryPreset"):
        if not isinstance(payload[key], str) or not payload[key].strip():
            raise SchemaError(f"manifest.{key} must be a non-empty string")

    status = payload["implementationStatus"]
    if status not in ALLOWED_IMPLEMENTATION_STATUSES:
        raise SchemaError(
            "manifest.implementationStatus must be one of "
            f"{sorted(ALLOWED_IMPLEMENTATION_STATUSES)}; got {status!r}"
        )

    scenes = _require_list_of_str(payload, "unityScenes", "manifest")
    if not scenes:
        raise SchemaError("manifest.unityScenes must not be empty")

    caps = payload["requiredCapabilities"]
    if not isinstance(caps, list) or any(not isinstance(item, str) for item in caps):
        raise SchemaError("manifest.requiredCapabilities must be a list of strings")

    scenarios = _require_list_of_str(payload, "scenarios", "manifest")
    missing_tiers: list[str] = []
    if not _scenario_covers_smoke(scenarios):
        missing_tiers.append("smoke")
    if not _scenario_covers_happy(scenarios):
        missing_tiers.append("happy-path")
    if not _scenario_covers_guard(scenarios):
        missing_tiers.append("guard")
    if missing_tiers:
        raise SchemaError(
            "manifest.scenarios must include smoke, happy-path, and guard ids; "
            f"missing: {', '.join(missing_tiers)}"
        )

    exit_contract = payload["exitContract"]
    if not isinstance(exit_contract, Mapping):
        raise SchemaError("manifest.exitContract must be an object")
    for key in ("inventoryContains", "unlocks"):
        if key not in exit_contract:
            raise SchemaError(f"manifest.exitContract missing {key}")
        if not isinstance(exit_contract[key], list):
            raise SchemaError(f"manifest.exitContract.{key} must be a list")
    if "flags" not in exit_contract or not isinstance(exit_contract["flags"], Mapping):
        raise SchemaError("manifest.exitContract.flags must be an object")


def validate_room_scenario(data: Mapping[str, Any] | Any) -> None:
    """Validate a single room scenario document."""
    payload = _require_mapping(data, "scenario")
    _require_keys(
        payload,
        ("schemaVersion", "id", "roomId", "tier", "requiredCapabilities", "steps"),
        "scenario",
    )
    if payload.get("schemaVersion") != 1:
        raise SchemaError("scenario.schemaVersion must be 1")
    if not isinstance(payload["id"], str) or not payload["id"].strip():
        raise SchemaError("scenario.id must be a non-empty string")
    if not isinstance(payload["roomId"], str) or not payload["roomId"].strip():
        raise SchemaError("scenario.roomId must be a non-empty string")
    tier = payload["tier"]
    if tier not in ALLOWED_SCENARIO_TIERS:
        raise SchemaError(
            f"scenario.tier must be one of {sorted(ALLOWED_SCENARIO_TIERS)}; got {tier!r}"
        )
    caps = payload["requiredCapabilities"]
    if not isinstance(caps, list) or any(not isinstance(item, str) for item in caps):
        raise SchemaError("scenario.requiredCapabilities must be a list of strings")
    steps = payload["steps"]
    if not isinstance(steps, list):
        raise SchemaError("scenario.steps must be a list")
    for index, step in enumerate(steps):
        _validate_room_scenario_step(step, index)


def _validate_room_scenario_step(step: Any, index: int) -> None:
    label = f"scenario.steps[{index}]"
    mapping = _require_mapping(step, label)
    for key in ("id", "family", "name"):
        if key not in mapping or not isinstance(mapping[key], str) or not mapping[key].strip():
            raise SchemaError(f"{label}.{key} must be a non-empty string")
    family = mapping["family"]
    name = mapping["name"]
    target = mapping.get("targetId")
    needs_target = (family == "interaction" and name in {"invoke", "pointer"}) or (
        family == "preset" and name == "apply"
    )
    if needs_target and (not isinstance(target, str) or not target.strip()):
        raise SchemaError(f"{label}.targetId is required for {family}.{name}")
    parameters = mapping.get("parameters")
    if parameters is not None and not isinstance(parameters, Mapping):
        raise SchemaError(f"{label}.parameters must be an object when present")
    if (
        family == "interaction"
        and name == "pointer"
        and isinstance(parameters, Mapping)
        and "mode" in parameters
        and parameters["mode"] not in (None, "realInput", "api")
    ):
        raise SchemaError(
            f"{label}.parameters.mode must be 'realInput' or 'api' when present"
        )

def validate_transition(data: Mapping[str, Any] | Any) -> None:
    """Validate a transition scenario against design §7."""
    payload = _require_mapping(data, "transition")
    _require_keys(payload, _REQUIRED_TRANSITION_KEYS, "transition")
    if payload.get("schemaVersion") != 1:
        raise SchemaError("transition.schemaVersion must be 1")
    for key in ("id", "sourceRegion", "destinationRegion"):
        if not isinstance(payload[key], str) or not payload[key].strip():
            raise SchemaError(f"transition.{key} must be a non-empty string")
    for key in (
        "prerequisites",
        "lockedAssertions",
        "sourceExitContract",
        "destinationEntryContract",
        "checkpointContract",
    ):
        value = payload[key]
        if not isinstance(value, list) or any(not isinstance(item, str) for item in value):
            raise SchemaError(f"transition.{key} must be a list of strings")
