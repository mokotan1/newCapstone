---
name: cheshire-localization-sdd
description: >-
  Use when implementing or reviewing Cheshire multilingual localization (ko/ja/en)
  against docs/superpowers/specs/2026-07-13-cheshire-multilingual-localization-design.md
  or docs/superpowers/plans/2026-07-13-cheshire-multilingual-localization.md, or when
  dispatching implementer/spec/quality/content subagents for that feature.
---

# Cheshire Localization Subagent-Driven Development

## Overview

Controller keeps the plan and context. Fresh subagent per task. Two-stage review after each task: spec compliance, then code quality.

**REQUIRED SUB-SKILL:** Superpowers `subagent-driven-development` and `test-driven-development`.

## When to use

- Executing the Cheshire multilingual localization plan
- Continuing mid-feature after a context reset
- Reviewing a localization task against the design spec

## Orchestration (controller)

1. Read `docs/superpowers/plans/2026-07-13-cheshire-multilingual-localization.md` once; extract all tasks into TodoWrite.
2. Attach `context-pack.md` + full task text to every implementer (subagents must not open the plan file).
3. Dispatch **one** implementer at a time (`generalPurpose`). No parallel implementers.
4. On DONE → dispatch spec reviewer (`readonly: true`) using `spec-reviewer-prompt.md`.
5. Spec pass → dispatch quality reviewer (`code-reviewer`) using `code-quality-reviewer-prompt.md`.
6. Any fail → re-dispatch implementer with the issue list; re-review until both pass.
7. Task 3: run content author (`content-author-prompt.md`) before or with implementer for JA/EN prompts.
8. After all tasks: final `code-reviewer` + architecture.md update check.
9. **Do not commit** unless the user explicitly asks.

## Red flags

- Fungus core edits or a second language settings system
- Mixing locales inside one request
- Changing tool/JSON keys (`give_hint`, `update_quiz`, `hint_level`, `is_correct`)
- Skipping spec review before quality review
- Parallel implementers on the same branch
- Claiming done without EditMode / pytest evidence

## Prompt files

| Role | File |
|------|------|
| Fixed domain context | `context-pack.md` |
| Implementer | `implementer-prompt.md` |
| Spec compliance | `spec-reviewer-prompt.md` |
| Code quality | `code-quality-reviewer-prompt.md` |
| JA/EN content | `content-author-prompt.md` |
