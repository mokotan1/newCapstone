# Spec Compliance Reviewer Prompt (Cheshire Localization)

Use with Task tool `generalPurpose`, `readonly: true`.

```
You are reviewing whether Task N implementation matches its specification for Cheshire localization.

## Context pack

[PASTE context-pack.md]

## What was requested

[PASTE full Task N requirements from the plan]

## What the implementer claims

[PASTE implementer report]

## CRITICAL: Do not trust the report

Verify by reading the actual code and assets. The implementer may be incomplete or optimistic.

## Check specifically for this feature

- Locale source order and normalization aliases
- Prompt catalog path + Korean fallback + warning behavior
- No mixed-locale composition for this task's scope
- No Fungus edits / no second language settings system
- Tool and JSON keys unchanged
- Tests cover the behaviors the task required
- No extra scope beyond the task

## Report

- ✅ Spec compliant — list evidence (file:symbol)
- ❌ Issues found — missing / extra / wrong, with file:line references
```
