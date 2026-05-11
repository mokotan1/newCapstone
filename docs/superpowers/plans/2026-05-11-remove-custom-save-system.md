# Remove Custom Save System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the project's custom save/load system while keeping the Fungus package's built-in save code available for future redesign.

**Architecture:** Detach gameplay UI from custom save types, then delete the custom save scripts and custom save assets. Leave `Assets/Fungus` untouched so future planning can reuse or replace it deliberately.

**Tech Stack:** Unity C#, Fungus, Unity YAML scenes/prefabs.

---

### Task 1: Detach UI From Custom Save Types

**Files:**
- Modify: `disputatio/Assets/godlotto/Script/MainMenu.cs`
- Modify: `disputatio/Assets/godlotto/Script/InGameSettingsPanel.cs`
- Modify: `disputatio/Assets/godlotto/Script/Setting/IntegratedSettingUI.cs`

- [ ] Remove fields, method calls, and references to `SaveSlotManager`, `SaveLoadBrowserView`, `FungusSaveStorage`, `FungusSaveSystemBootstrap`, and `Fungus.SaveMenu`.
- [ ] Keep existing settings, cursor, scene navigation, audio, and pause behavior unchanged.
- [ ] Make the load button a no-op with a warning log until the save feature is redesigned.

### Task 2: Delete Custom Save Assets

**Files:**
- Delete: `disputatio/Assets/godlotto/Script/SaveSlotManager.cs`
- Delete: `disputatio/Assets/godlotto/Script/SaveSlotManager.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/FungusSaveStorage.cs`
- Delete: `disputatio/Assets/godlotto/Script/FungusSaveStorage.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/FungusSaveSystemBootstrap.cs`
- Delete: `disputatio/Assets/godlotto/Script/FungusSaveSystemBootstrap.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/SaveThumbnailEncoder.cs`
- Delete: `disputatio/Assets/godlotto/Script/SaveThumbnailEncoder.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveLoadBrowserView.cs`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveLoadBrowserView.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveBrowserUiBuilder.cs`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveBrowserUiBuilder.cs.meta`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveSlotRowRenderer.cs`
- Delete: `disputatio/Assets/godlotto/Script/Setting/SaveSlotRowRenderer.cs.meta`
- Delete: `disputatio/Assets/mokotan/Pripab/save_Flowchart.prefab`
- Delete: `disputatio/Assets/mokotan/Pripab/save_Flowchart.prefab.meta`
- Delete: `disputatio/Assets/mokotan/Pripab/SavePannel.prefab`
- Delete: `disputatio/Assets/mokotan/Pripab/SavePannel.prefab.meta`
- Delete: `disputatio/Assets/mokotan/Pripab/Save.prefab`
- Delete: `disputatio/Assets/mokotan/Pripab/Save.prefab.meta`
- Delete: `disputatio/Assets/Sprite/SaveImage`
- Delete: `disputatio/Assets/Sprite/SaveImage.meta`

- [ ] Remove the listed custom save files and assets.
- [ ] Do not delete anything under `disputatio/Assets/Fungus`.

### Task 3: Verify References And Compilation Surface

**Files:**
- Inspect: `disputatio/Assets`

- [ ] Run `rg -n "SaveSlotManager|SaveLoadBrowserView|SaveBrowserUiBuilder|SaveSlotRowRenderer|FungusSaveStorage|FungusSaveSystemBootstrap|SaveThumbnailEncoder|FungusSaveSlotSummary" disputatio/Assets -g "*.cs" -g "*.unity" -g "*.prefab"`.
- [ ] Run the local C# syntax checker if available.
- [ ] Report any remaining Fungus package save references separately from project custom save references.
