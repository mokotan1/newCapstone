using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SnapTarget 이 붙은 GameObject 의 layer 를 TagManager 의 "SnapTarget" 과 일치시키는 에디터 보조 도구.
/// </summary>
[InitializeOnLoad]
internal static class SnapTargetLayerEnforcer
{
    private const string ScenesFolderPrefix = "Assets/Scenes/";

    private static bool isFixing;

    static SnapTargetLayerEnforcer()
    {
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (isFixing)
            return;

        ApplySnapTargetLayersInScene(scene, path, dryRun: false, logPrefixAutoFix: true);
    }

    [MenuItem("Tools/GodLotto/Fix SnapTarget Layers (All Scenes)")]
    private static void FixAllScenesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int expected = LayerMask.NameToLayer("SnapTarget");
        if (expected < 0)
        {
            Debug.LogError("[SnapTargetLayerEnforcer] Layer 'SnapTarget' is missing in TagManager.");
            return;
        }

        SceneSetup[] backup = EditorSceneManager.GetSceneManagerSetup();
        isFixing = true;

        int totalFixed = 0;
        int scenesWithFixes = 0;
        int scanned = 0;

        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsUnderScenesFolder(path))
                    continue;

                scanned++;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int n = ApplySnapTargetLayersInScene(scene, path, dryRun: false, logPrefixAutoFix: true);
                if (n < 0)
                    return;

                if (n > 0)
                {
                    totalFixed += n;
                    scenesWithFixes++;
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            isFixing = false;
            EditorSceneManager.RestoreSceneManagerSetup(backup);
        }

        Debug.Log(
            $"[SnapTargetLayerEnforcer] Fixed {totalFixed} targets in {scenesWithFixes} scenes (scanned {scanned}).");
    }

    [MenuItem("Tools/GodLotto/Validate SnapTarget Layers (Dry Run)")]
    private static void DryRunMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int expected = LayerMask.NameToLayer("SnapTarget");
        if (expected < 0)
        {
            Debug.LogError("[SnapTargetLayerEnforcer] Layer 'SnapTarget' is missing in TagManager.");
            return;
        }

        SceneSetup[] backup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsUnderScenesFolder(path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                ApplySnapTargetLayersInScene(scene, path, dryRun: true, logPrefixAutoFix: false);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(backup);
        }

        Debug.Log("[SnapTargetLayerEnforcer] Dry run complete — see warnings above for violations.");
    }

    /// <summary>
    /// Returns number of GameObjects whose layer was corrected, 0 if none, -1 if SnapTarget layer missing.
    /// </summary>
    private static int ApplySnapTargetLayersInScene(
        Scene scene,
        string pathForLog,
        bool dryRun,
        bool logPrefixAutoFix)
    {
        int expected = LayerMask.NameToLayer("SnapTarget");
        if (expected < 0)
        {
            Debug.LogError("[SnapTargetLayerEnforcer] Layer 'SnapTarget' is missing in TagManager.");
            return -1;
        }

        int fixedCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SnapTarget st in root.GetComponentsInChildren<SnapTarget>(true))
            {
                GameObject go = st.gameObject;
                if (go.layer == expected)
                    continue;

                if (dryRun)
                {
                    string hierarchyPath = GetHierarchyPath(go);
                    string layerName = LayerMask.LayerToName(go.layer);
                    string layerLabel = string.IsNullOrEmpty(layerName) ? go.layer.ToString() : $"{layerName} ({go.layer})";
                    Debug.LogWarning(
                        $"[SnapTargetLayerEnforcer] Dry-run · {pathForLog} · {hierarchyPath} · layer {layerLabel}");
                    continue;
                }

                string oldLayerName = LayerMask.LayerToName(go.layer);
                if (string.IsNullOrEmpty(oldLayerName))
                    oldLayerName = go.layer.ToString();

                go.layer = expected;
                fixedCount++;

                if (logPrefixAutoFix)
                {
                    Debug.LogWarning(
                        $"[SnapTargetLayerEnforcer] Auto-fixed '{pathForLog}': '{oldLayerName}' → 'SnapTarget'.",
                        go);
                }

                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        return fixedCount;
    }

    private static bool IsUnderScenesFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        return assetPath.StartsWith(ScenesFolderPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHierarchyPath(GameObject go)
    {
        if (go == null)
            return "";

        Transform t = go.transform;
        var sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, '/');
            sb.Insert(0, t.name);
        }

        return sb.ToString();
    }
}
