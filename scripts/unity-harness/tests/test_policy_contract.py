"""Static contract for Unity harness policy docs (design AC1, AC7, AC9)."""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def _read(relative: str) -> str:
    path = ROOT / relative
    assert path.is_file(), f"missing required file: {relative}"
    return path.read_text(encoding="utf-8")


def test_required_harness_policy_files_exist() -> None:
    for relative in (
        ".harness/unity-policy.md",
        ".harness/unity-verification.md",
        ".harness/unity-toolchain.json",
        ".harness/unity-cli-postflight.md",
        ".harness/official-cli-compat.md",
    ):
        assert (ROOT / relative).is_file(), relative


def test_toolchain_json_has_required_fields() -> None:
    payload = json.loads(_read(".harness/unity-toolchain.json"))
    for key in (
        "schemaVersion",
        "activeBackend",
        "projectPath",
        "editorVersion",
        "backends",
    ):
        assert key in payload, key
    assert payload["projectPath"] == "disputatio"
    assert payload["editorVersion"] == "6000.0.36f1"
    assert payload["activeBackend"] == "legacy-unity-cli"
    backends = payload["backends"]
    assert "legacy-unity-cli" in backends
    legacy = backends["legacy-unity-cli"]
    assert legacy["connectorPackage"] == "com.youngwoocho02.unity-cli-connector"
    assert legacy["connectorLockRevision"] == "c07b5dbd71edb51810df1a678d565bc045d5046e"
    official = backends["official-unity-cli"]
    assert official["status"] == "not-verified"
    assert official["cliVersionObserved"] == "1.0.0-beta.5"
    assert official["pipelinePackage"] == "not-installed-in-disputatio"


def test_agents_entry_points_to_common_policy_not_only_cli_commands() -> None:
    text = _read("AGENTS.md")
    assert ".harness/unity-policy.md" in text
    assert ".harness/unity-verification.md" in text
    assert "사용자 변경 보존" in text or "기존 사용자 변경을 보존" in text


def test_postflight_rule_defers_completion_criteria_to_common_verification() -> None:
    text = _read(".cursor/rules/unity-verification-postflight.mdc")
    assert ".harness/unity-verification.md" in text
    assert ".harness/unity-policy.md" in text


def test_unity_automation_skill_defers_completion_criteria() -> None:
    text = _read(".cursor/skills/newcapstone-unity-automation/SKILL.md")
    assert ".harness/unity-verification.md" in text
    assert ".harness/unity-toolchain.json" in text


def test_cli_postflight_is_short_pointer() -> None:
    text = _read(".harness/unity-cli-postflight.md")
    assert ".harness/unity-verification.md" in text
    assert len(text.splitlines()) <= 40


def test_policy_distinguishes_risk_and_forbids_r2_verified_without_review() -> None:
    policy = _read(".harness/unity-policy.md")
    verification = _read(".harness/unity-verification.md")
    combined = policy + "\n" + verification
    for token in ("R0", "R1", "R2", "R3"):
        assert token in combined, token
    assert "independentReview: false" in combined
    assert "verified" in combined
    assert "waived" in combined
    assert "Unity 실행 불필요" in policy or "Unity 실행 없이" in verification


def test_feature_workflow_records_risk_levels() -> None:
    text = _read("docs/development/feature-workflow.md")
    assert "R0" in text
    assert "R2" in text
    assert "R3" in text
    assert ".harness/unity-policy.md" in text


def test_task_packet_template_has_risk_ownership_and_evidence_fields() -> None:
    text = _read("docs/development/task-packet-template.md")
    assert "위험도" in text
    assert "소유권" in text
    assert "증거" in text
    assert "verificationStatus" in text or "검증 상태" in text
