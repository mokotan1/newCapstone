using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ClearMenuCommand = Fungus.ClearMenu;
using FungusMenuCommand = Fungus.Menu;

/// <summary>
/// Fungus <see cref="FungusMenuCommand"/> 연속 묶음을 <see cref="GlassMenu"/> 한 커맨드로 변환합니다.
/// <see cref="ClearMenuCommand"/>는 GlassMenu.OnEnter()가 Clear()를 호출하므로 제거합니다.
/// </summary>
public static class GlassMenuMigrator
{
    static readonly string[] DefaultSearchRoots =
    {
        "Assets/Scenes",
        "Assets/godlotto",
        "Assets/mokotan",
    };

    static readonly string[] ExcludedPathPrefixes =
    {
        "Assets/Fungus/",
        "Assets/FungusExamples/",
    };

    public struct MenuOptionSnapshot
    {
        public string Text;
        public Block TargetBlock;
        public bool Interactable;
    }

    public struct MigrationResult
    {
        public int AssetsProcessed;
        public int AssetsChanged;
        public int FlowchartsProcessed;
        public int GlassMenusCreated;
        public int MenusRemoved;
        public int ClearMenusRemoved;
        public int Warnings;
    }

    [MenuItem("Tools/Godlotto/Migrate/Fungus Menu → Glass Menu (Project Scenes)")]
    public static void MigrateProjectScenesMenuItem()
    {
        try
        {
            var result = MigrateAll(CollectAssetPaths(DefaultSearchRoots));
            AssetDatabase.SaveAssets();
            Debug.Log(BuildReport(result));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GlassMenuMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    [MenuItem("Tools/Godlotto/Migrate/Verify Fungus Menu Migration")]
    public static void VerifyMigrationMenuItem()
    {
        int remainingMenus = CountLegacyCommands<FungusMenuCommand>(CollectAssetPaths(DefaultSearchRoots));
        int remainingClear = CountLegacyCommands<ClearMenuCommand>(CollectAssetPaths(DefaultSearchRoots));

        if (remainingMenus == 0 && remainingClear == 0)
        {
            Debug.Log("[GlassMenuMigrator] Verification passed: no Fungus Menu/Clear Menu commands remain in scope.");
            return;
        }

        Debug.LogError(
            $"[GlassMenuMigrator] Verification failed: {remainingMenus} Menu, {remainingClear} Clear Menu command(s) remain in scope.");
    }

    public static MigrationResult MigrateAll(IReadOnlyList<string> assetPaths)
    {
        var result = new MigrationResult();
        foreach (string path in assetPaths)
        {
            result.AssetsProcessed++;
            bool changed = path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
                ? MigrateSceneAsset(path, ref result)
                : MigratePrefabAsset(path, ref result);

            if (changed)
                result.AssetsChanged++;
        }

        return result;
    }

    public static IReadOnlyList<string> CollectAssetPaths(IEnumerable<string> searchRoots)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in searchRoots)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { root }))
                TryAddAsset(paths, AssetDatabase.GUIDToAssetPath(guid));

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                TryAddAsset(paths, AssetDatabase.GUIDToAssetPath(guid));
        }

        return paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static void TryAddAsset(ISet<string> paths, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        if (ExcludedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return;
        paths.Add(path);
    }

    static bool MigrateSceneAsset(string scenePath, ref MigrationResult result)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        bool changed = false;
        foreach (GameObject root in scene.GetRootGameObjects())
            changed |= MigrateHierarchy(root, ref result);

        if (changed)
            EditorSceneManager.SaveScene(scene);

        return changed;
    }

    static bool MigratePrefabAsset(string prefabPath, ref MigrationResult result)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            bool changed = MigrateHierarchy(root, ref result);
            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool MigrateHierarchy(GameObject root, ref MigrationResult result)
    {
        bool changed = false;
        foreach (Flowchart flowchart in root.GetComponentsInChildren<Flowchart>(true))
            changed |= MigrateFlowchart(flowchart, ref result);
        return changed;
    }

    static bool MigrateFlowchart(Flowchart flowchart, ref MigrationResult result)
    {
        bool changed = false;
        result.FlowchartsProcessed++;

        foreach (Block block in flowchart.GetComponents<Block>())
            changed |= MigrateBlock(block, flowchart, ref result);

        if (changed)
        {
            EditorUtility.SetDirty(flowchart);
            foreach (Block block in flowchart.GetComponents<Block>())
                EditorUtility.SetDirty(block);
        }

        return changed;
    }

    static bool MigrateBlock(Block block, Flowchart flowchart, ref MigrationResult result)
    {
        bool changed = false;

        for (int i = block.CommandList.Count - 1; i >= 0; i--)
        {
            Command command = block.CommandList[i];
            if (command == null)
                continue;

            if (command is ClearMenuCommand clearMenu)
            {
                RemoveCommand(block, clearMenu);
                result.ClearMenusRemoved++;
                changed = true;
            }
        }

        while (TryFindMenuRun(block.CommandList, out int startIndex, out int runLength))
        {
            var menuRun = block.CommandList
                .Skip(startIndex)
                .Take(runLength)
                .Cast<FungusMenuCommand>()
                .ToList();

            ReplaceMenuRun(block, flowchart, menuRun, ref result);
            result.MenusRemoved += menuRun.Count;
            result.GlassMenusCreated++;
            changed = true;
        }

        return changed;
    }

    static bool TryFindMenuRun(IReadOnlyList<Command> commands, out int startIndex, out int runLength)
    {
        startIndex = -1;
        runLength = 0;

        for (int i = 0; i < commands.Count; i++)
        {
            if (commands[i] == null || commands[i] is not FungusMenuCommand)
                continue;

            startIndex = i;
            runLength = 1;
            for (int j = i + 1; j < commands.Count; j++)
            {
                if (commands[j] is FungusMenuCommand)
                {
                    runLength++;
                    continue;
                }
                break;
            }

            return true;
        }

        return false;
    }

    static void ReplaceMenuRun(
        Block block,
        Flowchart flowchart,
        IReadOnlyList<FungusMenuCommand> menuRun,
        ref MigrationResult result)
    {
        int insertIndex = block.CommandList.IndexOf(menuRun[0]);
        int indentLevel = menuRun[0].IndentLevel;
        var snapshots = new List<MenuOptionSnapshot>(menuRun.Count);
        foreach (FungusMenuCommand menu in menuRun)
            snapshots.Add(ReadMenuSnapshot(menu, ref result));

        foreach (FungusMenuCommand menu in menuRun)
            RemoveCommand(block, menu);

        GlassMenu glassMenu = CreateGlassMenu(block, flowchart, insertIndex, indentLevel, snapshots);
        EditorUtility.SetDirty(glassMenu);
    }

    static MenuOptionSnapshot ReadMenuSnapshot(FungusMenuCommand menu, ref MigrationResult result)
    {
        var so = new SerializedObject(menu);
        SerializedProperty interactableProp = so.FindProperty("interactable");
        SerializedProperty hideIfVisitedProp = so.FindProperty("hideIfVisited");
        SerializedProperty hideThisOptionProp = so.FindProperty("hideThisOption");
        SerializedProperty setMenuDialogProp = so.FindProperty("setMenuDialog");

        if (hideIfVisitedProp != null && hideIfVisitedProp.boolValue)
        {
            result.Warnings++;
            Debug.LogWarning(
                $"[GlassMenuMigrator] '{menu.name}' uses hideIfVisited; GlassMenu does not support it. Option migrated as static.",
                menu);
        }

        if (hideThisOptionProp != null)
        {
            SerializedProperty hideVal = hideThisOptionProp.FindPropertyRelative("booleanVal");
            if (hideVal != null && hideVal.boolValue)
            {
                result.Warnings++;
                Debug.LogWarning(
                    $"[GlassMenuMigrator] '{menu.name}' uses hideThisOption; GlassMenu does not support it. Option migrated as visible.",
                    menu);
            }
        }

        SerializedProperty booleanRef = interactableProp?.FindPropertyRelative("booleanRef");
        if (booleanRef != null && booleanRef.objectReferenceValue != null)
        {
            result.Warnings++;
            Debug.LogWarning(
                $"[GlassMenuMigrator] '{menu.name}' interactable uses a Boolean variable ref; GlassMenu stores a static bool (booleanVal copied).",
                menu);
        }

        if (setMenuDialogProp != null && setMenuDialogProp.objectReferenceValue != null)
        {
            result.Warnings++;
            Debug.LogWarning(
                $"[GlassMenuMigrator] '{menu.name}' setMenuDialog is ignored; GlassMenu uses GlassMenuDialog.GetMenuDialog().",
                menu);
        }

        return new MenuOptionSnapshot
        {
            Text = so.FindProperty("text")?.stringValue ?? string.Empty,
            TargetBlock = so.FindProperty("targetBlock")?.objectReferenceValue as Block,
            Interactable = interactableProp?.FindPropertyRelative("booleanVal")?.boolValue ?? true,
        };
    }

    static GlassMenu CreateGlassMenu(
        Block block,
        Flowchart flowchart,
        int insertIndex,
        int indentLevel,
        IReadOnlyList<MenuOptionSnapshot> snapshots)
    {
        var glassMenu = block.gameObject.AddComponent<GlassMenu>();
        glassMenu.ParentBlock = block;
        glassMenu.ItemId = flowchart.NextItemId();
        glassMenu.IndentLevel = indentLevel;
        glassMenu.OnCommandAdded(block);

        var so = new SerializedObject(glassMenu);
        SerializedProperty optionsProp = so.FindProperty("options");
        optionsProp.arraySize = snapshots.Count;
        for (int i = 0; i < snapshots.Count; i++)
        {
            SerializedProperty element = optionsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("text").stringValue = snapshots[i].Text;
            element.FindPropertyRelative("targetBlock").objectReferenceValue = snapshots[i].TargetBlock;
            element.FindPropertyRelative("interactable").boolValue = snapshots[i].Interactable;
        }

        so.FindProperty("anchor").enumValueIndex = (int)MenuAnchor.BottomCenter;
        so.FindProperty("menuOffset").vector2Value = Vector2.zero;
        so.FindProperty("setMenuDialog").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        block.CommandList.Insert(insertIndex, glassMenu);
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        return glassMenu;
    }

    static void RemoveCommand(Block block, Command command)
    {
        command.OnCommandRemoved(block);
        int index = block.CommandList.IndexOf(command);
        if (index >= 0)
            block.CommandList.RemoveAt(index);
        UnityEngine.Object.DestroyImmediate(command);
    }

    static int CountLegacyCommands<TCommand>(IReadOnlyList<string> assetPaths) where TCommand : Command
    {
        int count = 0;
        foreach (string path in assetPaths)
        {
            if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                        count += root.GetComponentsInChildren<TCommand>(true).Length;
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            else
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    count += root.GetComponentsInChildren<TCommand>(true).Length;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        return count;
    }

    static string BuildReport(MigrationResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[GlassMenuMigrator] Migration complete.");
        sb.AppendLine($"  Assets processed/changed: {result.AssetsProcessed}/{result.AssetsChanged}");
        sb.AppendLine($"  Flowcharts processed: {result.FlowchartsProcessed}");
        sb.AppendLine($"  GlassMenu created: {result.GlassMenusCreated}");
        sb.AppendLine($"  Fungus Menu removed: {result.MenusRemoved}");
        sb.AppendLine($"  Clear Menu removed: {result.ClearMenusRemoved}");
        sb.AppendLine($"  Warnings: {result.Warnings}");
        return sb.ToString();
    }

    /// <summary>
    /// EditMode 테스트용: 연속 Menu 인덱스 묶음 계산.
    /// ClearMenu는 묶음을 끊고, 단독 Menu는 길이 1 묶음을 반환합니다.
    /// </summary>
    public static List<List<int>> FindConsecutiveMenuRuns(IReadOnlyList<Type> commandTypes)
    {
        var runs = new List<List<int>>();
        int i = 0;
        while (i < commandTypes.Count)
        {
            if (commandTypes[i] != typeof(FungusMenuCommand))
            {
                i++;
                continue;
            }

            var run = new List<int> { i };
            int j = i + 1;
            while (j < commandTypes.Count)
            {
                if (commandTypes[j] == typeof(ClearMenuCommand))
                    break;
                if (commandTypes[j] == typeof(FungusMenuCommand))
                {
                    run.Add(j);
                    j++;
                    continue;
                }
                break;
            }

            runs.Add(run);
            i = j;
        }

        return runs;
    }
}
