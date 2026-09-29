# Checkpoint Domain Boundary Implementation Plan

> **For agentic workers:** Execute tasks in order. The parent implements in this checkout; a separate session reviews the final diff. Do not commit, push, or deploy without a user request.

**Goal:** Move existing checkpoint save invariants out of PlayerPrefs persistence without changing stored JSON or player behavior.

**Architecture:** `CheckpointSaveData` remains the serialization contract. A pure C# `CheckpointSavePolicy` validates it before `CheckpointRepository` writes. Runtime capture, scene loading, and restore stay with their existing owners.

**Tech Stack:** Unity 6000.0.36f1, C#, NUnit EditMode, repository `unity-cli` wrapper.

**Spec:** `docs/superpowers/specs/2026-09-28-checkpoint-domain-boundary-design.md`

## Global Constraints

- Preserve PlayerPrefs keys `Checkpoint.Latest.v1` and `Checkpoint.LatestId.v1`, JSON fields, and `CheckpointType` enum values.
- Preserve existing acceptance rules, including the current Fungus-only duplicate-key check; no new Sequence validation.
- Preserve pre-write rejection and `version`/`createdAtUtc` default behavior.
- No changes to scenes, prefabs, `Assets/Fungus`, game settings, or unrelated dirty files.
- R3 requires Unity compilation, related tests, isolated save/continue regression, and independent review for `verified`.

## Review Focus

- Null data must still throw `ArgumentNullException` before writing.
- Whitespace resume scene must still throw `ArgumentException` and preserve the previous save.
- Blank or duplicate Fungus keys across any bool/int/string arrays must still fail before writing.
- Null Fungus arrays must remain valid.
- Sequence entries sharing a key with Fungus entries must not acquire a new rejection rule.

---

### Task 1: Lock the save contract with tests

**Files:**
- Modify: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/CheckpointRepositoryTests.cs`
- Create: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/CheckpointSavePolicyTests.cs` and matching `.meta`

**Interfaces:** `CheckpointSavePolicy.ValidateForSave(CheckpointSaveData data): void` throws the existing exception types.

- [x] Add direct policy tests for the five review-focus cases. Check the new source dependencies during review rather than testing source text.
- [x] Run a source-linked Mono probe before implementation; RED confirmed because the policy did not exist. Unity EditMode was blocked by an unrelated project compile error.
- [x] Add a repository regression for unchanged `version`/`createdAtUtc` defaulting and existing save preservation.

### Task 2: Extract the pure validation rule

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Progress/Checkpoint/CheckpointSavePolicy.cs` and matching `.meta`
- Modify: `disputatio/Assets/godlotto/Script/Progress/Checkpoint/CheckpointRepository.cs`

**Interfaces:** `public static void ValidateForSave(CheckpointSaveData data)`; repository calls it before mutating the input and before any PlayerPrefs write.

- [x] Move the current null, resume-scene, and Fungus-key checks without altering accepted inputs or exception classes.
- [x] Leave JSON, PlayerPrefs operations, defaulting, and load normalization in `CheckpointRepository`.
- [ ] Run `CheckpointSavePolicyTests` and `CheckpointRepositoryTests` in Unity; source-linked Mono probe is GREEN (5/5), but Unity reports zero executed tests while compilation fails elsewhere.

### Task 3: Integration and handoff

**Files:**
- Modify: `docs/development/tasks/checkpoint-domain-boundary/index.md` (create if absent)
- Modify only if required by a changed architecture fact: `docs/architecture.md`

- [ ] Run Unity compile, Console error check, `CheckpointRepositoryTests`, `CheckpointServiceTests`, and `FlagStoreCheckpointMapperTests` with exact class filters.
- [ ] Verify new game, save, reentry, continue, and settings preservation using isolated QA state under `.harness/unity-verification.md`.
- [x] Request an independent review of requirements and quality. Resolve its two test-gap findings; rerun source-linked Mono tests. Unity tests remain blocked by project compilation.
- [x] Record commands, test counts, QA gap, and review status in the task index. Report `blocked` rather than `verified` while Unity compile and isolated play QA are unavailable.
