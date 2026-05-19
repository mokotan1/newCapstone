using System.IO;
using System;
using System.Collections.Generic;
using Fungus;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BackspaceUiSceneApplier
{
    public const string SceneBackRootName = "SceneBackNavigator_Ribbon";
    private const string KoreanFontAssetPath = "Assets/Font/JalnanGothic SDF.asset";
    private const string CornerFoldSpritePath = BackspaceUiStyleCatalog.PrefabRoot + "/CornerFoldTriangle.png";
    private const string PendingRequestFileName = "BackspaceSceneApply.request";
    private static readonly string[] LegacySceneBackspaceObjectNames = { "Backspace" };
    private static readonly string[] CurrentBackspaceObjectNames =
    {
        SceneBackRootName,
        "SceneBackRibbon",
        "BackspaceCornerFold",
        "BackspaceNameplate"
    };
    private static readonly string[] LegacySceneBackspaceBlockNames =
    {
        "Backspace_Clicked",
        "Backspace_clicked",
        "Backspace_Select_Yes",
        "Backspace_Select_No"
    };
    private static readonly string[] SceneBackspaceExcludedSceneNames =
    {
        "MainMenuScene",
        "SettingScene",
        "IntroScene",
        "Opening_Office",
        "Opening_Mention",
        "Opening_Mention _open",
        "Hall_animate",
        "Hall_playerble",
        "StudyRoomCutScene",
        "POAnimation",
        "GoPrisonAnimation",
        "BetaEnd"
    };
    private static readonly string[] InteractionPanelNames =
    {
        "Panel",
        "ShowcasePanel1",
        "ShowcasePanel2",
        "ShowcasePanel3",
        "electrical control panel_Panel",
        "pot_Panel",
        "ButtonPanel",
        "TrashBox_pannel",
        "Sink_Pannel",
        "firpan_Panel",
        "BookcasePanel",
        "BookPanel",
        "SafePanel",
        "DiaryPanel",
        "CardStackPanel",
        "LockPanel",
        "WindowPanel",
        "WhiteBoardPanel",
        "NotePanel",
        "CalendarPanel",
        "DrawerPanel",
        "WallclockPanel",
        "Diary_Panel",
        "CookBook_Panel",
        "PuzzlePanel",
        "Key_Panel",
        "TablePanel",
        "BedfloorPanel",
        "ChestPanel"
    };
    private static readonly string[] InteractionPanelTargets =
    {
        "BasementResearchRoom|Panel",
        "2floorHallway_Left|ShowcasePanel1",
        "2floorHallway_Left|ShowcasePanel2",
        "2floorHallway_Left|ShowcasePanel3",
        "2floorLeft|ShowcasePanel1",
        "2floorLeft|ShowcasePanel2",
        "2floorLeft|ShowcasePanel3",
        "UtilityRoom|electrical control panel_Panel",
        "Hallway_Left|pot_Panel",
        "Hall_Left|pot_Panel",
        "BookCase2|ButtonPanel",
        "Kitchen|TrashBox_pannel",
        "Kitchen|Sink_Pannel",
        "Kitchen|firpan_Panel",
        "BedRoom|BookcasePanel",
        "BedRoom|BookPanel",
        "BedRoom|SafePanel",
        "StudyRoom|DiaryPanel",
        "StudyRoom|CardStackPanel",
        "StudyRoom|LockPanel",
        "TutorRoom|WindowPanel",
        "TutorRoom|WhiteBoardPanel",
        "Prison|NotePanel",
        "PrisonEntrance|LockPanel",
        "DressingRoom|CalendarPanel",
        "WifeRoom|LockPanel",
        "WifeRoom|DrawerPanel",
        "WifeRoom|WallclockPanel",
        "MaidRoom|Diary_Panel",
        "MaidRoom|CookBook_Panel",
        "MaidRoom|PuzzlePanel",
        "MaidRoom|Key_Panel",
        "MaidRoom|LockPanel",
        "ChildRoom|DrawerPanel",
        "ChildRoom|TablePanel",
        "ChildRoom|BedfloorPanel",
        "ChildRoom|ChestPanel"
    };
    private static readonly string[] ChatbotPanelTargets =
    {
        "Hall_playerble|Parret_Panel",
        "TutorRoom|Parret_Panel",
        "WifeRoom|Parret_Panel",
        "BedRoom|Parret_Panel",
        "ChildRoom|Parret_Panel"
    };
    private static readonly string[] LegacyCloseBlockTargets =
    {
        "StudyRoom|DiaryPanel|DiaryBackspace",
        "StudyRoom|CardStackPanel|CardStackBackspace",
        "Prison|NotePanel|PanelBackspace",
        "PrisonEntrance|LockPanel|LockBackspace",
        "MaidRoom|Diary_Panel|PanelBackspace"
    };
    private static readonly Color Ink = new Color(0.13f, 0.09f, 0.06f, 1f);
    private static readonly Color Paper = new Color(0.91f, 0.81f, 0.62f, 0.96f);
    private static readonly Color Gold = new Color(0.78f, 0.58f, 0.29f, 1f);

    [InitializeOnLoadMethod]
    private static void ApplyPendingRequestOnEditorReload()
    {
        if (!File.Exists(GetPendingRequestPath()))
            return;

        EditorApplication.delayCall += ApplyPendingRequest;
    }

    [MenuItem("Tools/godlotto/UI/Apply Scene Backspace To Build Scenes")]
    public static void ApplyToBuildScenes()
    {
        BackspaceUiPrefabBuilder.GenerateAll();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackspaceUiStyleCatalog.SceneBackPrefabPath);
        if (prefab == null)
            throw new InvalidOperationException($"Scene backspace prefab not found: {BackspaceUiStyleCatalog.SceneBackPrefabPath}");

        int changedCount = 0;
        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;
            if (IsSceneBackspaceExcludedScenePath(buildScene.path))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            if (HasSceneBackspace(scene))
                continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = SceneBackRootName;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedCount++;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Scene backspace applied to {changedCount} build scene(s).");
    }

    [MenuItem("Tools/godlotto/UI/Cleanup Legacy Scene Backspace")]
    public static void CleanupLegacySceneBackspaceInBuildScenes()
    {
        int changedScenes = 0;
        int removedObjects = 0;
        int removedBlocks = 0;

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;
            if (!IsKnownInteractionPanelTargetScene(Path.GetFileNameWithoutExtension(buildScene.path)))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var sceneRemovedObjects = RemoveLegacyBackspaceObjects(scene);
            var sceneRemovedBlocks = RemoveLegacyBackspaceBlocks(scene);

            if (sceneRemovedObjects == 0 && sceneRemovedBlocks == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedScenes++;
            removedObjects += sceneRemovedObjects;
            removedBlocks += sceneRemovedBlocks;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Legacy scene backspace cleanup changed {changedScenes} scene(s), removed {removedObjects} object(s) and {removedBlocks} Fungus block(s).");
    }

    [MenuItem("Tools/godlotto/UI/Remove Scene Backspace From Flow-Locked Scenes")]
    public static void RemoveSceneBackspaceFromFlowLockedScenes()
    {
        int changedScenes = 0;
        int removedObjects = 0;

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;
            if (!IsSceneBackspaceExcludedScenePath(buildScene.path))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var sceneRemovedObjects = RemoveSceneBackspaceObjects(scene);
            if (sceneRemovedObjects == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedScenes++;
            removedObjects += sceneRemovedObjects;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Removed scene backspace from {changedScenes} flow-locked scene(s), {removedObjects} object(s) total.");
    }

    [MenuItem("Tools/godlotto/UI/Apply Panel Backspace Skin To Build Scenes")]
    public static void ApplyPanelBackspaceSkinToBuildScenes()
    {
        BackspaceUiPrefabBuilder.GenerateAll();
        ApplyPanelBackspaceSkinToBuildScenesWithoutRegeneratingPrefabs();
    }

    [MenuItem("Tools/godlotto/UI/Apply Panel Backspace Skin Only To Build Scenes")]
    public static void ApplyPanelBackspaceSkinToBuildScenesWithoutRegeneratingPrefabs()
    {
        int changedScenes = 0;
        int changedButtons = 0;

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var sceneChangedButtons = ApplyPanelBackspaceSkin(scene);
            sceneChangedButtons += EnsureInteractionPanelBackspaces(scene);
            sceneChangedButtons += RemoveGeneratedBackspacesFromNonTargetPanels(scene);
            var sceneChangedCanvases = ApplySceneBackspaceCanvasOrder(scene);

            if (sceneChangedButtons == 0 && sceneChangedCanvases == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedScenes++;
            changedButtons += sceneChangedButtons;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Panel backspace skin applied in {changedScenes} scene(s), {changedButtons} button(s) total.");
    }

    [MenuItem("Tools/godlotto/UI/Apply Chatbot Backspace Nameplate Only To Build Scenes")]
    public static void ApplyChatbotBackspaceNameplateToBuildScenes()
    {
        int changedScenes = 0;
        int changedButtons = 0;

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;
            if (!IsKnownChatbotPanelTargetScene(Path.GetFileNameWithoutExtension(buildScene.path)))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var sceneChangedButtons = EnsureChatbotBackspaces(scene);
            if (sceneChangedButtons == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedScenes++;
            changedButtons += sceneChangedButtons;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Chatbot backspace nameplate applied in {changedScenes} scene(s), {changedButtons} button(s) total.");
    }

    [MenuItem("Tools/godlotto/UI/Configure Scene Backspace Camera Canvases And Cleanup Legacy")]
    public static void ConfigureSceneBackspaceCameraCanvasesAndCleanupLegacy()
    {
        var prefabChanged = ConfigureSceneBackspacePrefabCanvas();
        int changedScenes = 0;
        int changedCanvases = 0;
        int ensuredButtons = 0;
        int removedObjects = 0;

        foreach (var buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
                continue;

            var scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            var sceneChangedCanvases = ApplySceneBackspaceCanvasOrder(scene);
            var sceneEnsuredButtons = EnsureInteractionPanelBackspaces(scene) + EnsureChatbotBackspaces(scene);
            sceneEnsuredButtons += RemoveGeneratedBackspacesFromNonTargetPanels(scene);
            var sceneRemovedObjects = RemoveNonCurrentBackspaceObjects(scene);

            if (sceneChangedCanvases == 0 && sceneEnsuredButtons == 0 && sceneRemovedObjects == 0)
                continue;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            changedScenes++;
            changedCanvases += sceneChangedCanvases;
            ensuredButtons += sceneEnsuredButtons;
            removedObjects += sceneRemovedObjects;
        }

        Debug.Log($"[BackspaceUiSceneApplier] Scene backspace camera canvas cleanup done. Prefab changed: {prefabChanged}, changed {changedScenes} scene(s), configured {changedCanvases} canvas(es), ensured {ensuredButtons} current button(s), removed {removedObjects} old backspace object(s).");
    }

    public static bool IsSceneBackspaceName(string objectName)
    {
        return string.Equals(objectName, SceneBackRootName, StringComparison.Ordinal)
            || string.Equals(objectName, "SceneBackRibbon", StringComparison.Ordinal);
    }

    public static bool IsCurrentBackspaceObjectName(string objectName)
    {
        foreach (var currentName in CurrentBackspaceObjectNames)
        {
            if (string.Equals(objectName, currentName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsSceneBackspaceExcludedScenePath(string scenePath)
    {
        return IsSceneBackspaceExcludedSceneName(Path.GetFileNameWithoutExtension(scenePath));
    }

    public static bool IsSceneBackspaceExcludedSceneName(string sceneName)
    {
        foreach (var excludedSceneName in SceneBackspaceExcludedSceneNames)
        {
            if (string.Equals(sceneName, excludedSceneName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsLegacySceneBackspaceName(string objectName)
    {
        foreach (var legacyName in LegacySceneBackspaceObjectNames)
        {
            if (string.Equals(objectName, legacyName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsPanelBackspaceCandidateName(string objectName)
    {
        return !IsSceneBackspaceName(objectName)
            && !string.Equals(objectName, "BackspaceNameplate", StringComparison.Ordinal)
            && objectName.IndexOf("backspace", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsKnownInteractionPanelName(string objectName)
    {
        foreach (var panelName in InteractionPanelNames)
        {
            if (string.Equals(objectName, panelName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsKnownInteractionPanelTarget(string sceneName, string objectName)
    {
        var key = sceneName + "|" + objectName;
        foreach (var target in InteractionPanelTargets)
        {
            if (string.Equals(key, target, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsKnownInteractionPanelTargetScene(string sceneName)
    {
        foreach (var target in InteractionPanelTargets)
        {
            if (target.StartsWith(sceneName + "|", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsKnownChatbotPanelTarget(string sceneName, string objectName)
    {
        var key = sceneName + "|" + objectName;
        foreach (var target in ChatbotPanelTargets)
        {
            if (string.Equals(key, target, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static bool IsKnownChatbotPanelTargetScene(string sceneName)
    {
        foreach (var target in ChatbotPanelTargets)
        {
            if (target.StartsWith(sceneName + "|", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public static string ResolveLegacyCloseBlockName(string sceneName, string panelName)
    {
        var key = sceneName + "|" + panelName + "|";
        foreach (var target in LegacyCloseBlockTargets)
        {
            if (target.StartsWith(key, StringComparison.Ordinal))
                return target.Substring(key.Length);
        }

        return string.Empty;
    }

    [MenuItem("Tools/godlotto/UI/Request Scene Backspace Apply On Reload")]
    public static void RequestApplyOnReload()
    {
        Directory.CreateDirectory(GetPendingRequestDirectory());
        File.WriteAllText(GetPendingRequestPath(), DateTime.Now.ToString("O"));
        Debug.Log("[BackspaceUiSceneApplier] Pending request created. Scene backspace will apply after the next editor reload.");
    }

    private static void ApplyPendingRequest()
    {
        var path = GetPendingRequestPath();
        if (!File.Exists(path))
            return;

        File.Delete(path);
        ApplyToBuildScenes();
    }

    private static string GetPendingRequestDirectory()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), "Temp");
    }

    private static string GetPendingRequestPath()
    {
        return Path.Combine(GetPendingRequestDirectory(), PendingRequestFileName);
    }

    private static bool HasSceneBackspace(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (ContainsSceneBackspace(root.transform))
                return true;
        }

        return false;
    }

    private static bool ContainsSceneBackspace(Transform transform)
    {
        if (IsSceneBackspaceName(transform.gameObject.name))
            return true;

        for (int i = 0; i < transform.childCount; i++)
        {
            if (ContainsSceneBackspace(transform.GetChild(i)))
                return true;
        }

        return false;
    }

    private static int ApplySceneBackspaceCanvasOrder(Scene scene)
    {
        int changedCount = 0;
        var sceneCamera = FindSceneCamera(scene);

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (!string.Equals(canvas.gameObject.name, SceneBackRootName, StringComparison.Ordinal))
                    continue;

                if (ConfigureSceneBackspaceCanvas(canvas, sceneCamera))
                    changedCount++;
            }
        }

        return changedCount;
    }

    private static bool ConfigureSceneBackspacePrefabCanvas()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(BackspaceUiStyleCatalog.SceneBackPrefabPath);
        if (prefabRoot == null)
            return false;

        try
        {
            var canvas = prefabRoot.GetComponent<Canvas>();
            if (canvas == null)
                return false;

            if (!ConfigureSceneBackspaceCanvas(canvas, null))
                return false;

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BackspaceUiStyleCatalog.SceneBackPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static bool ConfigureSceneBackspaceCanvas(Canvas canvas, Camera sceneCamera)
    {
        bool changed = false;

        if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            changed = true;
        }

        if (canvas.worldCamera != sceneCamera)
        {
            canvas.worldCamera = sceneCamera;
            changed = true;
        }

        if (!canvas.overrideSorting)
        {
            canvas.overrideSorting = true;
            changed = true;
        }

        if (!string.Equals(canvas.sortingLayerName, BackspaceUiPrefabBuilder.SceneBackCanvasSortingLayerName, StringComparison.Ordinal))
        {
            canvas.sortingLayerName = BackspaceUiPrefabBuilder.SceneBackCanvasSortingLayerName;
            changed = true;
        }

        if (canvas.sortingOrder != BackspaceUiPrefabBuilder.SceneBackCanvasSortingOrder)
        {
            canvas.sortingOrder = BackspaceUiPrefabBuilder.SceneBackCanvasSortingOrder;
            changed = true;
        }

        if (!Mathf.Approximately(canvas.planeDistance, BackspaceUiPrefabBuilder.SceneBackCanvasPlaneDistance))
        {
            canvas.planeDistance = BackspaceUiPrefabBuilder.SceneBackCanvasPlaneDistance;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(canvas);

        return changed;
    }

    private static Camera FindSceneCamera(Scene scene)
    {
        Camera fallback = null;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera == null)
                    continue;
                if (fallback == null)
                    fallback = camera;
                if (camera.CompareTag("MainCamera") || string.Equals(camera.gameObject.name, "Main Camera", StringComparison.Ordinal))
                {
                    return camera;
                }
            }
        }

        return fallback;
    }

    private static int ApplyPanelBackspaceSkin(Scene scene)
    {
        int changedCount = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!IsPanelBackspaceCandidate(button.transform))
                    continue;

                ApplyCornerFoldSkin(button);
                changedCount++;
            }
        }

        return changedCount;
    }

    private static int EnsureInteractionPanelBackspaces(Scene scene)
    {
        int changedCount = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null)
                    continue;
                if (!IsKnownInteractionPanelName(rect.gameObject.name))
                    continue;
                if (!IsKnownInteractionPanelTarget(scene.name, rect.gameObject.name))
                    continue;

                var button = FindDirectChildButton(rect, "BackspaceCornerFold");
                if (button == null)
                    button = CreatePanelBackspaceButton(rect);

                EnsurePanelBackspaceCloser(button, rect.gameObject);
                ApplyCornerFoldSkin(button);
                changedCount++;
            }
        }

        return changedCount;
    }

    private static int RemoveGeneratedBackspacesFromNonTargetPanels(Scene scene)
    {
        int changedCount = 0;
        var buttonsToRemove = new List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;
                if (!string.Equals(button.gameObject.name, "BackspaceCornerFold", StringComparison.Ordinal))
                    continue;
                if (button.transform.parent == null)
                    continue;
                if (IsKnownInteractionPanelTarget(scene.name, button.transform.parent.gameObject.name))
                    continue;

                buttonsToRemove.Add(button.gameObject);
            }
        }

        foreach (var buttonObject in buttonsToRemove)
        {
            if (buttonObject == null)
                continue;

            UnityEngine.Object.DestroyImmediate(buttonObject);
            changedCount++;
        }

        return changedCount;
    }

    private static int EnsureChatbotBackspaces(Scene scene)
    {
        int changedCount = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null)
                    continue;
                if (!IsKnownChatbotPanelTarget(scene.name, rect.gameObject.name))
                    continue;

                var button = FindDirectChildButton(rect, "BackspaceNameplate");
                if (button == null)
                    button = CreateChatbotBackspaceButton(rect);

                EnsurePanelBackspaceCloser(button, rect.gameObject);
                ApplyChatNameplateSkin(button);
                changedCount++;
            }
        }

        return changedCount;
    }

    private static Button FindDirectChildButton(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (!string.Equals(child.gameObject.name, objectName, StringComparison.Ordinal))
                continue;

            return child.GetComponent<Button>();
        }

        return null;
    }

    private static Button CreateChatbotBackspaceButton(Transform panel)
    {
        var buttonObject = new GameObject("BackspaceNameplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel, false);
        return buttonObject.GetComponent<Button>();
    }

    private static Button FindPanelBackspaceButton(Transform panel)
    {
        for (int i = 0; i < panel.childCount; i++)
        {
            var child = panel.GetChild(i);
            if (!IsPanelBackspaceCandidateName(child.gameObject.name))
                continue;

            var button = child.GetComponent<Button>();
            if (button != null)
                return button;
        }

        return null;
    }

    private static Button CreatePanelBackspaceButton(Transform panel)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackspaceUiStyleCatalog.PanelBackspaceButtonPrefabPath);
        if (prefab != null)
        {
            var prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.gameObject.scene);
            prefabInstance.name = "BackspaceCornerFold";
            prefabInstance.transform.SetParent(panel, false);
            return prefabInstance.GetComponent<Button>();
        }

        var buttonObject = new GameObject("BackspaceCornerFold", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panel, false);
        return buttonObject.GetComponent<Button>();
    }

    private static void EnsurePanelBackspaceCloser(Button button, GameObject targetPanel)
    {
        var closer = button.GetComponent<PanelBackspaceCloser>();
        if (closer == null)
            closer = button.gameObject.AddComponent<PanelBackspaceCloser>();

        var serialized = new SerializedObject(closer);
        serialized.FindProperty("targetPanel").objectReferenceValue = targetPanel;
        serialized.FindProperty("executeBlockName").stringValue = ResolveLegacyCloseBlockName(targetPanel.scene.name, targetPanel.name);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(closer);
    }

    private static bool IsPanelBackspaceCandidate(Transform transform)
    {
        return IsPanelBackspaceCandidateName(transform.gameObject.name)
            && !IsInsideSceneBackspace(transform);
    }

    private static void ApplyCornerFoldSkin(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(96f, 96f);
        }

        var image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        image.color = Gold;
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CornerFoldSpritePath);
        image.type = Image.Type.Simple;
        image.raycastTarget = true;
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.78f, 0.67f, 0.48f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RemoveVisualChildren(button.transform);
        CreateCornerFoldLabel(button.transform);
        EditorUtility.SetDirty(button);
    }

    private static void ApplyChatNameplateSkin(Button button)
    {
        var rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-88f, -32f);
            rect.sizeDelta = new Vector2(175f, 71f);
        }

        var image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();

        image.color = Paper;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = true;
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.78f, 0.67f, 0.48f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        RemoveVisualChildren(button.transform);
        CreateChatNameplateLabel(button.transform);
        EditorUtility.SetDirty(button);
    }

    private static void CreateChatNameplateLabel(Transform parent)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(122f, 32f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "닫기  X";
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = Ink;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (fontAsset != null)
        {
            label.font = fontAsset;
            label.fontSharedMaterial = fontAsset.material;
        }
    }

    private static void RemoveVisualChildren(Transform transform)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private static void CreateCornerFoldLabel(Transform parent)
    {
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-25f, -24f);
        rect.sizeDelta = new Vector2(48f, 48f);

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "X";
        label.fontSize = 34f;
        label.fontStyle = FontStyles.Bold;
        label.color = Ink;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
        if (fontAsset != null)
        {
            label.font = fontAsset;
            label.fontSharedMaterial = fontAsset.material;
        }
    }

    private static int RemoveSceneBackspaceObjects(Scene scene)
    {
        var sceneBackspaceObjects = new List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
            CollectSceneBackspaceObjects(root.transform, sceneBackspaceObjects);

        foreach (var sceneBackspaceObject in sceneBackspaceObjects)
        {
            if (sceneBackspaceObject != null)
                UnityEngine.Object.DestroyImmediate(sceneBackspaceObject);
        }

        return sceneBackspaceObjects.Count;
    }

    private static void CollectSceneBackspaceObjects(Transform transform, List<GameObject> sceneBackspaceObjects)
    {
        if (string.Equals(transform.gameObject.name, SceneBackRootName, StringComparison.Ordinal))
        {
            sceneBackspaceObjects.Add(transform.gameObject);
            return;
        }

        if (string.Equals(transform.gameObject.name, "SceneBackRibbon", StringComparison.Ordinal)
            && !HasSceneBackspaceAncestor(transform.parent))
        {
            sceneBackspaceObjects.Add(transform.gameObject);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
            CollectSceneBackspaceObjects(transform.GetChild(i), sceneBackspaceObjects);
    }

    private static bool HasSceneBackspaceAncestor(Transform transform)
    {
        while (transform != null)
        {
            if (string.Equals(transform.gameObject.name, SceneBackRootName, StringComparison.Ordinal))
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private static int RemoveLegacyBackspaceObjects(Scene scene)
    {
        var legacyObjects = new List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
            CollectLegacyBackspaceObjects(root.transform, legacyObjects);

        foreach (var legacyObject in legacyObjects)
            UnityEngine.Object.DestroyImmediate(legacyObject);

        return legacyObjects.Count;
    }

    private static int RemoveNonCurrentBackspaceObjects(Scene scene)
    {
        var oldBackspaceObjects = new List<GameObject>();

        foreach (var root in scene.GetRootGameObjects())
            CollectNonCurrentBackspaceObjects(root.transform, oldBackspaceObjects);

        foreach (var oldBackspaceObject in oldBackspaceObjects)
        {
            if (oldBackspaceObject != null)
                UnityEngine.Object.DestroyImmediate(oldBackspaceObject);
        }

        return oldBackspaceObjects.Count;
    }

    private static void CollectNonCurrentBackspaceObjects(Transform transform, List<GameObject> oldBackspaceObjects)
    {
        if (IsNonCurrentBackspaceObjectName(transform.gameObject.name) && !HasCurrentBackspaceAncestor(transform.parent))
        {
            oldBackspaceObjects.Add(transform.gameObject);
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
            CollectNonCurrentBackspaceObjects(transform.GetChild(i), oldBackspaceObjects);
    }

    private static bool IsNonCurrentBackspaceObjectName(string objectName)
    {
        return objectName.IndexOf("backspace", StringComparison.OrdinalIgnoreCase) >= 0
            && !IsCurrentBackspaceObjectName(objectName);
    }

    private static bool HasCurrentBackspaceAncestor(Transform transform)
    {
        while (transform != null)
        {
            if (IsCurrentBackspaceObjectName(transform.gameObject.name))
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private static void CollectLegacyBackspaceObjects(Transform transform, List<GameObject> legacyObjects)
    {
        if (IsLegacySceneBackspaceName(transform.gameObject.name) && !IsInsideSceneBackspace(transform))
            legacyObjects.Add(transform.gameObject);

        for (int i = 0; i < transform.childCount; i++)
            CollectLegacyBackspaceObjects(transform.GetChild(i), legacyObjects);
    }

    private static bool IsInsideSceneBackspace(Transform transform)
    {
        while (transform != null)
        {
            if (IsSceneBackspaceName(transform.gameObject.name))
                return true;

            transform = transform.parent;
        }

        return false;
    }

    private static int RemoveLegacyBackspaceBlocks(Scene scene)
    {
        int removedCount = 0;

        foreach (var flowchart in FindFlowchartsInScene(scene))
        {
            var blocks = flowchart.GetComponents<Block>();
            foreach (var block in blocks)
            {
                if (!IsLegacyBackspaceBlockName(block.BlockName))
                    continue;

                RemoveBlock(flowchart, block);
                removedCount++;
            }

            if (removedCount > 0)
                EditorUtility.SetDirty(flowchart);
        }

        return removedCount;
    }

    private static IEnumerable<Flowchart> FindFlowchartsInScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var flowchart in root.GetComponentsInChildren<Flowchart>(true))
                yield return flowchart;
        }
    }

    private static bool IsLegacyBackspaceBlockName(string blockName)
    {
        foreach (var legacyName in LegacySceneBackspaceBlockNames)
        {
            if (string.Equals(blockName, legacyName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void RemoveBlock(Flowchart flowchart, Block block)
    {
        foreach (var command in block.CommandList)
        {
            if (command != null)
                UnityEngine.Object.DestroyImmediate(command);
        }

        if (block._EventHandler != null)
            UnityEngine.Object.DestroyImmediate(block._EventHandler);

        flowchart.SelectedBlocks.Remove(block);
        UnityEngine.Object.DestroyImmediate(block);
    }
}
