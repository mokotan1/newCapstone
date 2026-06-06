---
name: newcapstone-architecture
description: >-
  Read docs/architecture.md before any newCapstone implementation work (Unity
  disputatio, backend_ai, deploy, CI). Use when adding features, fixing bugs,
  refactoring, migrating Fungus scenes, wiring AI chat, checkpoints, or when
  the user asks where to put code in this repo.
---

# newCapstone 아키텍처 선독

## When to use

**Always** at the start of work that changes this repository:

- New scenes, puzzles, interaction, inventory, checkpoints
- AI chatbot or `backend_ai` endpoints
- Tests, CI, deployment config
- Refactors under `disputatio/Assets/godlotto/` or `Assets/mokotan/`

Skip only for pure Q&A with no code edits, or when `docs/architecture.md` does not exist yet.

## Mandatory preflight

1. **Read** `docs/architecture.md` from the repo root (full file, or §2 + §6 + §7 + any section matching the task).
2. **Map the task** to documented folders and patterns before writing code.
3. **Implement** using §6 rules (e.g. `SceneNames`, `FungusVariableKeys`, `Godlotto.Interaction`, `SceneTransitionService`, `GameLog`, `CheckpointRepository`).
4. **After shipping logic changes**, update `docs/architecture.md` if structure or flows changed; note unknowns in §8.

## Quick routing (confirm in doc before coding)

| Task | Start here (see architecture.md) |
|------|----------------------------------|
| Room/corridor click & scene load | `Assets/godlotto/Script/Interaction/` |
| Save / continue | `Assets/godlotto/Script/Checkpoint/` |
| AI chat UI | `Assets/mokotan/mokotan/script/AI/` |
| Server URL | `Assets/godlotto/Script/Config/ServerConfig.cs` |
| HTTP API | `backend_ai/main.py`, `models/`, `services/` |
| Constants | `Assets/godlotto/Script/Constants/` |
| EditMode tests | `Assets/Editor/Tests/EditMode/` |

## Anti-patterns (from architecture doc)

- New Fungus `LoadScene` without `RoomInteractionController` / migration pattern
- Raw `SceneManager.LoadScene` bypassing `SceneTransitionService`
- Magic scene/variable strings instead of `SceneNames` / `FungusVariableKeys`
- Direct HTTP from UI instead of `ChatHttpClient`
- Large edits under `Assets/Fungus/` vendor tree

## Related rules

- `.cursor/rules/architecture-preflight.mdc` — same requirement, always applied in Cursor
- `.cursor/rules/notion-capstone-spec.mdc` — Notion spec sync (separate from architecture preflight)
- `docs/fungus-room-migration-plan.md` — Fungus → C# migration detail
