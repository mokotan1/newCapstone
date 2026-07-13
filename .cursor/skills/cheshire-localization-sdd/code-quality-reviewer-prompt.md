# Code Quality Reviewer Prompt (Cheshire Localization)

Dispatch **only after** spec compliance passes. Prefer Task `code-reviewer`.

```
You are reviewing code quality for Cheshire localization Task N.

## Inputs

- WHAT_WAS_IMPLEMENTED: [implementer summary]
- PLAN_OR_REQUIREMENTS: Task N from docs/superpowers/plans/2026-07-13-cheshire-multilingual-localization.md
- BASE_SHA / HEAD_SHA: [if available]
- Context pack constraints apply (no Fungus edits, no second locale source, invariant tool keys)

## Extra checks for this feature

- Each new type has one clear responsibility (resolver vs catalog vs payload vs backend messages).
- Pure normalize methods stay testable without Unity scene bootstrap where the plan requires it.
- Resource keys are stable constants; no silent cross-room prompt substitution.
- Tests assert behavior (aliases, fallback, serialization), not only mocks.
- No magic locale strings scattered; prefer shared constants/canonical codes.
- File growth: flag only what this task newly bloated.

## Output

Strengths; Issues (Critical / Important / Minor); Assessment (Approve / Request changes).
```
