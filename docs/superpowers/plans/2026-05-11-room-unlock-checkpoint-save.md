# Room Unlock Checkpoint Save Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the auto-save checkpoint system where room unlocks save progress, corridor ghost deaths restore the latest unlocked room, underground deaths use separate checkpoints, and the main menu can continue from the latest checkpoint.

**Architecture:** Add a small checkpoint module under `disputatio/Assets/godlotto/Script/Checkpoint` with serializable checkpoint data, PlayerPrefs-backed repository, room checkpoint definitions, snapshot collect/apply helpers, a UnityEvent-friendly trigger, and a load coordinator. Keep the implementation independent of Fungus SaveMenu UI and integrate only at menu/retry click call sites.

**Tech Stack:** Unity C#, NUnit EditMode tests, PlayerPrefs JSON storage, Fungus Flowchart/global variables.

---

### Task 1: Checkpoint Data And Repository

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/CheckpointSaveData.cs`
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/CheckpointRepository.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/CheckpointRepositoryTests.cs`

- [x] Write failing tests for no-data, save/read, and clear behavior.
- [x] Implement serializable checkpoint DTO and PlayerPrefs JSON repository.

### Task 2: Definitions And Snapshot Policy

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/RoomCheckpointDefinition.cs`
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/ProgressSnapshotCollector.cs`
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/ProgressSnapshotApplier.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/RoomCheckpointDefinitionTests.cs`

- [x] Write failing tests for room definitions and excluded puzzle mid-state keys.
- [x] Implement room unlock definitions and custom checkpoint entry point for basement/minigame use.
- [x] Keep settings keys and puzzle mid-state keys out of snapshots.

### Task 3: Runtime Triggers And Load Flow

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/RoomUnlockCheckpointService.cs`
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/RoomUnlockCheckpointTrigger.cs`
- Create: `disputatio/Assets/godlotto/Script/Checkpoint/CheckpointLoadCoordinator.cs`
- Modify: `disputatio/Assets/godlotto/Script/MainMenu.cs`
- Modify: `disputatio/Assets/godlotto/KTH/Jumpscare/JumpscareManager.cs`
- Modify: `disputatio/Assets/godlotto/KTH/Jumpscare/SpecialJumpscareManager.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/CheckpointServiceTests.cs`

- [x] Write failing tests for saving a room unlock and choosing latest checkpoint for continue/retry.
- [x] Implement UnityEvent-friendly save trigger and static load coordinator.
- [x] Replace menu load/retry click call sites with checkpoint load plus fallback.
