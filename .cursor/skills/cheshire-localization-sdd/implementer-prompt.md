# Implementer Subagent Prompt (Cheshire Localization)

Use with Task tool `generalPurpose`. Fill bracketed sections before dispatch.

```
You are implementing Task N: [task name] for Cheshire multilingual localization.

## Context pack (mandatory)

[PASTE full contents of context-pack.md]

## Task description (mandatory — full text from plan)

[PASTE full Task N section from the plan, including every step and code block]

## Scene-setting

[Where this fits: dependencies from prior tasks, what must remain unchanged]

## Before you begin

If anything is unclear about requirements, approach, dependencies, or acceptance criteria, **ask now** and stop.

## Your job

Once clear:
1. Follow TDD: failing test → minimal implementation → pass → refactor.
2. Implement exactly this task — nothing else.
3. Run the verification commands listed in the task / context pack.
4. Self-review completeness, quality, YAGNI, and tests.
5. **Do NOT git commit or push.**

Work from: c:\Users\user\Documents\GitHub\newCapstone

## Hard constraints

- Do not edit Fungus package sources.
- Do not add a second language settings system or PlayerPrefs language key.
- Do not change tool/JSON schema keys.
- Do not mix locales in one composed request.
- Prefer existing project patterns (global namespace for mokotan AI types, EditMode NUnit tests).

## Escalate

Report BLOCKED or NEEDS_CONTEXT if architectural judgment is required beyond the plan, or you cannot find clarity after reasonable search.

## Report format

- **Status:** DONE | DONE_WITH_CONCERNS | BLOCKED | NEEDS_CONTEXT
- What you implemented
- What you tested and results (commands + pass/fail)
- Files changed (create/modify/delete)
- Self-review findings
- Concerns (if any)
```
