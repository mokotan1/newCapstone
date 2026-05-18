using System.IO;
using System;
using System.Collections.Generic;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BackspaceUiSceneApplier
{
    public const string SceneBackRootName = "SceneBackNavigator_Ribbon";
    private const string PendingRequestFileName = "BackspaceSceneApply.request";
    private static readonly string[] LegacySceneBackspaceObjectNames = { "Backspace" };
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
        "StudyRoomCutScene",
        "POAnimation",
        "GoPrisonAnimation",
        "BetaEnd"
    };

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

    public static bool IsSceneBackspaceName(string objectName)
    {
        return string.Equals(objectName, SceneBackRootName, StringComparison.Ordinal)
            || string.Equals(objectName, "SceneBackRibbon", StringComparison.Ordinal);
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
