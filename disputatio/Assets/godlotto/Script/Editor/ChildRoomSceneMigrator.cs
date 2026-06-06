using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChildRoomSceneMigrator
{
    const string ChildRoomScenePath = "Assets/Scenes/Mokotan/Second Floor/ChildRoom.unity";

    static readonly RoomInteractionSceneMigrationEditor.WorldClickTarget[] WorldClickTargets =
    {
        new() { InteractionId = "bedfloor", GameObjectName = "Bedfloor" },
        new() { InteractionId = "drawer", GameObjectName = "Drawer" },
        new() { InteractionId = "chest", GameObjectName = "Chest" },
        new() { InteractionId = "table", GameObjectName = "Table" },
        new() { InteractionId = "parrot", GameObjectName = "Parret" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ClickRoutes =
    {
        new() { InteractionId = "bedfloor", BlockName = "Bedfloor_Clicked" },
        new() { InteractionId = "drawer", BlockName = "Drawer_Clicked" },
        new() { InteractionId = "chest", BlockName = "Chest_Clicked" },
        new() { InteractionId = "table", BlockName = "Table_Clicked" },
        new() { InteractionId = "parrot", BlockName = "Parrot_Clicked" },
        new() { InteractionId = "button", BlockName = "Button_Clicked" },
        new() { InteractionId = "drawer_open", BlockName = "DrawerOpen" },
        new() { InteractionId = "drawer_close", BlockName = "DrawerClose" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] SealRoutes =
    {
        new() { InteractionId = "seal5", BlockName = "Drag_seal5" },
        new() { InteractionId = "seal6", BlockName = "Drag_seal6" },
        new() { InteractionId = "seal7", BlockName = "Drag_seal7" },
    };

    static readonly HashSet<string> SealBlockNamesForVerification = new(
        SealRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec AllSealsCompleteRoute =
        new() { InteractionId = "all_seals_complete", BlockName = "allSealsComplete" };

    static readonly HashSet<string> AllSealsCompleteBlockNamesForVerification = new(
        new[] { AllSealsCompleteRoute.BlockName },
        StringComparer.Ordinal);

    static readonly string[] DisconnectObjectClickedBlockNames =
    {
        "Bedfloor_Clicked",
        "Drawer_Clicked",
        "Chest_Clicked",
        "Table_Clicked",
        "Parrot_Clicked",
    };

    static readonly string[] DisconnectButtonClickedBlockNames =
    {
        "Button_Clicked",
    };

    static readonly HashSet<string> ClickBlockNamesForVerification = new(
        ClickRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    const string DrawerOpenObjectName = "DrawerOpen";
    const string DrawerCloseObjectName = "DrawerClose";
    const string ButtonClickTargetObjectName = "SetButton";

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-A Click Entry")]
    public static void MigrateChildRoomClickEntry()
    {
        try
        {
            if (MigrateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[ChildRoomSceneMigrator] ChildRoom Phase R5-A click migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChildRoomSceneMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyChildRoomClickMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-A Verify Click Entry")]
    public static void VerifyChildRoomClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            ClickBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] Verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[ChildRoomSceneMigrator] Verification failed: ChildRoomPuzzleController missing on Flowchart.");
        }
        else
        {
            violations += VerifyRoutes(controller);
        }

        foreach (string blockName in DisconnectObjectClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[ChildRoomSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        foreach (string blockName in DisconnectButtonClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[ChildRoomSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        violations += VerifyDrawerUiButtonsWired(scene, controller);

        if (violations == 0)
            Debug.Log("[ChildRoomSceneMigrator] Verification passed: click entry points routed through ChildRoomPuzzleController.");
        else
            Debug.LogError($"[ChildRoomSceneMigrator] Verification failed: {violations} issue(s).");
    }

    static bool MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<ChildRoomPuzzleController>();

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

        UnityEngine.UI.Button buttonTarget = RoomInteractionSceneMigrationEditor.TryResolveUiButtonForBlock(
            flowchart,
            "Button_Clicked");

        RoomInteractionSceneMigrationEditor.DisconnectObjectClickedHandlers(
            flowchart,
            DisconnectObjectClickedBlockNames);
        RoomInteractionSceneMigrationEditor.DisconnectFungusButtonClickedHandlers(
            flowchart,
            DisconnectButtonClickedBlockNames);

        WireButtonClickEntry(scene, controller, buttonTarget);
        WireDrawerUiButtons(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void WireDrawerUiButtons(UnityEngine.SceneManagement.Scene scene, ChildRoomPuzzleController controller)
    {
        WireDrawerUiButton(scene, controller, DrawerOpenObjectName, "drawer_open");
        WireDrawerUiButton(scene, controller, DrawerCloseObjectName, "drawer_close");
    }

    static void WireDrawerUiButton(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller,
        string objectName,
        string interactionId)
    {
        GameObject target = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[ChildRoomSceneMigrator] Drawer UI '{objectName}' not found.");
            return;
        }

        var button = target.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
        {
            Debug.LogWarning($"[ChildRoomSceneMigrator] '{objectName}' has no UI Button component.");
            return;
        }

        var buttonSo = new SerializedObject(button);
        RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
            buttonSo,
            "m_OnClick",
            controller,
            RoomInteractionSceneMigrationEditor.ChildRoomControllerTypeName,
            nameof(ChildRoomPuzzleController.OnInteraction),
            interactionId);
        buttonSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(button);
    }

    static void WireButtonClickEntry(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller,
        UnityEngine.UI.Button resolvedButton)
    {
        GameObject buttonClickGo = resolvedButton != null
            ? resolvedButton.gameObject
            : RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, ButtonClickTargetObjectName);

        if (buttonClickGo == null)
        {
            Debug.LogWarning(
                "[ChildRoomSceneMigrator] Button_Clicked target not found; route registered but no UI forwarder wired.");
            return;
        }

        if (buttonClickGo.GetComponent<Collider2D>() != null &&
            buttonClickGo.GetComponent<UnityEngine.UI.Button>() == null)
        {
            Debug.LogWarning(
                "[ChildRoomSceneMigrator] Button_Clicked target has Collider2D but no UI Button; add a worldClicks binding manually.");
            return;
        }

        RoomInteractionSceneMigrationEditor.EnsureUiClickForwarder(
            buttonClickGo,
            controller,
            "button");
    }

    static int VerifyRoutes(ChildRoomPuzzleController controller)
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
                    $"[ChildRoomSceneMigrator] Missing or wrong route '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyDrawerUiButtonsWired(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller)
    {
        int violations = 0;
        violations += VerifyDrawerUiButtonWired(scene, controller, DrawerOpenObjectName, "drawer_open");
        violations += VerifyDrawerUiButtonWired(scene, controller, DrawerCloseObjectName, "drawer_close");
        return violations;
    }

    static int VerifyDrawerUiButtonWired(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller,
        string objectName,
        string interactionId)
    {
        GameObject target = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[ChildRoomSceneMigrator] Verify: '{objectName}' not found.");
            return 0;
        }

        var button = target.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
        {
            Debug.LogError($"[ChildRoomSceneMigrator] Verify: '{objectName}' has no Button.");
            return 1;
        }

        var buttonSo = new SerializedObject(button);
        SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls == null)
            return 1;

        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller)
                continue;
            if (call.FindPropertyRelative("m_MethodName").stringValue
                != nameof(RoomInteractionController.OnInteraction))
                continue;
            if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue != interactionId)
                continue;

            return 0;
        }

        Debug.LogError(
            $"[ChildRoomSceneMigrator] '{objectName}' Button is not wired to OnInteraction('{interactionId}').",
            button);
        return 1;
    }

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-B Seal Drop")]
    public static void MigrateChildRoomSealDrop()
    {
        try
        {
            if (MigrateSealDropScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[ChildRoomSceneMigrator] ChildRoom Phase R5-B seal drop migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChildRoomSceneMigrator] R5-B migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyChildRoomSealDropMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-B Verify Seal Drop")]
    public static void VerifyChildRoomSealDropMigration()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            SealBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-B verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[ChildRoomSceneMigrator] R5-B verification failed: ChildRoomPuzzleController missing.");
        }
        else
        {
            violations += VerifySealRoutes(controller);
            violations += VerifySealDropZonesWired(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[ChildRoomSceneMigrator] R5-B verification passed: seal drops routed through ChildRoomPuzzleController.");
        else
            Debug.LogError($"[ChildRoomSceneMigrator] R5-B verification failed: {violations} issue(s).");
    }

    static bool MigrateSealDropScene()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-B: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-B: ChildRoomPuzzleController missing. Run R5-A first.");
            return false;
        }

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] mergedRoutes =
            MergeRoutes(ReadRoutes(so.FindProperty("routes")), SealRoutes);
        RoomInteractionSceneMigrationEditor.WriteRoutes(so.FindProperty("routes"), mergedRoutes);
        so.ApplyModifiedPropertiesWithoutUndo();

        RewireSealDropZones(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ReadRoutes(SerializedProperty routesProp)
    {
        if (routesProp == null || routesProp.arraySize == 0)
            return Array.Empty<RoomInteractionSceneMigrationEditor.InteractionRouteSpec>();

        var routes = new RoomInteractionSceneMigrationEditor.InteractionRouteSpec[routesProp.arraySize];
        for (int i = 0; i < routesProp.arraySize; i++)
        {
            SerializedProperty route = routesProp.GetArrayElementAtIndex(i);
            routes[i] = new RoomInteractionSceneMigrationEditor.InteractionRouteSpec
            {
                InteractionId = route.FindPropertyRelative("interactionId").stringValue,
                BlockName = route.FindPropertyRelative("fungusBlockName").stringValue,
            };
        }

        return routes;
    }

    static RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] MergeRoutes(
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] baseRoutes,
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] extraRoutes)
    {
        var merged = new List<RoomInteractionSceneMigrationEditor.InteractionRouteSpec>(baseRoutes);
        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec extra in extraRoutes)
        {
            int existingIndex = merged.FindIndex(route =>
                string.Equals(route.InteractionId, extra.InteractionId, StringComparison.Ordinal));
            if (existingIndex >= 0)
                merged[existingIndex] = extra;
            else
                merged.Add(extra);
        }

        return merged.ToArray();
    }

    static void RewireSealDropZones(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller)
    {
        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            bool rewired = false;

            foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec sealRoute in SealRoutes)
            {
                if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                        dropZoneSo,
                        "onUnlock",
                        sealRoute.BlockName))
                    continue;

                RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                    dropZoneSo,
                    "onUnlock",
                    controller,
                    RoomInteractionSceneMigrationEditor.ChildRoomControllerTypeName,
                    nameof(ChildRoomPuzzleController.OnInteraction),
                    sealRoute.InteractionId);
                rewired = true;
                break;
            }

            if (rewired)
            {
                dropZoneSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(dropZone);
            }
        }
    }

    static int VerifySealRoutes(ChildRoomPuzzleController controller)
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

        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec expected in SealRoutes)
        {
            if (!found.TryGetValue(expected.InteractionId, out string actualBlock)
                || actualBlock != expected.BlockName)
            {
                violations++;
                Debug.LogError(
                    $"[ChildRoomSceneMigrator] R5-B missing or wrong route '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifySealDropZonesWired(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller)
    {
        int violations = 0;

        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec sealRoute in SealRoutes)
            {
                if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                        dropZoneSo,
                        "onUnlock",
                        sealRoute.BlockName))
                    continue;

                violations++;
                Debug.LogError(
                    $"[ChildRoomSceneMigrator] R5-B: WorldItemDropZone '{dropZone.gameObject.name}' still calls ExecuteBlock('{sealRoute.BlockName}').",
                    dropZone);
            }

            SerializedProperty calls = dropZoneSo.FindProperty("onUnlock.m_PersistentCalls.m_Calls");
            if (calls == null)
                continue;

            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller)
                    continue;
                if (call.FindPropertyRelative("m_MethodName").stringValue
                    != nameof(RoomInteractionController.OnInteraction))
                    continue;

                string interactionId = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                if (interactionId is "seal5" or "seal6" or "seal7")
                    continue;

                violations++;
                Debug.LogError(
                    $"[ChildRoomSceneMigrator] R5-B: '{dropZone.gameObject.name}' onUnlock calls OnInteraction('{interactionId}') unexpectedly.",
                    dropZone);
            }
        }

        return violations;
    }

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-C All Seals Complete")]
    public static void MigrateChildRoomAllSealsComplete()
    {
        try
        {
            if (MigrateAllSealsCompleteScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[ChildRoomSceneMigrator] ChildRoom Phase R5-C allSealsComplete migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChildRoomSceneMigrator] R5-C migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyChildRoomAllSealsCompleteMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/ChildRoom R5-C Verify All Seals Complete")]
    public static void VerifyChildRoomAllSealsCompleteMigration()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        int violations = CountDirectAllSealsCompleteExecuteBlocks(scene);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-C verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[ChildRoomSceneMigrator] R5-C verification failed: ChildRoomPuzzleController missing.");
        }
        else
        {
            violations += VerifyAllSealsCompleteRoute(controller);
            violations += VerifySealManagerWired(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[ChildRoomSceneMigrator] R5-C verification passed: allSealsComplete routed through ChildRoomPuzzleController.");
        else
            Debug.LogError($"[ChildRoomSceneMigrator] R5-C verification failed: {violations} issue(s).");
    }

    static bool MigrateAllSealsCompleteScene()
    {
        var scene = EditorSceneManager.OpenScene(ChildRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-C: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<ChildRoomPuzzleController>();
        if (controller == null)
        {
            Debug.LogError("[ChildRoomSceneMigrator] R5-C: ChildRoomPuzzleController missing. Run R5-A first.");
            return false;
        }

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] mergedRoutes =
            MergeRoutes(ReadRoutes(so.FindProperty("routes")), new[] { AllSealsCompleteRoute });
        RoomInteractionSceneMigrationEditor.WriteRoutes(so.FindProperty("routes"), mergedRoutes);
        so.ApplyModifiedPropertiesWithoutUndo();

        RewireSealManagerOnAllSealsComplete(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void RewireSealManagerOnAllSealsComplete(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller)
    {
        foreach (SealManager sealManager in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<SealManager>(scene))
        {
            var sealManagerSo = new SerializedObject(sealManager);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    sealManagerSo,
                    "onAllSealsComplete",
                    AllSealsCompleteRoute.BlockName))
                continue;

            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                sealManagerSo,
                "onAllSealsComplete",
                controller,
                RoomInteractionSceneMigrationEditor.ChildRoomControllerTypeName,
                nameof(ChildRoomPuzzleController.OnInteraction),
                AllSealsCompleteRoute.InteractionId);
            sealManagerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sealManager);
        }
    }

    static int CountDirectAllSealsCompleteExecuteBlocks(UnityEngine.SceneManagement.Scene scene)
    {
        int count = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            AllSealsCompleteBlockNamesForVerification);

        foreach (SealManager sealManager in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<SealManager>(scene))
        {
            var sealManagerSo = new SerializedObject(sealManager);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    sealManagerSo,
                    "onAllSealsComplete",
                    AllSealsCompleteRoute.BlockName))
                continue;

            count++;
            Debug.LogError(
                $"[ChildRoomSceneMigrator] R5-C: SealManager on '{sealManager.gameObject.name}' still calls ExecuteBlock('{AllSealsCompleteRoute.BlockName}').",
                sealManager);
        }

        return count;
    }

    static int VerifyAllSealsCompleteRoute(ChildRoomPuzzleController controller)
    {
        var so = new SerializedObject(controller);
        SerializedProperty routes = so.FindProperty("routes");

        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            if (route.FindPropertyRelative("interactionId").stringValue != AllSealsCompleteRoute.InteractionId)
                continue;
            if (route.FindPropertyRelative("fungusBlockName").stringValue == AllSealsCompleteRoute.BlockName)
                return 0;

            Debug.LogError("[ChildRoomSceneMigrator] R5-C: all_seals_complete route maps to wrong Fungus block.");
            return 1;
        }

        Debug.LogError("[ChildRoomSceneMigrator] R5-C: all_seals_complete route missing on ChildRoomPuzzleController.");
        return 1;
    }

    static int VerifySealManagerWired(
        UnityEngine.SceneManagement.Scene scene,
        ChildRoomPuzzleController controller)
    {
        int violations = 0;
        bool foundSealManager = false;

        foreach (SealManager sealManager in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<SealManager>(scene))
        {
            foundSealManager = true;
            var sealManagerSo = new SerializedObject(sealManager);

            if (RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    sealManagerSo,
                    "onAllSealsComplete",
                    AllSealsCompleteRoute.BlockName))
            {
                violations++;
                Debug.LogError(
                    $"[ChildRoomSceneMigrator] R5-C: SealManager '{sealManager.gameObject.name}' still calls ExecuteBlock directly.",
                    sealManager);
                continue;
            }

            SerializedProperty calls = sealManagerSo.FindProperty("onAllSealsComplete.m_PersistentCalls.m_Calls");
            if (calls == null)
            {
                violations++;
                continue;
            }

            bool wired = false;
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller)
                    continue;
                if (call.FindPropertyRelative("m_MethodName").stringValue
                    != nameof(RoomInteractionController.OnInteraction))
                    continue;
                if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue
                    != AllSealsCompleteRoute.InteractionId)
                    continue;

                wired = true;
                break;
            }

            if (!wired)
            {
                violations++;
                Debug.LogError(
                    $"[ChildRoomSceneMigrator] R5-C: SealManager '{sealManager.gameObject.name}' is not wired to OnInteraction('{AllSealsCompleteRoute.InteractionId}').",
                    sealManager);
            }
        }

        if (!foundSealManager)
        {
            violations++;
            Debug.LogError("[ChildRoomSceneMigrator] R5-C: SealManager not found in ChildRoom scene.");
        }

        return violations;
    }
}
