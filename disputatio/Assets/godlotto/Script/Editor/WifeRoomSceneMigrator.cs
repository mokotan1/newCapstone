using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WifeRoomSceneMigrator
{
    const string WifeRoomScenePath = "Assets/Scenes/Mokotan/Second Floor/WifeRoom.unity";

    static readonly RoomInteractionSceneMigrationEditor.WorldClickTarget[] WorldClickTargets =
    {
        new() { InteractionId = "wallclock", GameObjectName = "Wallclock" },
        new() { InteractionId = "dress_door", GameObjectName = "Dress_Door" },
        new() { InteractionId = "dressingtable", GameObjectName = "Dressingtable" },
        new() { InteractionId = "drawer", GameObjectName = "Drawer" },
        new() { InteractionId = "parrot", GameObjectName = "Parret" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ClickRoutes =
    {
        new() { InteractionId = "wallclock", BlockName = "Wallclock_Clicked" },
        new() { InteractionId = "dress_door", BlockName = "DressDoor_Clicked" },
        new() { InteractionId = "dressingtable", BlockName = "Dressingtable_Clicked" },
        new() { InteractionId = "drawer", BlockName = "Drawer_Clicked" },
        new() { InteractionId = "down_drawer", BlockName = "DownDrawer_Clicked" },
        new() { InteractionId = "parrot", BlockName = "Parrot_Clicked" },
    };

    static readonly string[] DisconnectObjectClickedBlockNames =
    {
        "Wallclock_Clicked",
        "DressDoor_Clicked",
        "Dressingtable_Clicked",
        "Drawer_Clicked",
        "Parrot_Clicked",
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec UnlockRoute =
        new() { InteractionId = "unlock", BlockName = "UnlockSuccess" };

    static readonly HashSet<string> ClickBlockNamesForVerification = new(
        ClickRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    static readonly HashSet<string> UnlockBlockNamesForVerification = new(
        new[] { UnlockRoute.BlockName },
        StringComparer.Ordinal);

    const string WallclockPanelBackspaceId = "wallclock_backspace";
    const string DrawerPanelBackspaceId = "drawer_backspace";
    const string LockPanelBackspaceId = "lock_backspace";

    static readonly PanelCloseTarget[] PanelCloseTargets =
    {
        new() { PanelCloseId = WallclockPanelBackspaceId, PanelObjectName = "WallclockPanel" },
        new() { PanelCloseId = DrawerPanelBackspaceId, PanelObjectName = "DrawerPanel" },
        new() { PanelCloseId = LockPanelBackspaceId, PanelObjectName = "DrawerPanel" },
    };

    static readonly PanelBackspaceTarget[] PanelBackspaceTargets =
    {
        new() { PanelName = "WallclockPanel", PanelCloseId = WallclockPanelBackspaceId },
        new() { PanelName = "DrawerPanel", PanelCloseId = DrawerPanelBackspaceId },
    };

    static readonly HashSet<string> PanelBackspaceBlockNamesForVerification = new(
        new[] { "Wallclock_Backspace", "Drawer_Backspace", "Lock_Backspace" },
        StringComparer.Ordinal);

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec BackRoute =
        new() { InteractionId = "back", BlockName = "BackSpace_Clicked" };

    static readonly RoomInteractionSceneMigrationEditor.BlockOutcomeSpec[] BackOutcomes =
    {
        new() { BlockName = "Select_Yes", GoBack = true },
        new() { BlockName = "Select_No", ResetIsClicked = true },
    };

    static readonly string[] DisableLoadSceneBlockNames = { "Select_Yes" };

    static readonly string[] DisableGoBackBlockNames = { "Select_Yes" };

    static readonly string[] DisableIsClickedResetBlockNames = { "Select_No" };

    static readonly string[] DisconnectBackBlockNames = { "BackSpace_Clicked" };

    static readonly HashSet<string> BackBlockNamesForVerification = new(
        new[] { BackRoute.BlockName, "Select_Yes", "Select_No" },
        StringComparer.Ordinal);

    sealed class PanelCloseTarget
    {
        public string PanelCloseId;
        public string PanelObjectName;
    }

    sealed class PanelBackspaceTarget
    {
        public string PanelName;
        public string PanelCloseId;
    }

    [MenuItem("Tools/godlotto/Migrate WifeRoom Phase R2-A Click Entry")]
    public static void MigrateWifeRoomClickEntry()
    {
        try
        {
            if (MigrateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WifeRoomSceneMigrator] WifeRoom Phase R2-A migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WifeRoomSceneMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyWifeRoomClickMigration();
    }

    [MenuItem("Tools/godlotto/Migrate WifeRoom Phase R2-B Unlock Entry")]
    public static void MigrateWifeRoomUnlockEntry()
    {
        try
        {
            if (MigrateUnlockScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WifeRoomSceneMigrator] WifeRoom Phase R2-B unlock migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WifeRoomSceneMigrator] R2-B migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyWifeRoomUnlockMigration();
    }

    [MenuItem("Tools/godlotto/Verify WifeRoom Unlock Migration")]
    public static void VerifyWifeRoomUnlockMigration()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        int violations = CountDirectUnlockSuccessExecuteBlockCalls(scene);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] R2-B verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[WifeRoomSceneMigrator] R2-B verification failed: WifeRoomPuzzleController missing.");
        }
        else
        {
            violations += VerifyUnlockRoute(controller);
            violations += VerifyUnlockEventSources(scene);
        }

        if (violations == 0)
            Debug.Log("[WifeRoomSceneMigrator] R2-B verification passed: unlock routed through WifeRoomPuzzleController.");
        else
            Debug.LogError($"[WifeRoomSceneMigrator] R2-B verification failed: {violations} issue(s).");
    }

    [MenuItem("Tools/godlotto/Migrate WifeRoom Phase R2-C Panel And Back Return")]
    public static void MigrateWifeRoomPanelAndBackReturn()
    {
        try
        {
            if (MigratePanelAndBackScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[WifeRoomSceneMigrator] WifeRoom Phase R2-C panel/back migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WifeRoomSceneMigrator] R2-C migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyWifeRoomPanelAndBackMigration();
    }

    [MenuItem("Tools/godlotto/Verify WifeRoom Panel And Back Migration")]
    public static void VerifyWifeRoomPanelAndBackMigration()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            PanelBackspaceBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] R2-C verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[WifeRoomSceneMigrator] R2-C verification failed: WifeRoomPuzzleController missing.");
        }
        else
        {
            violations += VerifyPanelCloseBindings(controller, scene);
            violations += VerifyPanelBackspaceClosers(scene, controller);
            violations += VerifyBackRoute(controller);
            violations += VerifyBackOutcomes(controller);
            violations += RoomInteractionSceneMigrationEditor.VerifyRibbonBackButtonsWired(scene, controller);
        }

        violations += RoomInteractionSceneMigrationEditor.CountEnabledLoadSceneCommandsInBlocks(
            flowchart,
            DisableLoadSceneBlockNames);
        violations += RoomInteractionSceneMigrationEditor.CountEnabledGoBackCommandsInBlocks(
            flowchart,
            DisableGoBackBlockNames);

        Block backBlock = flowchart.GetComponents<Block>()
            .FirstOrDefault(block => block.BlockName == BackRoute.BlockName);
        if (backBlock != null && backBlock._EventHandler != null)
        {
            violations++;
            Debug.LogError("[WifeRoomSceneMigrator] R2-C: BackSpace_Clicked still has an event handler.");
        }

        if (violations == 0)
            Debug.Log("[WifeRoomSceneMigrator] R2-C verification passed: panel close and back return routed through WifeRoomPuzzleController.");
        else
            Debug.LogError($"[WifeRoomSceneMigrator] R2-C verification failed: {violations} issue(s).");
    }

    [MenuItem("Tools/godlotto/Verify WifeRoom Click Migration")]
    public static void VerifyWifeRoomClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            ClickBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] Verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[WifeRoomSceneMigrator] Verification failed: WifeRoomPuzzleController missing on Flowchart.");
        }
        else
        {
            violations += VerifyClickRoutes(controller);
        }

        foreach (string blockName in DisconnectObjectClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        if (violations == 0)
            Debug.Log("[WifeRoomSceneMigrator] Verification passed: click entry points routed through WifeRoomPuzzleController.");
        else
            Debug.LogError($"[WifeRoomSceneMigrator] Verification failed: {violations} issue(s).");
    }

    static bool MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<WifeRoomPuzzleController>();

        BackNavigator backNavigator = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<BackNavigator>(scene)
            .FirstOrDefault();

        var so = new SerializedObject(controller);
        so.FindProperty("flowchart").objectReferenceValue = flowchart;
        so.FindProperty("backNavigator").objectReferenceValue = backNavigator;
        so.FindProperty("enableDebugLogging").boolValue = false;
        RoomInteractionSceneMigrationEditor.ApplyWorldClickBindings(
            so.FindProperty("worldClicks"),
            scene,
            WorldClickTargets,
            scene.name);
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            ClickRoutes);
        RoomInteractionSceneMigrationEditor.WriteOutcomes(
            so.FindProperty("blockOutcomes"),
            Array.Empty<RoomInteractionSceneMigrationEditor.BlockOutcomeSpec>());
        so.ApplyModifiedPropertiesWithoutUndo();

        RoomInteractionSceneMigrationEditor.DisconnectObjectClickedHandlers(
            flowchart,
            DisconnectObjectClickedBlockNames);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static bool MigrateUnlockScene()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] R2-B: WifeRoomPuzzleController missing. Run R2-A click migration first.");
            return false;
        }

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            MergeRoutes(ClickRoutes, UnlockRoute));
        so.ApplyModifiedPropertiesWithoutUndo();

        RewireUnlockEventSources(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] MergeRoutes(
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] baseRoutes,
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec extraRoute)
    {
        if (extraRoute == null)
            return baseRoutes;

        bool alreadyPresent = baseRoutes.Any(route =>
            string.Equals(route.InteractionId, extraRoute.InteractionId, StringComparison.Ordinal));
        if (alreadyPresent)
            return baseRoutes;

        var merged = new RoomInteractionSceneMigrationEditor.InteractionRouteSpec[baseRoutes.Length + 1];
        Array.Copy(baseRoutes, merged, baseRoutes.Length);
        merged[baseRoutes.Length] = extraRoute;
        return merged;
    }

    static void RewireUnlockEventSources(
        UnityEngine.SceneManagement.Scene scene,
        WifeRoomPuzzleController controller)
    {
        foreach (CombinationLock combinationLock in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<CombinationLock>(scene))
        {
            var lockSo = new SerializedObject(combinationLock);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    lockSo,
                    "onUnlockSuccess",
                    UnlockRoute.BlockName))
                continue;

            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                lockSo,
                "onUnlockSuccess",
                controller,
                RoomInteractionSceneMigrationEditor.WifeRoomControllerTypeName,
                nameof(WifeRoomPuzzleController.OnInteraction),
                UnlockRoute.InteractionId);
            lockSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combinationLock);
        }

        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    dropZoneSo,
                    "onUnlock",
                    UnlockRoute.BlockName))
                continue;

            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                dropZoneSo,
                "onUnlock",
                controller,
                RoomInteractionSceneMigrationEditor.WifeRoomControllerTypeName,
                nameof(WifeRoomPuzzleController.OnInteraction),
                UnlockRoute.InteractionId);
            dropZoneSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dropZone);
        }
    }

    static int CountDirectUnlockSuccessExecuteBlockCalls(UnityEngine.SceneManagement.Scene scene)
    {
        int count = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            UnlockBlockNamesForVerification);

        foreach (CombinationLock combinationLock in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<CombinationLock>(scene))
        {
            var lockSo = new SerializedObject(combinationLock);
            if (RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    lockSo,
                    "onUnlockSuccess",
                    UnlockRoute.BlockName))
            {
                count++;
                Debug.LogError(
                    $"[WifeRoomSceneMigrator] CombinationLock '{combinationLock.gameObject.name}' still calls ExecuteBlock('{UnlockRoute.BlockName}').",
                    combinationLock);
            }
        }

        return count;
    }

    static int VerifyUnlockRoute(WifeRoomPuzzleController controller)
    {
        var so = new SerializedObject(controller);
        SerializedProperty routes = so.FindProperty("routes");
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            if (route.FindPropertyRelative("interactionId").stringValue != UnlockRoute.InteractionId)
                continue;
            if (route.FindPropertyRelative("fungusBlockName").stringValue == UnlockRoute.BlockName)
                return 0;

            Debug.LogError("[WifeRoomSceneMigrator] unlock route maps to wrong Fungus block.");
            return 1;
        }

        Debug.LogError("[WifeRoomSceneMigrator] unlock route missing on WifeRoomPuzzleController.");
        return 1;
    }

    static int VerifyUnlockEventSources(UnityEngine.SceneManagement.Scene scene)
    {
        int violations = 0;

        foreach (CombinationLock combinationLock in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<CombinationLock>(scene))
        {
            var lockSo = new SerializedObject(combinationLock);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    lockSo,
                    "onUnlockSuccess",
                    UnlockRoute.BlockName))
                continue;

            violations++;
            Debug.LogError(
                $"[WifeRoomSceneMigrator] CombinationLock '{combinationLock.gameObject.name}' still calls ExecuteBlock('{UnlockRoute.BlockName}').",
                combinationLock);
        }

        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    dropZoneSo,
                    "onUnlock",
                    UnlockRoute.BlockName))
                continue;

            violations++;
            Debug.LogError(
                $"[WifeRoomSceneMigrator] WorldItemDropZone '{dropZone.gameObject.name}' still calls ExecuteBlock('{UnlockRoute.BlockName}').",
                dropZone);
        }

        return violations;
    }

    static bool MigratePanelAndBackScene()
    {
        var scene = EditorSceneManager.OpenScene(WifeRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<WifeRoomPuzzleController>();
        if (controller == null)
        {
            Debug.LogError("[WifeRoomSceneMigrator] R2-C: WifeRoomPuzzleController missing. Run R2-A/R2-B first.");
            return false;
        }

        BackNavigator backNavigator = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<BackNavigator>(scene)
            .FirstOrDefault();

        var panelCloseSpecs = new List<RoomInteractionSceneMigrationEditor.PanelCloseSpec>();
        foreach (PanelCloseTarget target in PanelCloseTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelObjectName);
            if (panel == null)
            {
                Debug.LogWarning(
                    $"[WifeRoomSceneMigrator] R2-C: panel '{target.PanelObjectName}' not found for '{target.PanelCloseId}'.");
                continue;
            }

            panelCloseSpecs.Add(new RoomInteractionSceneMigrationEditor.PanelCloseSpec
            {
                PanelCloseId = target.PanelCloseId,
                Panel = panel,
            });
        }

        var so = new SerializedObject(controller);
        so.FindProperty("backNavigator").objectReferenceValue = backNavigator;
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] existingRoutes =
            ReadExistingRoutes(so.FindProperty("routes"));
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            MergeRoutes(existingRoutes, BackRoute));
        RoomInteractionSceneMigrationEditor.WriteOutcomes(
            so.FindProperty("blockOutcomes"),
            BackOutcomes);
        RoomInteractionSceneMigrationEditor.WritePanelCloses(
            so.FindProperty("panelCloses"),
            panelCloseSpecs.ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();

        WirePanelBackspaceClosers(scene, controller);

        RoomInteractionSceneMigrationEditor.DisconnectObjectClickedHandlers(
            flowchart,
            DisconnectBackBlockNames);
        RoomInteractionSceneMigrationEditor.DisableLoadSceneInBlocks(flowchart, DisableLoadSceneBlockNames);
        RoomInteractionSceneMigrationEditor.DisableGoBackCommandsInBlocks(flowchart, DisableGoBackBlockNames);
        RoomInteractionSceneMigrationEditor.DisableIsClickedResetInBlocks(
            flowchart,
            DisableIsClickedResetBlockNames);
        RoomInteractionSceneMigrationEditor.RewireRibbonBackButtons(
            scene,
            controller,
            RoomInteractionSceneMigrationEditor.WifeRoomControllerTypeName);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ReadExistingRoutes(
        SerializedProperty routesProp)
    {
        var routes = new RoomInteractionSceneMigrationEditor.InteractionRouteSpec[routesProp.arraySize];
        for (int i = 0; i < routesProp.arraySize; i++)
        {
            SerializedProperty element = routesProp.GetArrayElementAtIndex(i);
            routes[i] = new RoomInteractionSceneMigrationEditor.InteractionRouteSpec
            {
                InteractionId = element.FindPropertyRelative("interactionId").stringValue,
                BlockName = element.FindPropertyRelative("fungusBlockName").stringValue,
            };
        }

        return routes;
    }

    static void WirePanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        WifeRoomPuzzleController controller)
    {
        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
            {
                Debug.LogWarning($"[WifeRoomSceneMigrator] R2-C: backspace target panel '{target.PanelName}' not found.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                Debug.LogWarning($"[WifeRoomSceneMigrator] R2-C: PanelBackspaceCloser missing on '{target.PanelName}'.");
                continue;
            }

            RoomInteractionSceneMigrationEditor.WirePanelBackspaceCloser(
                closer,
                controller,
                target.PanelCloseId);
        }
    }

    static int VerifyPanelCloseBindings(WifeRoomPuzzleController controller, UnityEngine.SceneManagement.Scene scene)
    {
        int violations = 0;
        var so = new SerializedObject(controller);
        SerializedProperty panelCloses = so.FindProperty("panelCloses");
        var foundIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < panelCloses.arraySize; i++)
        {
            SerializedProperty element = panelCloses.GetArrayElementAtIndex(i);
            string panelCloseId = element.FindPropertyRelative("panelCloseId").stringValue;
            if (!string.IsNullOrWhiteSpace(panelCloseId))
                foundIds.Add(panelCloseId);

            if (element.FindPropertyRelative("panel").objectReferenceValue == null)
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: panelCloses[{i}] has no panel reference.");
            }
        }

        foreach (PanelCloseTarget expected in PanelCloseTargets)
        {
            if (!foundIds.Contains(expected.PanelCloseId))
            {
                violations++;
                Debug.LogError(
                    $"[WifeRoomSceneMigrator] R2-C: missing panel close binding '{expected.PanelCloseId}'.");
            }
        }

        return violations;
    }

    static int VerifyPanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        WifeRoomPuzzleController controller)
    {
        int violations = 0;

        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: panel '{target.PanelName}' not found.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: PanelBackspaceCloser missing on '{target.PanelName}'.");
                continue;
            }

            var closerSo = new SerializedObject(closer);
            if (closerSo.FindProperty("interactionController").objectReferenceValue != controller)
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: '{target.PanelName}' closer controller reference is wrong.");
            }

            string panelCloseId = closerSo.FindProperty("panelCloseInteractionId").stringValue;
            if (panelCloseId != target.PanelCloseId)
            {
                violations++;
                Debug.LogError(
                    $"[WifeRoomSceneMigrator] R2-C: '{target.PanelName}' closer panelCloseInteractionId expected '{target.PanelCloseId}'.");
            }

            if (!string.IsNullOrWhiteSpace(closerSo.FindProperty("executeBlockName").stringValue))
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: '{target.PanelName}' closer still has executeBlockName set.");
            }
        }

        return violations;
    }

    static int VerifyBackRoute(WifeRoomPuzzleController controller)
    {
        var so = new SerializedObject(controller);
        SerializedProperty routes = so.FindProperty("routes");
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            if (route.FindPropertyRelative("interactionId").stringValue != BackRoute.InteractionId)
                continue;

            if (route.FindPropertyRelative("fungusBlockName").stringValue == BackRoute.BlockName)
                return 0;

            Debug.LogError("[WifeRoomSceneMigrator] R2-C: back route maps to unexpected block.");
            return 1;
        }

        Debug.LogError("[WifeRoomSceneMigrator] R2-C: missing back route on WifeRoomPuzzleController.");
        return 1;
    }

    static int VerifyBackOutcomes(WifeRoomPuzzleController controller)
    {
        int violations = 0;
        var so = new SerializedObject(controller);
        SerializedProperty outcomes = so.FindProperty("blockOutcomes");
        var found = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < outcomes.arraySize; i++)
        {
            SerializedProperty outcome = outcomes.GetArrayElementAtIndex(i);
            string blockName = outcome.FindPropertyRelative("blockName").stringValue;
            if (string.IsNullOrWhiteSpace(blockName))
                continue;

            found.Add(blockName);
            if (blockName == "Select_Yes" && !outcome.FindPropertyRelative("goBack").boolValue)
            {
                violations++;
                Debug.LogError("[WifeRoomSceneMigrator] R2-C: Select_Yes outcome must set goBack.");
            }

            if (blockName == "Select_No" && !outcome.FindPropertyRelative("resetIsClicked").boolValue)
            {
                violations++;
                Debug.LogError("[WifeRoomSceneMigrator] R2-C: Select_No outcome must set resetIsClicked.");
            }
        }

        foreach (RoomInteractionSceneMigrationEditor.BlockOutcomeSpec expected in BackOutcomes)
        {
            if (!found.Contains(expected.BlockName))
            {
                violations++;
                Debug.LogError($"[WifeRoomSceneMigrator] R2-C: missing block outcome for '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyClickRoutes(WifeRoomPuzzleController controller)
    {
        int violations = 0;
        var so = new SerializedObject(controller);
        SerializedProperty routes = so.FindProperty("routes");
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            string interactionId = route.FindPropertyRelative("interactionId").stringValue;
            string blockName = route.FindPropertyRelative("fungusBlockName").stringValue;
            if (!string.IsNullOrWhiteSpace(interactionId))
                found[interactionId] = blockName;
        }

        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec expected in ClickRoutes)
        {
            if (!found.TryGetValue(expected.InteractionId, out string actualBlock)
                || actualBlock != expected.BlockName)
            {
                violations++;
                Debug.LogError(
                    $"[WifeRoomSceneMigrator] Missing or wrong route for '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }
}
