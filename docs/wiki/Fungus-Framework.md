# Fungus deletion status

Working ledger for the C# framework that replaces Fungus.
This page is not built from the source manifest, so `build_wiki.py` keeps the body.
Home links here from the curated list in `tools/wiki_rag/build_wiki.py`.

Back to [Home](Home.md).

## Done

- Listener is ready and the product-scene inventory is written. See [fungus-deletion index](../development/tasks/fungus-deletion/index.md).
- Sequence runtime (`FlagStore`, `SequencePlayer`) passes EditMode. Independent review is done.
- Back navigation reads session route state (`SceneRouteState`) instead of the Fungus `PrevScene` variable. Independent review is done. Scene and prefab `PrevScene` fields are still in the files.
- Checkpoint save rejects a blank resume scene, a blank Fungus key, and a duplicate or mixed-type key before it writes. The previous checkpoint stays. EditMode only. Independent review is not done.
- Save and load keys are listed in [P2-save-key-map](../development/tasks/fungus-deletion/P2-save-key-map.md). Continue reads `Checkpoint.Latest.v1` JSON. New game keeps four audio and display settings and does not call Fungus `DoSaveReset()`.
- `SceneNameSetter` writes `SceneName` only. Inventory items are not restored from the Fungus load signal.
- Progress and save scripts, with their `.meta` files, live in `disputatio/Assets/godlotto/Script/Progress/`. Checkpoint code is `Progress/Checkpoint`. `FungusVariableKeys` moved with them.
- Loose game scripts are in zone folders: `Script/SceneFlow`, `Script/Interaction`, `Script/Dialogue`, `Script/Setting`, `Script/Title`, `Script/Stage`, `Script/Minigame`, and `mokotan/script/Stage`. Files were not split. `Assets/Fungus` stayed.
- Product scenes no longer contain a Fungus Save Point command. Each former start block keeps its Game Started handler, and that handler runs the first command that used to follow the Save Point. Fungus example scenes were left unchanged. `Hallway_Left2` also dropped one command reference, fileID `1931588757`, which did not point at an object.

Folder check on 2026-09-22: compile exit 0, console error/warning empty. After the progress move: `CheckpointRepositoryTests` 7/7, `CheckpointServiceTests` 6/6, `InventoryManagerFungusSaveSignalTests` 1/1. After the remaining zone move: `SceneRouteStateTests` 2/2, `BackNavigatorTests` 10/10, `SceneNameSetterTests` 1/1, `MainMenuNewGameResetTests` 3/3. Save Point removal: `pytest tools/tests/test_remove_fungus_save_points.py` 4 passed, 42 product scenes updated, compile exit 0, console empty after refresh. Play mode and live QA were not run.

Zone map: [script-zones](../development/script-zones.md). Paths: [architecture](../architecture.md).

## Next

- Dialogue slice. `Assets/Fungus` stays until that slice is done.
- Tell a corrupt checkpoint apart from a missing one. Fold outside-JSON progress keys in only with a migration. `CheshireAnswerTextScale` and `LocalAi.ChatDisabled` are still wiped on a new game.
- G2 is not met: no schema migration, no independent review of the save work, no play QA.

Do not commit until asked. Do not pop the qa-tool-integration stash.
