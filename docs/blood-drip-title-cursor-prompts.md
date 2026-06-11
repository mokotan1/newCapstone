# Blood Drip Title Unity Port - Cursor Prompts

Use these prompts one at a time. Each task already includes the shared context Cursor should follow.

## BTD-01 - Create Unity title style data contract and local mock loader

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Implement the data layer for the blood-drip title feature. Create a serializable TitleStylePayload matching backendContract.fields from cursorUnityPortSpec, plus a TitleStyleService that can load a local mock payload first. Place mock data in an appropriate Assets path. Do not wire networking yet. Include validation/clamping for dripIntensity and safe defaults for missing optional colors, poolEnabled, and seed.
```

## BTD-02 - Create TMP font registry with language fallback

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Implement TitleFontRegistry for mapping backend fontKey values to TMP_FontAsset references. It must support explicit fontKey lookup and language fallback for at least ko and en. Unknown fontKey should not break rendering. Add inspector-friendly serializable entries so designers can assign TMP font assets in Unity.
```

## BTD-03 - Build BloodDripTitleRenderer using TMP glyph anchors

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Create BloodDripTitleRenderer. It should apply TitleStylePayload to a TMP_Text component, call ForceMeshUpdate(), read TMP_Text.textInfo.characterInfo, collect visible glyph lower anchor positions, and schedule attached blood drips from those anchors. Keep scheduling deterministic when seed is present. Expose inspector fields for min/max spawn delay, streak length, fall speed, and intensity scaling.
```

## BTD-04 - Implement BloodDrip visual behavior

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Implement the BloodDrip component/prefab behavior. A drip starts attached to a glyph anchor as a thin vertical streak, grows downward, shows a rounded tip droplet, then detaches/falls and triggers splash/pool callbacks. Use simple Unity UI or SpriteRenderer primitives consistent with the target scene setup. Match the timing and visual intent of oozeFrom(), dripFrom(), and impact() in the HTML prototype without copying browser-specific code.
```

## BTD-05 - Implement BloodPool growth and splash feedback

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Implement BloodPool behavior. It should grow width/height gradually as drips land, support poolEnabled=false, and emit small splash particles or simple transient sprites at impact positions. Keep the implementation lightweight and controllable from BloodDripTitleRenderer.
```

## BTD-06 - Create integration scene or prefab and smoke test path

```text
You are working in C:/Users/user/Documents/GitHub/newCapstone. Preserve unrelated user changes. Read docs/blood-drip-title-final.html and parse the JSON script tag with id cursorUnityPortSpec before editing. Implement only the requested work item. Follow existing Unity project conventions under disputatio/Assets. Prefer TextMeshPro for title rendering. After script edits, run the available C# syntax/check workflow if present and report exact verification results.

Wire the components into a reusable Unity prefab or sample scene. It should load the local mock payload, render DISPUTATIO by default, and allow switching the mock text to Korean to verify fallback fonts. Add a concise smoke-test note or editor test if the project has an established test pattern for this kind of UI behavior.
```
