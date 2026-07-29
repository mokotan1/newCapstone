"""Contract tests for Task 11: Cursor QA subagents and orchestration fallback.

Validates:
  * .cursor/agents/qa-*.md frontmatter/body contracts (name, description,
    common evidence root reference, required JSON handoff envelope, and
    exclusive Unity mutation authority for qa-playtester only).
  * .cursor/rules/qa-subagent-orchestration.mdc documents the same handoff
    contract, the single-writer lease rule, and the cursor-agent CLI
    fallback.
  * scripts/qa/invoke-qa-agent.ps1 and scripts/qa/run-cursor-qa.ps1 exist,
    are syntactically valid PowerShell, reference the required cursor-agent
    CLI surface and preflight behaviour, and never disable normal command
    approvals.

No PyYAML dependency is assumed (not installed in this repo's environment),
so frontmatter is parsed with light-touch regexes rather than a full YAML
parser -- sufficient because agent frontmatter only uses simple
``key: value`` / ``key: >-`` block-scalar pairs.
"""

from __future__ import annotations

import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
AGENTS_DIR = REPO_ROOT / ".cursor" / "agents"
RULES_DIR = REPO_ROOT / ".cursor" / "rules"
SCRIPTS_DIR = REPO_ROOT / "scripts" / "qa"

ORCHESTRATION_RULE = RULES_DIR / "qa-subagent-orchestration.mdc"
INVOKE_SCRIPT = SCRIPTS_DIR / "invoke-qa-agent.ps1"
RUN_SCRIPT = SCRIPTS_DIR / "run-cursor-qa.ps1"

EXPECTED_AGENT_NAMES = (
    "qa-coordinator",
    "qa-inventory",
    "qa-scenario-author",
    "qa-playtester",
    "qa-evidence-reviewer",
)

UNITY_MUTATING_AGENT = "qa-playtester"

REQUIRED_ENVELOPE_FIELDS = (
    "taskId",
    "scenarioIds",
    "evidenceRoot",
    "status",
    "findings",
)

EVIDENCE_ROOT_PREFIX = "docs/qa/runs/"

REQUIRED_CLI_OPTIONS = ("-p", "--output-format", "--workspace", "--prompt")
FORBIDDEN_APPROVAL_BYPASS_FLAGS = (
    "--dangerously-skip-permissions",
    "--yolo",
    "--force-approve",
    "--auto-approve",
    "--skip-approval",
)

FRONTMATTER_RE = re.compile(r"\A---\r?\n(?P<body>.*?)\r?\n---\r?\n", re.DOTALL)
JSON_BLOCK_RE = re.compile(r"```json\s*(?P<body>\{.*?\})\s*```", re.DOTALL)


def _agent_path(name: str) -> Path:
    return AGENTS_DIR / f"{name}.md"


def _read(path: Path) -> str:
    assert path.exists(), f"Expected file missing: {path}"
    return path.read_text(encoding="utf-8")


def _frontmatter(text: str) -> str:
    match = FRONTMATTER_RE.match(text)
    assert match, "Agent/rule file must start with a --- frontmatter block"
    return match.group("body")


def _frontmatter_value(frontmatter: str, key: str) -> str | None:
    match = re.search(rf"^{re.escape(key)}:\s*(\S+)\s*$", frontmatter, re.MULTILINE)
    return match.group(1) if match else None


def _json_blocks(text: str) -> list[dict]:
    blocks: list[dict] = []
    for match in JSON_BLOCK_RE.finditer(text):
        try:
            blocks.append(json.loads(match.group("body")))
        except json.JSONDecodeError:
            continue
    return blocks


def _find_powershell() -> str | None:
    for exe in ("pwsh", "powershell"):
        path = shutil.which(exe)
        if path:
            return path
    return None


_BLOCK_COMMENT_RE = re.compile(r"<#.*?#>", re.DOTALL)
_LINE_COMMENT_RE = re.compile(r"(?<!['\"])#.*$", re.MULTILINE)


def _strip_powershell_comments(text: str) -> str:
    """Remove <# ... #> block comments and trailing # line comments.

    Used to check what a script actually *executes* (e.g. real CLI flags),
    as opposed to prose in comment-doc headers that merely *names* a flag
    while explaining it is deliberately never passed.
    """
    without_blocks = _BLOCK_COMMENT_RE.sub("", text)
    return _LINE_COMMENT_RE.sub("", without_blocks)


# ---------------------------------------------------------------------------
# .cursor/agents/qa-*.md contracts
# ---------------------------------------------------------------------------


def test_exactly_the_expected_qa_agent_files_exist() -> None:
    """No missing files, and no invented/conflicting qa-* agent names."""
    found = {p.stem for p in AGENTS_DIR.glob("qa-*.md")}
    expected = set(EXPECTED_AGENT_NAMES)
    missing = expected - found
    unexpected = found - expected
    assert not missing, f"Missing required agent definitions: {sorted(missing)}"
    assert not unexpected, f"Unexpected/conflicting qa-* agent names: {sorted(unexpected)}"


@pytest.mark.parametrize("agent_name", EXPECTED_AGENT_NAMES)
def test_agent_has_name_and_description_frontmatter(agent_name: str) -> None:
    frontmatter = _frontmatter(_read(_agent_path(agent_name)))

    name_value = _frontmatter_value(frontmatter, "name")
    assert name_value, f"{agent_name}.md frontmatter missing 'name'"
    assert name_value == agent_name, (
        f"{agent_name}.md frontmatter name '{name_value}' must match its filename"
    )

    assert re.search(r"^description:\s*\S", frontmatter, re.MULTILINE), (
        f"{agent_name}.md frontmatter missing a non-empty 'description'"
    )


@pytest.mark.parametrize("agent_name", EXPECTED_AGENT_NAMES)
def test_agent_references_common_evidence_root(agent_name: str) -> None:
    text = _read(_agent_path(agent_name))
    assert EVIDENCE_ROOT_PREFIX in text, (
        f"{agent_name}.md must reference the common evidence root '{EVIDENCE_ROOT_PREFIX}'"
    )


@pytest.mark.parametrize("agent_name", EXPECTED_AGENT_NAMES)
def test_agent_emits_required_json_envelope(agent_name: str) -> None:
    text = _read(_agent_path(agent_name))
    blocks = _json_blocks(text)
    assert blocks, f"{agent_name}.md must include at least one ```json envelope block"

    matching = [
        block
        for block in blocks
        if all(field in block for field in REQUIRED_ENVELOPE_FIELDS)
    ]
    assert matching, (
        f"{agent_name}.md must include a JSON block with all required envelope fields "
        f"{REQUIRED_ENVELOPE_FIELDS}; found blocks: {blocks}"
    )

    for block in matching:
        evidence_root = block["evidenceRoot"]
        assert evidence_root.startswith(EVIDENCE_ROOT_PREFIX) or evidence_root == EVIDENCE_ROOT_PREFIX.rstrip("/"), (
            f"{agent_name}.md envelope 'evidenceRoot' must live under '{EVIDENCE_ROOT_PREFIX}', "
            f"got: {evidence_root!r}"
        )
        assert isinstance(block["scenarioIds"], list)
        assert isinstance(block["findings"], list)


@pytest.mark.parametrize("agent_name", EXPECTED_AGENT_NAMES)
def test_agent_declares_unity_mutation_authority_flag(agent_name: str) -> None:
    """Every agent must explicitly declare unity-mutation-authority: true|false."""
    frontmatter = _frontmatter(_read(_agent_path(agent_name)))
    value = _frontmatter_value(frontmatter, "unity-mutation-authority")
    assert value in ("true", "false"), (
        f"{agent_name}.md frontmatter must declare 'unity-mutation-authority: true' or "
        f"'unity-mutation-authority: false', found: {value!r}"
    )
    expected = "true" if agent_name == UNITY_MUTATING_AGENT else "false"
    assert value == expected, (
        f"{agent_name}.md declares unity-mutation-authority={value}, expected {expected}"
    )


def test_only_qa_playtester_claims_unity_mutation_authority() -> None:
    claimants = []
    for agent_name in EXPECTED_AGENT_NAMES:
        frontmatter = _frontmatter(_read(_agent_path(agent_name)))
        if _frontmatter_value(frontmatter, "unity-mutation-authority") == "true":
            claimants.append(agent_name)
    assert claimants == [UNITY_MUTATING_AGENT], (
        f"Only '{UNITY_MUTATING_AGENT}' may claim Unity mutation authority; found: {claimants}"
    )


@pytest.mark.parametrize(
    "agent_name", [name for name in EXPECTED_AGENT_NAMES if name != UNITY_MUTATING_AGENT]
)
def test_non_playtester_agents_declare_no_unity_drive_in_authority_table(agent_name: str) -> None:
    """Belt-and-braces prose check alongside the frontmatter flag."""
    text = _read(_agent_path(agent_name))
    authority_lines = [
        line
        for line in text.splitlines()
        if line.strip().startswith("|") and re.search(r"unity|lease", line, re.IGNORECASE)
    ]
    assert authority_lines, (
        f"{agent_name}.md must document Unity/lease authority in its Authority table"
    )
    for line in authority_lines:
        assert re.search(r"\bNo\b", line), (
            f"{agent_name}.md Authority table row must deny Unity/lease access: {line!r}"
        )


def test_playtester_declares_unity_drive_in_authority_table() -> None:
    text = _read(_agent_path(UNITY_MUTATING_AGENT))
    authority_lines = [
        line
        for line in text.splitlines()
        if line.strip().startswith("|") and re.search(r"unity|lease", line, re.IGNORECASE)
    ]
    assert authority_lines, "qa-playtester.md must document Unity/lease authority"
    assert any(re.search(r"\bYes\b", line) for line in authority_lines), (
        "qa-playtester.md Authority table must grant Unity mutation / lease authority"
    )


@pytest.mark.parametrize(
    "agent_name",
    [
        ("qa-inventory", "read"),
        ("qa-evidence-reviewer", "read"),
        ("qa-scenario-author", "authoriz"),
        ("qa-coordinator", "delegat"),
        ("qa-playtester", "lease"),
    ],
)
def test_role_prose_matches_its_documented_responsibility(agent_name: tuple[str, str]) -> None:
    name, keyword = agent_name
    text = _read(_agent_path(name)).lower()
    assert keyword in text, f"{name}.md should describe its role using '{keyword}'"


# ---------------------------------------------------------------------------
# .cursor/rules/qa-subagent-orchestration.mdc contract
# ---------------------------------------------------------------------------


def test_orchestration_rule_has_description_frontmatter() -> None:
    frontmatter = _frontmatter(_read(ORCHESTRATION_RULE))
    assert re.search(r"^description:\s*\S", frontmatter, re.MULTILINE)


def test_orchestration_rule_references_evidence_root() -> None:
    text = _read(ORCHESTRATION_RULE)
    assert EVIDENCE_ROOT_PREFIX in text


def test_orchestration_rule_lists_required_envelope_fields() -> None:
    text = _read(ORCHESTRATION_RULE)
    for field in REQUIRED_ENVELOPE_FIELDS:
        assert field in text, f"Rule must mention envelope field '{field}'"


def test_orchestration_rule_enforces_single_qa_playtester_lease() -> None:
    text = _read(ORCHESTRATION_RULE)
    lowered = text.lower()
    assert "qa-playtester" in lowered
    assert "lease" in lowered
    assert re.search(r"\bone\b|\bsingle\b|\bsole\b|\bexclusive\b", lowered), (
        "Rule must state exclusivity of the Unity-mutating lease"
    )
    assert "reject" in lowered, "Rule must state rejection of a second concurrent lease owner"


def test_orchestration_rule_documents_cursor_agent_cli_fallback() -> None:
    text = _read(ORCHESTRATION_RULE)
    assert "cursor-agent" in text
    assert "--output-format" in text


def test_orchestration_rule_references_all_five_agents() -> None:
    text = _read(ORCHESTRATION_RULE)
    for agent_name in EXPECTED_AGENT_NAMES:
        assert agent_name in text, f"Rule must reference '{agent_name}'"


# ---------------------------------------------------------------------------
# scripts/qa/*.ps1 presence + structural contract
# ---------------------------------------------------------------------------


@pytest.mark.parametrize("script_path", [INVOKE_SCRIPT, RUN_SCRIPT])
def test_qa_script_exists_and_nonempty(script_path: Path) -> None:
    assert script_path.exists(), f"Required script missing: {script_path}"
    assert script_path.stat().st_size > 0, f"Script is empty: {script_path}"


@pytest.mark.parametrize("script_path", [INVOKE_SCRIPT, RUN_SCRIPT])
def test_qa_script_parses_as_valid_powershell(script_path: Path) -> None:
    powershell = _find_powershell()
    if not powershell:
        pytest.skip("No pwsh/powershell executable available to validate syntax")

    command = (
        "$parseErrors = $null; "
        f"[void][System.Management.Automation.Language.Parser]::ParseFile('{script_path.as_posix()}', "
        "[ref]$null, [ref]$parseErrors); "
        "if ($parseErrors.Count -gt 0) { "
        "$parseErrors | ForEach-Object { Write-Output $_.Message }; exit 1 "
        "} else { exit 0 }"
    )
    result = subprocess.run(
        [powershell, "-NoProfile", "-NonInteractive", "-Command", command],
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )
    assert result.returncode == 0, (
        f"PowerShell parse errors in {script_path.name}:\n{result.stdout}\n{result.stderr}"
    )


def test_invoke_qa_agent_script_uses_required_cursor_cli_surface() -> None:
    text = _read(INVOKE_SCRIPT)
    for token in REQUIRED_CLI_OPTIONS:
        assert token in text, f"invoke-qa-agent.ps1 must reference required CLI option '{token}'"
    assert "cursor-agent" in text
    assert "--help" in text, "invoke-qa-agent.ps1 must run a --help preflight check"
    assert "--version" in text, "invoke-qa-agent.ps1 must capture the Cursor CLI version"
    assert re.search(r"\$ErrorActionPreference\s*=\s*['\"]Stop['\"]", text)


def test_invoke_qa_agent_script_reads_agent_definition_files() -> None:
    text = _read(INVOKE_SCRIPT)
    assert ".cursor" in text and "agents" in text
    for agent_name in EXPECTED_AGENT_NAMES:
        assert agent_name in text, f"invoke-qa-agent.ps1 ValidateSet must include '{agent_name}'"


def test_invoke_qa_agent_script_emits_json_envelope_fields() -> None:
    text = _read(INVOKE_SCRIPT)
    for field in REQUIRED_ENVELOPE_FIELDS:
        assert field in text, f"invoke-qa-agent.ps1 must build the '{field}' envelope field"


def test_run_cursor_qa_script_preflight_and_fallback_contract() -> None:
    text = _read(RUN_SCRIPT)
    for token in ("cursor-agent", "--help", "--version", "invoke-qa-agent"):
        assert token in text, f"run-cursor-qa.ps1 must reference '{token}'"
    assert re.search(r"TaskDelegation", text), (
        "run-cursor-qa.ps1 must check whether Task custom-agent delegation is available"
    )
    assert "lease" in text.lower(), "run-cursor-qa.ps1 must enforce the single-writer QA lease"
    assert EVIDENCE_ROOT_PREFIX in text, "run-cursor-qa.ps1 must write evidence under docs/qa/runs/"
    assert re.search(r"manifest", text, re.IGNORECASE), (
        "run-cursor-qa.ps1 must record a run manifest"
    )
    assert "cursorVersion" in text or "cursor_version" in text.lower(), (
        "run-cursor-qa.ps1 must capture the Cursor CLI version in the run manifest"
    )


def test_run_cursor_qa_script_rejects_second_playtester_lease_owner() -> None:
    text = _read(RUN_SCRIPT)
    assert re.search(r"qa-playtester", text)
    assert re.search(r"reject|already held", text, re.IGNORECASE), (
        "run-cursor-qa.ps1 must reject a second concurrent qa-playtester lease owner"
    )


@pytest.mark.parametrize("script_path", [INVOKE_SCRIPT, RUN_SCRIPT])
def test_qa_scripts_never_disable_command_approvals(script_path: Path) -> None:
    # Strip comments first: the docs deliberately *name* these flags to
    # explain they are never passed. Only the executable code must be free
    # of them.
    executable_text = _strip_powershell_comments(_read(script_path)).lower()
    for flag in FORBIDDEN_APPROVAL_BYPASS_FLAGS:
        assert flag not in executable_text, (
            f"{script_path.name} must not disable normal command approvals via '{flag}'"
        )


@pytest.mark.parametrize("script_path", [INVOKE_SCRIPT, RUN_SCRIPT])
def test_qa_scripts_fail_gracefully_when_cursor_cli_missing(script_path: Path) -> None:
    """Scripts must exist and produce an actionable message even without cursor-agent."""
    text = _read(script_path)
    assert re.search(r"Get-Command\s+['\"]?cursor-agent", text), (
        f"{script_path.name} must probe for the cursor-agent CLI with Get-Command"
    )
    assert re.search(r"cursor\.com/cli|install", text, re.IGNORECASE), (
        f"{script_path.name} must give an actionable remediation message when the CLI is missing"
    )
