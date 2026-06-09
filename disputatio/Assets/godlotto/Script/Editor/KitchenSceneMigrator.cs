using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KitchenSceneMigrator
{
    const string KitchenScenePath = "Assets/Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity";

    static readonly RoomInteractionSceneMigrationEditor.WorldClickTarget[] WorldClickTargets =
    {
        new() { InteractionId = "door", GameObjectName = "Door_toUtility room" },
        new() { InteractionId = "door_to_hall", GameObjectName = "Door_toHall" },
        new() { InteractionId = "trashbox", GameObjectName = "TrashBox" },
        new() { InteractionId = "refrigerator", GameObjectName = "refrigerator" },
        new() { InteractionId = "sink", GameObjectName = "Sink" },
        new() { InteractionId = "burner", GameObjectName = "Burner" },
        new() { InteractionId = "fripan", GameObjectName = "Fripan" },
        new() { InteractionId = "parret", GameObjectName = "Parret" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ClickRoutes =
    {
        new() { InteractionId = "door", BlockName = "Door_Clicked" },
        new() { InteractionId = "door_to_hall", BlockName = "Door_toHall_Clicked" },
        new() { InteractionId = "trashbox", BlockName = "TrashBox_Clicked" },
        new() { InteractionId = "refrigerator", BlockName = "refrigeratorClicked" },
        new() { InteractionId = "sink", BlockName = "Sink" },
        new() { InteractionId = "bottle", BlockName = "Bottle_Clicked" },
        new() { InteractionId = "burner", BlockName = "burner" },
        new() { InteractionId = "fripan", BlockName = "fripan" },
        new() { InteractionId = "parret", BlockName = "parret" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] UiClickRoutes =
    {
        new() { InteractionId = "burner", BlockName = "burner" },
        new() { InteractionId = "faucet", BlockName = "Faucet" },
        new() { InteractionId = "filled_bottle", BlockName = "FilledBottle" },
        new() { InteractionId = "bottle", BlockName = "Bottle_Clicked" },
    };

    static readonly HashSet<string> UiExecuteBlockNamesForVerification = new(
        UiClickRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] DropRoutes =
    {
        new() { InteractionId = "bottle_drag", BlockName = "Bottle_Dragged" },
        new() { InteractionId = "food_drag", BlockName = "Food_Dragged" },
    };

    static readonly HashSet<string> DropExecuteBlockNamesForVerification = new(
        DropRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    const string PanelBackspaceCloseId = "panel_backspace";

    static readonly string[] PanelSetActiveBlockNames =
    {
        "PannelBackspace",
        "forActiveBurnerPannel",
        "burner",
        "fripan",
        "onFri",
        "offFri",
        "parret",
    };

    static readonly (string ObjectName, string OpenMethod, string CloseMethod)[] PanelMethodMap =
    {
        ("burner", nameof(KitchenPanelRegistry.OpenBurnerPanel), nameof(KitchenPanelRegistry.CloseBurnerPanel)),
        ("firpan_Panel", nameof(KitchenPanelRegistry.OpenFripanPanel), nameof(KitchenPanelRegistry.CloseFripanPanel)),
        ("Parret", nameof(KitchenPanelRegistry.OpenParrotPanel), nameof(KitchenPanelRegistry.CloseParrotPanel)),
        ("Sink_Pannel", nameof(KitchenPanelRegistry.OpenSinkPanel), nameof(KitchenPanelRegistry.CloseSinkPanel)),
        ("Bottle", nameof(KitchenPanelRegistry.OpenBottlePanel), nameof(KitchenPanelRegistry.CloseBottlePanel)),
    };

    sealed class PanelBackspaceTarget
    {
        public string PanelName;
        public string PanelCloseId;
    }

    static readonly PanelBackspaceTarget[] PanelBackspaceTargets =
    {
        new() { PanelName = "firpan_Panel", PanelCloseId = PanelBackspaceCloseId },
        new() { PanelName = "Sink_Pannel", PanelCloseId = PanelBackspaceCloseId },
    };

    /// <summary>
    /// Kitchen 씬에 BackspaceCornerFold가 없는 패널. 닫기는 상위 패널 backspace + CloseAllPanels 또는 Fungus Call Method로 처리.
    /// </summary>
    static readonly (string PanelName, string Reason)[] PanelBackspaceExcluded =
    {
        ("burner", "firpan_Panel 자식 UI. 전용 닫기 버튼 없음 → fripan backspace + CloseAllPanels."),
        ("Parret", "월드 스프라이트 오버레이. 닫기 버튼 없음 → Fungus Call Method CloseParrotPanel."),
        ("Bottle", "Sink_Pannel 자식 UI. 전용 닫기 버튼 없음 → Sink backspace + CloseAllPanels."),
    };

    static readonly string[] DisconnectObjectClickedBlockNames = { "parret" };

    static readonly HashSet<string> FungusClickTriggerBlockNames = new(StringComparer.Ordinal)
    {
        "Door_Clicked",
        "Door_toHall_Clicked",
        "TrashBox_Clicked",
        "refrigeratorClicked",
        "Sink",
    };

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-A World Click Entry")]
    public static void MigrateKitchenWorldClickEntry()
    {
        try
        {
            if (MigrateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KitchenSceneMigrator] Kitchen Phase R6-A world click migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KitchenSceneMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyKitchenWorldClickMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-A Verify World Click Entry")]
    public static void VerifyKitchenWorldClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        int violations = 0;

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] Verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] Verification failed: KitchenInteractionController missing on Flowchart.");
        }
        else
        {
            violations += VerifyRoutes(controller);
            violations += VerifyWorldClickBindings(controller);
        }

        foreach (string blockName in DisconnectObjectClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[KitchenSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        violations += CountEnabledFungusClickTriggers(scene, FungusClickTriggerBlockNames);

        if (violations == 0)
            Debug.Log("[KitchenSceneMigrator] Verification passed: world clicks routed through KitchenInteractionController.");
        else
            Debug.LogError($"[KitchenSceneMigrator] Verification failed: {violations} issue(s).");
    }

    static bool MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<KitchenInteractionController>();

        var so = new SerializedObject(controller);
        so.FindProperty("flowchart").objectReferenceValue = flowchart;
        so.FindProperty("backNavigator").objectReferenceValue = null;
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
        DisableFungusClickTriggers(scene, FungusClickTriggerBlockNames);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void DisableFungusClickTriggers(UnityEngine.SceneManagement.Scene scene, HashSet<string> blockNames)
    {
        foreach (FungusClickTrigger trigger in RoomInteractionSceneMigrationEditor.FindSceneComponents<FungusClickTrigger>(scene))
        {
            if (trigger == null || string.IsNullOrWhiteSpace(trigger.blockToExecute))
                continue;
            if (!blockNames.Contains(trigger.blockToExecute))
                continue;

            trigger.enabled = false;
            EditorUtility.SetDirty(trigger);
        }
    }

    static int CountEnabledFungusClickTriggers(UnityEngine.SceneManagement.Scene scene, HashSet<string> blockNames)
    {
        int count = 0;
        foreach (FungusClickTrigger trigger in RoomInteractionSceneMigrationEditor.FindSceneComponents<FungusClickTrigger>(scene))
        {
            if (trigger == null || !trigger.enabled)
                continue;
            if (!blockNames.Contains(trigger.blockToExecute))
                continue;

            count++;
            Debug.LogError(
                $"[KitchenSceneMigrator] FungusClickTrigger on '{trigger.gameObject.name}' still enabled for '{trigger.blockToExecute}'.",
                trigger);
        }

        return count;
    }

    static int VerifyRoutes(KitchenInteractionController controller)
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
                    $"[KitchenSceneMigrator] Missing or wrong route '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyWorldClickBindings(KitchenInteractionController controller)
    {
        int violations = 0;
        var so = new SerializedObject(controller);
        SerializedProperty worldClicks = so.FindProperty("worldClicks");
        var found = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < worldClicks.arraySize; i++)
        {
            SerializedProperty element = worldClicks.GetArrayElementAtIndex(i);
            string interactionId = element.FindPropertyRelative("interactionId").stringValue;
            if (!string.IsNullOrWhiteSpace(interactionId))
                found.Add(interactionId);

            Collider2D collider = element.FindPropertyRelative("collider").objectReferenceValue as Collider2D;
            if (collider == null)
            {
                violations++;
                Debug.LogError($"[KitchenSceneMigrator] worldClicks '{interactionId}' has no collider.");
            }
        }

        foreach (RoomInteractionSceneMigrationEditor.WorldClickTarget expected in WorldClickTargets)
        {
            if (!found.Contains(expected.InteractionId))
            {
                violations++;
                Debug.LogError($"[KitchenSceneMigrator] Missing worldClicks binding for '{expected.InteractionId}'.");
            }
        }

        return violations;
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-B UI Click Entry")]
    public static void MigrateKitchenUiClickEntry()
    {
        try
        {
            if (MigrateUiClickScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KitchenSceneMigrator] Kitchen Phase R6-B UI click migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KitchenSceneMigrator] R6-B migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyKitchenUiClickMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-B Verify UI Click Entry")]
    public static void VerifyKitchenUiClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            UiExecuteBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-B verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-B verification failed: KitchenInteractionController missing.");
        }
        else
        {
            violations += VerifyUiRoutes(controller);
            violations += VerifyUiButtonsWired(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[KitchenSceneMigrator] R6-B verification passed: UI clicks routed through KitchenInteractionController.");
        else
            Debug.LogError($"[KitchenSceneMigrator] R6-B verification failed: {violations} issue(s).");
    }

    static bool MigrateUiClickScene()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-B: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-B: KitchenInteractionController missing. Run R6-A first.");
            return false;
        }

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            MergeRoutes(ClickRoutes, UiClickRoutes, DropRoutes));
        so.ApplyModifiedPropertiesWithoutUndo();

        RewireUiExecuteBlockButtons(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] MergeRoutes(
        params RoomInteractionSceneMigrationEditor.InteractionRouteSpec[][] routeSets)
    {
        var merged = new Dictionary<string, RoomInteractionSceneMigrationEditor.InteractionRouteSpec>(StringComparer.Ordinal);
        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] routeSet in routeSets)
        {
            if (routeSet == null)
                continue;

            foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec route in routeSet)
                merged[route.InteractionId] = route;
        }

        return merged.Values.ToArray();
    }

    static void RewireUiExecuteBlockButtons(UnityEngine.SceneManagement.Scene scene, KitchenInteractionController controller)
    {
        var blockToInteractionId = UiClickRoutes
            .GroupBy(route => route.BlockName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().InteractionId, StringComparer.Ordinal);

        foreach (UnityEngine.UI.Button button in RoomInteractionSceneMigrationEditor.FindSceneComponents<UnityEngine.UI.Button>(scene))
        {
            var buttonSo = new SerializedObject(button);
            SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null)
                continue;

            string interactionId = null;
            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                    continue;

                string blockArg = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                if (!blockToInteractionId.TryGetValue(blockArg, out string mappedId))
                    continue;

                interactionId = mappedId;
                break;
            }

            if (string.IsNullOrWhiteSpace(interactionId))
                continue;

            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                buttonSo,
                "m_OnClick",
                controller,
                RoomInteractionSceneMigrationEditor.KitchenControllerTypeName,
                nameof(RoomInteractionController.OnInteraction),
                interactionId);
            buttonSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(button);
        }
    }

    static int VerifyUiRoutes(KitchenInteractionController controller)
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

        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec expected in UiClickRoutes)
        {
            if (!found.TryGetValue(expected.InteractionId, out string actualBlock)
                || actualBlock != expected.BlockName)
            {
                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-B missing or wrong route '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyUiButtonsWired(UnityEngine.SceneManagement.Scene scene, KitchenInteractionController controller)
    {
        int violations = 0;
        var blockToInteractionId = UiClickRoutes
            .GroupBy(route => route.BlockName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().InteractionId, StringComparer.Ordinal);

        foreach (UnityEngine.UI.Button button in RoomInteractionSceneMigrationEditor.FindSceneComponents<UnityEngine.UI.Button>(scene))
        {
            var buttonSo = new SerializedObject(button);
            SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null)
                continue;

            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                    continue;

                string blockArg = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                if (!blockToInteractionId.ContainsKey(blockArg))
                    continue;

                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-B: Button on '{button.gameObject.name}' still calls ExecuteBlock('{blockArg}').",
                    button);
            }
        }

        return violations;
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-C Drop Entry")]
    public static void MigrateKitchenDropEntry()
    {
        try
        {
            if (MigrateDropScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KitchenSceneMigrator] Kitchen Phase R6-C drop migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KitchenSceneMigrator] R6-C migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyKitchenDropMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-C Verify Drop Entry")]
    public static void VerifyKitchenDropMigration()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            DropExecuteBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-C verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-C verification failed: KitchenInteractionController missing.");
        }
        else
        {
            violations += VerifyDropRoutes(controller);
            violations += VerifyDropZonesWired(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[KitchenSceneMigrator] R6-C verification passed: drop zones routed through KitchenInteractionController.");
        else
            Debug.LogError($"[KitchenSceneMigrator] R6-C verification failed: {violations} issue(s).");
    }

    static bool MigrateDropScene()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-C: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-C: KitchenInteractionController missing. Run R6-A first.");
            return false;
        }

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            MergeRoutes(ClickRoutes, UiClickRoutes, DropRoutes));
        so.ApplyModifiedPropertiesWithoutUndo();

        RewireDropZones(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void RewireDropZones(UnityEngine.SceneManagement.Scene scene, KitchenInteractionController controller)
    {
        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            bool rewired = false;

            foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec dropRoute in DropRoutes)
            {
                if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                        dropZoneSo,
                        "onUnlock",
                        dropRoute.BlockName))
                    continue;

                RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                    dropZoneSo,
                    "onUnlock",
                    controller,
                    RoomInteractionSceneMigrationEditor.KitchenControllerTypeName,
                    nameof(RoomInteractionController.OnInteraction),
                    dropRoute.InteractionId);
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

    static int VerifyDropRoutes(KitchenInteractionController controller)
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

        foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec expected in DropRoutes)
        {
            if (!found.TryGetValue(expected.InteractionId, out string actualBlock)
                || actualBlock != expected.BlockName)
            {
                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-C missing or wrong route '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyDropZonesWired(UnityEngine.SceneManagement.Scene scene, KitchenInteractionController controller)
    {
        int violations = 0;

        foreach (WorldItemDropZone dropZone in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<WorldItemDropZone>(scene))
        {
            var dropZoneSo = new SerializedObject(dropZone);
            foreach (RoomInteractionSceneMigrationEditor.InteractionRouteSpec dropRoute in DropRoutes)
            {
                if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                        dropZoneSo,
                        "onUnlock",
                        dropRoute.BlockName))
                    continue;

                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-C: WorldItemDropZone '{dropZone.gameObject.name}' still calls ExecuteBlock('{dropRoute.BlockName}').",
                    dropZone);
            }
        }

        return violations;
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-D Panel Open/Close")]
    public static void MigrateKitchenPanelOpenClose()
    {
        try
        {
            if (MigratePanelScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KitchenSceneMigrator] Kitchen Phase R6-D panel migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KitchenSceneMigrator] R6-D migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyKitchenPanelMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-D Verify Panel Open/Close")]
    public static void VerifyKitchenPanelMigration()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        int violations = 0;

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-D verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-D verification failed: KitchenInteractionController missing.");
        }
        else
        {
            violations += VerifyPanelRegistryWired(flowchart, controller);
            violations += VerifyPanelBackspaceClosers(scene, controller);
            VerifyPanelBackspaceExclusions(scene);
        }

        violations += CountEnabledPanelSetActiveCommands(flowchart);

        if (violations == 0)
            Debug.Log("[KitchenSceneMigrator] R6-D verification passed: panels routed through KitchenPanelRegistry.");
        else
            Debug.LogError($"[KitchenSceneMigrator] R6-D verification failed: {violations} issue(s).");
    }

    static bool MigratePanelScene()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-D: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-D: KitchenInteractionController missing. Run R6-A first.");
            return false;
        }

        KitchenPanelRegistry registry = flowchart.GetComponent<KitchenPanelRegistry>();
        if (registry == null)
            registry = flowchart.gameObject.AddComponent<KitchenPanelRegistry>();

        WirePanelRegistryReferences(scene, registry);

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("panelRegistry").objectReferenceValue = registry;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        RedirectPanelSetActiveToRegistry(flowchart, registry);
        WirePanelBackspaceClosers(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void WirePanelRegistryReferences(UnityEngine.SceneManagement.Scene scene, KitchenPanelRegistry registry)
    {
        var so = new SerializedObject(registry);
        so.FindProperty("burnerPanel").objectReferenceValue =
            RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "burner");
        so.FindProperty("fripanPanel").objectReferenceValue =
            RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "firpan_Panel");
        so.FindProperty("parrotPanel").objectReferenceValue =
            RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "Parret");
        so.FindProperty("sinkPanel").objectReferenceValue =
            RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "Sink_Pannel");
        so.FindProperty("bottlePanel").objectReferenceValue =
            RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "Bottle");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void RedirectPanelSetActiveToRegistry(Flowchart flowchart, KitchenPanelRegistry registry)
    {
        var methodByObjectName = PanelMethodMap.ToDictionary(
            entry => entry.ObjectName,
            entry => entry,
            StringComparer.Ordinal);

        foreach (Block block in flowchart.GetComponents<Block>())
        {
            if (block == null || !PanelSetActiveBlockNames.Contains(block.BlockName))
                continue;

            for (int i = 0; i < block.CommandList.Count; i++)
            {
                Command command = block.CommandList[i];
                if (command == null || command.GetType().Name != "SetActive" || !command.enabled)
                    continue;

                if (!TryGetSetActiveTarget(command, out GameObject target, out bool active))
                    continue;

                if (target == null || !methodByObjectName.TryGetValue(target.name, out var methods))
                    continue;

                string methodName = active ? methods.OpenMethod : methods.CloseMethod;
                ReplaceCommandWithCallMethod(flowchart, block, i, command, registry.gameObject, methodName);
            }
        }
    }

    static void ReplaceCommandWithCallMethod(
        Flowchart flowchart,
        Block block,
        int index,
        Command setActiveCommand,
        GameObject registryObject,
        string methodName)
    {
        var callMethod = flowchart.gameObject.AddComponent<CallMethod>();
        callMethod.ParentBlock = block;

        var callSo = new SerializedObject(callMethod);
        callSo.FindProperty("itemId").intValue = setActiveCommand.ItemId;
        callSo.FindProperty("indentLevel").intValue = GetCommandIndent(setActiveCommand);
        callSo.FindProperty("targetObject").objectReferenceValue = registryObject;
        callSo.FindProperty("methodName").stringValue = methodName;
        callSo.FindProperty("delay").floatValue = 0f;
        callSo.ApplyModifiedPropertiesWithoutUndo();

        setActiveCommand.enabled = false;
        block.CommandList[index] = callMethod;

        EditorUtility.SetDirty(setActiveCommand);
        EditorUtility.SetDirty(callMethod);
        EditorUtility.SetDirty(block);
    }

    static int GetCommandIndent(Command command)
    {
        var so = new SerializedObject(command);
        return so.FindProperty("indentLevel").intValue;
    }

    static bool TryGetSetActiveTarget(Command command, out GameObject target, out bool active)
    {
        target = null;
        active = false;

        if (command == null)
            return false;

        var so = new SerializedObject(command);
        target = so.FindProperty("_targetGameObject.gameObjectVal").objectReferenceValue as GameObject;
        active = so.FindProperty("activeState.booleanVal").boolValue;
        return target != null;
    }

    static void WirePanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        KitchenInteractionController controller)
    {
        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
            {
                Debug.LogWarning(
                    $"[KitchenSceneMigrator] R6-D: panel '{target.PanelName}' not found for backspace wiring.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                Debug.LogWarning(
                    $"[KitchenSceneMigrator] R6-D: PanelBackspaceCloser missing on '{target.PanelName}'.");
                continue;
            }

            RoomInteractionSceneMigrationEditor.WirePanelBackspaceCloser(
                closer,
                controller,
                target.PanelCloseId);
        }
    }

    static int VerifyPanelRegistryWired(Flowchart flowchart, KitchenInteractionController controller)
    {
        int violations = 0;
        var controllerSo = new SerializedObject(controller);
        if (controllerSo.FindProperty("panelRegistry").objectReferenceValue == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-D: KitchenInteractionController.panelRegistry is not assigned.");
        }

        KitchenPanelRegistry registry = flowchart.GetComponent<KitchenPanelRegistry>();
        if (registry == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-D: KitchenPanelRegistry component missing on Flowchart.");
            return violations;
        }

        var registrySo = new SerializedObject(registry);
        foreach (string fieldName in new[] { "burnerPanel", "fripanPanel", "parrotPanel", "sinkPanel", "bottlePanel" })
        {
            if (registrySo.FindProperty(fieldName).objectReferenceValue == null)
            {
                violations++;
                Debug.LogError($"[KitchenSceneMigrator] R6-D: KitchenPanelRegistry.{fieldName} is not assigned.");
            }
        }

        return violations;
    }

    static void VerifyPanelBackspaceExclusions(UnityEngine.SceneManagement.Scene scene)
    {
        foreach ((string panelName, string reason) in PanelBackspaceExcluded)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, panelName);
            if (panel == null)
            {
                Debug.LogWarning(
                    $"[KitchenSceneMigrator] R6-D: excluded panel '{panelName}' not found in scene.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer != null)
            {
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-D: '{panelName}' is marked backspace-excluded but has PanelBackspaceCloser.",
                    closer);
                continue;
            }

            Debug.Log(
                $"[KitchenSceneMigrator] R6-D: '{panelName}' has no backspace button (expected). {reason}");
        }
    }

    static int VerifyPanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        KitchenInteractionController controller)
    {
        int violations = 0;

        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
                continue;

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-D: PanelBackspaceCloser missing on '{target.PanelName}'.",
                    panel);
                continue;
            }

            var closerSo = new SerializedObject(closer);
            if (closerSo.FindProperty("interactionController").objectReferenceValue != controller)
            {
                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-D: '{target.PanelName}' backspace is not wired to KitchenInteractionController.",
                    closer);
            }

            string closeId = closerSo.FindProperty("panelCloseInteractionId").stringValue;
            if (closeId != target.PanelCloseId)
            {
                violations++;
                Debug.LogError(
                    $"[KitchenSceneMigrator] R6-D: '{target.PanelName}' backspace id is '{closeId}', expected '{target.PanelCloseId}'.",
                    closer);
            }
        }

        return violations;
    }

    static int CountEnabledPanelSetActiveCommands(Flowchart flowchart)
    {
        var panelNames = new HashSet<string>(
            PanelMethodMap.Select(entry => entry.ObjectName),
            StringComparer.Ordinal);

        int violations = 0;
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (command == null || !command.enabled || command.GetType().Name != "SetActive")
                continue;
            if (command.ParentBlock == null || !PanelSetActiveBlockNames.Contains(command.ParentBlock.BlockName))
                continue;

            if (!TryGetSetActiveTarget(command, out GameObject target, out _)
                || target == null
                || !panelNames.Contains(target.name))
                continue;

            violations++;
            Debug.LogError(
                $"[KitchenSceneMigrator] R6-D: enabled SetActive on panel '{target.name}' remains in block '{command.ParentBlock.BlockName}'.",
                command);
        }

        return violations;
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-E Sink Puzzle State")]
    public static void MigrateKitchenSinkPuzzleState()
    {
        try
        {
            if (MigrateSinkPuzzleStateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[KitchenSceneMigrator] Kitchen Phase R6-E sink puzzle state migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KitchenSceneMigrator] R6-E migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyKitchenSinkPuzzleStateMigration();
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen R6-E Verify Sink Puzzle State")]
    public static void VerifyKitchenSinkPuzzleStateMigration()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        int violations = VerifySinkPuzzleState(scene);

        if (violations == 0)
            Debug.Log("[KitchenSceneMigrator] R6-E verification passed: KitchenPuzzleState wired on Flowchart.");
        else
            Debug.LogError($"[KitchenSceneMigrator] R6-E verification failed: {violations} issue(s).");
    }

    static bool MigrateSinkPuzzleStateScene()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-E: Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-E: KitchenInteractionController missing. Run R6-A first.");
            return false;
        }

        KitchenPuzzleState puzzleState = flowchart.GetComponent<KitchenPuzzleState>();
        if (puzzleState == null)
            puzzleState = flowchart.gameObject.AddComponent<KitchenPuzzleState>();

        var puzzleSo = new SerializedObject(puzzleState);
        puzzleSo.FindProperty("flowchart").objectReferenceValue = flowchart;
        puzzleSo.ApplyModifiedPropertiesWithoutUndo();

        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("puzzleState").objectReferenceValue = puzzleState;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(puzzleState);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static int VerifySinkPuzzleState(UnityEngine.SceneManagement.Scene scene)
    {
        int violations = 0;
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[KitchenSceneMigrator] R6-E: Flowchart not found.");
            return 1;
        }

        var controller = flowchart.GetComponent<KitchenInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-E: KitchenInteractionController missing.");
            return violations;
        }

        KitchenPuzzleState puzzleState = flowchart.GetComponent<KitchenPuzzleState>();
        if (puzzleState == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-E: KitchenPuzzleState missing on Flowchart.");
        }

        var controllerSo = new SerializedObject(controller);
        if (controllerSo.FindProperty("puzzleState").objectReferenceValue == null)
        {
            violations++;
            Debug.LogError("[KitchenSceneMigrator] R6-E: KitchenInteractionController.puzzleState is not wired.");
        }

        return violations;
    }
}

/// <summary>
/// Kitchen R6-A/B/C EditMode static test와 마이그레이터 검증이 공유하는 기대값.
/// </summary>
public static class KitchenSceneMigrationSpecs
{
    public const string InteractionControllerScriptGuid = "a7c3e8914b2d4f6e9a1c5d8e3f7b2a04";
    public const string PuzzleStateScriptGuid = "7f2e8b9c1d4a5e6f807192a3b4c5d6e7";
    public const string FungusClickTriggerScriptGuid = "0235fc39fc1c6894386995e3c0a9a673";
    public const string WorldItemDropZoneScriptGuid = "f12114e92cf93ff4e869374048581b4f";

    public static readonly string[] MigratedFungusBlockNames =
    {
        "Door_Clicked",
        "Door_toHall_Clicked",
        "TrashBox_Clicked",
        "refrigeratorClicked",
        "Sink",
        "burner",
        "fripan",
        "parret",
        "Faucet",
        "FilledBottle",
        "Bottle_Clicked",
        "Bottle_Dragged",
        "Food_Dragged",
    };

    public static readonly (string InteractionId, string BlockName)[] ClickRoutes =
    {
        ("door", "Door_Clicked"),
        ("door_to_hall", "Door_toHall_Clicked"),
        ("trashbox", "TrashBox_Clicked"),
        ("refrigerator", "refrigeratorClicked"),
        ("sink", "Sink"),
        ("bottle", "Bottle_Clicked"),
        ("burner", "burner"),
        ("fripan", "fripan"),
        ("parret", "parret"),
    };

    public static readonly (string InteractionId, string BlockName)[] UiClickRoutes =
    {
        ("burner", "burner"),
        ("faucet", "Faucet"),
        ("filled_bottle", "FilledBottle"),
        ("bottle", "Bottle_Clicked"),
    };

    public static readonly (string InteractionId, string BlockName)[] DropRoutes =
    {
        ("bottle_drag", "Bottle_Dragged"),
        ("food_drag", "Food_Dragged"),
    };

    public static readonly string[] WorldClickInteractionIds =
    {
        "door",
        "door_to_hall",
        "trashbox",
        "refrigerator",
        "sink",
        "burner",
        "fripan",
        "parret",
    };

    public static readonly (string DropZoneObjectName, string InteractionId)[] DropZoneUnlockRoutes =
    {
        ("SinkDropzone", "bottle_drag"),
        ("BurnerDropzone", "food_drag"),
    };

    public static readonly string[] FungusClickTriggerBlockNames =
    {
        "Door_Clicked",
        "Door_toHall_Clicked",
        "TrashBox_Clicked",
        "refrigeratorClicked",
        "Sink",
    };

    public static (string InteractionId, string BlockName)[] AllInteractionRoutes()
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string interactionId, string blockName) in ClickRoutes)
            merged[interactionId] = blockName;
        foreach ((string interactionId, string blockName) in UiClickRoutes)
            merged[interactionId] = blockName;
        foreach ((string interactionId, string blockName) in DropRoutes)
            merged[interactionId] = blockName;

        return merged.Select(pair => (pair.Key, pair.Value)).ToArray();
    }
}
