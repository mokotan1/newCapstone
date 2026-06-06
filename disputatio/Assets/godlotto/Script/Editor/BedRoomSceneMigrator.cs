using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BedRoomSceneMigrator
{
    const string BedRoomScenePath = "Assets/Scenes/Mokotan/Second Floor/BedRoom.unity";

    static readonly RoomInteractionSceneMigrationEditor.WorldClickTarget[] WorldClickTargets =
    {
        new() { InteractionId = "bookcase", GameObjectName = "Bookcase" },
        new() { InteractionId = "safe", GameObjectName = "Safe" },
        new() { InteractionId = "parrot", GameObjectName = "Parret" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ClickRoutes =
    {
        new() { InteractionId = "bookcase", BlockName = "Bookcase_Clicked" },
        new() { InteractionId = "safe", BlockName = "Safe_Clicked" },
        new() { InteractionId = "book", BlockName = "Book_Clicked" },
        new() { InteractionId = "parrot", BlockName = "Parrot_Clicked" },
        new() { InteractionId = "button", BlockName = "Button_Clicked" },
    };

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec UnlockRoute =
        new() { InteractionId = "unlock", BlockName = "onUnlock" };

    static readonly HashSet<string> UnlockBlockNamesForVerification = new(
        new[] { UnlockRoute.BlockName },
        StringComparer.Ordinal);

    static readonly string[] DisconnectObjectClickedBlockNames =
    {
        "Bookcase_Clicked",
        "Safe_Clicked",
        "Book_Clicked",
        "Parrot_Clicked",
    };

    static readonly string[] DisconnectButtonClickedBlockNames =
    {
        "Button_Clicked",
    };

    const string ButtonClickTargetObjectName = "SetButton";

    static readonly string[] RemoveButtonForwarderFromObjectNames =
    {
        "BackspaceNameplate",
    };

    static readonly HashSet<string> ClickBlockNamesForVerification = new(
        ClickRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    const string BookPanelBackspaceId = "bookpanel_backspace";
    const string PanelBackspaceId = "panel_backspace";

    static readonly PanelCloseTarget[] PanelCloseTargets =
    {
        new() { PanelCloseId = BookPanelBackspaceId, PanelObjectName = "BookPanel" },
        new() { PanelCloseId = PanelBackspaceId, PanelObjectName = "SafePanel" },
        new() { PanelCloseId = PanelBackspaceId, PanelObjectName = "BookcasePanel" },
    };

    static readonly PanelBackspaceTarget[] PanelBackspaceTargets =
    {
        new() { PanelName = "BookPanel", PanelCloseId = BookPanelBackspaceId },
        new() { PanelName = "SafePanel", PanelCloseId = PanelBackspaceId },
        new() { PanelName = "BookcasePanel", PanelCloseId = PanelBackspaceId },
    };

    static readonly HashSet<string> PanelBackspaceBlockNamesForVerification = new(
        new[] { "BookPanel_Backspace", "Panel_Backspace" },
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

    [MenuItem("Tools/godlotto/Migrate BedRoom Phase R1 Click Entry")]
    public static void MigrateBedRoomClickEntry()
    {
        try
        {
            if (MigrateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BedRoomSceneMigrator] BedRoom Phase R1 migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BedRoomSceneMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyBedRoomClickMigration();
    }

    [MenuItem("Tools/godlotto/Migrate BedRoom Phase R1-B Unlock Entry")]
    public static void MigrateBedRoomUnlockEntry()
    {
        try
        {
            if (MigrateUnlockScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BedRoomSceneMigrator] BedRoom Phase R1-B unlock migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BedRoomSceneMigrator] R1-B migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyBedRoomUnlockMigration();
    }

    [MenuItem("Tools/godlotto/Migrate BedRoom Phase R1-C Panel Backspace")]
    public static void MigrateBedRoomPanelBackspace()
    {
        try
        {
            if (MigratePanelBackspaceScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BedRoomSceneMigrator] BedRoom Phase R1-C panel backspace migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BedRoomSceneMigrator] R1-C migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyBedRoomPanelBackspaceMigration();
    }

    [MenuItem("Tools/godlotto/Migrate BedRoom Phase R1-D Back Return")]
    public static void MigrateBedRoomBackReturn()
    {
        try
        {
            if (MigrateBackReturnScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[BedRoomSceneMigrator] BedRoom Phase R1-D back return migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BedRoomSceneMigrator] R1-D migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyBedRoomBackReturnMigration();
    }

    [MenuItem("Tools/godlotto/Verify BedRoom Back Return Migration")]
    public static void VerifyBedRoomBackReturnMigration()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            BackBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] R1-D verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-D verification failed: BedRoomInteractionController missing.");
        }
        else
        {
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

        if (violations == 0)
            Debug.Log("[BedRoomSceneMigrator] R1-D verification passed: back return routed through BedRoomInteractionController.");
        else
            Debug.LogError($"[BedRoomSceneMigrator] R1-D verification failed: {violations} issue(s).");
    }

    [MenuItem("Tools/godlotto/Verify BedRoom Unlock Migration")]
    public static void VerifyBedRoomUnlockMigration()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            UnlockBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] R1-B verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-B verification failed: BedRoomInteractionController missing.");
        }
        else
        {
            violations += VerifyUnlockRoute(controller);
            violations += VerifyUnlockEventSources(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[BedRoomSceneMigrator] R1-B verification passed: unlock routed through BedRoomInteractionController.");
        else
            Debug.LogError($"[BedRoomSceneMigrator] R1-B verification failed: {violations} issue(s).");
    }

    [MenuItem("Tools/godlotto/Verify BedRoom Click Migration")]
    public static void VerifyBedRoomClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            ClickBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] Verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] Verification failed: BedRoomInteractionController missing on Flowchart.");
        }

        foreach (string blockName in DisconnectObjectClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[BedRoomSceneMigrator] Block '{blockName}' still has an event handler.");
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
                Debug.LogError($"[BedRoomSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        violations += VerifyButtonClickForwarder(scene, controller);

        if (violations == 0)
            Debug.Log("[BedRoomSceneMigrator] Verification passed: click entry points routed through BedRoomInteractionController.");
        else
            Debug.LogError($"[BedRoomSceneMigrator] Verification failed: {violations} issue(s).");
    }

    [MenuItem("Tools/godlotto/Verify BedRoom Panel Backspace Migration")]
    public static void VerifyBedRoomPanelBackspaceMigration()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            PanelBackspaceBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] R1-C verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-C verification failed: BedRoomInteractionController missing.");
        }
        else
        {
            violations += VerifyPanelCloseBindings(controller, scene);
            violations += VerifyPanelBackspaceClosers(scene, controller);
        }

        if (violations == 0)
            Debug.Log("[BedRoomSceneMigrator] R1-C verification passed: panel backspace routed through BedRoomInteractionController.");
        else
            Debug.LogError($"[BedRoomSceneMigrator] R1-C verification failed: {violations} issue(s).");
    }

    static bool MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<BedRoomInteractionController>();

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
            MergeRoutes(ClickRoutes, UnlockRoute));
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

        RemoveMisplacedButtonForwarders(scene);
        WireButtonClickEntry(scene, controller, buttonTarget);

        Clickable2D bookClickable = RoomInteractionSceneMigrationEditor.TryResolveClickableForBlock(
            flowchart,
            "Book_Clicked");
        if (bookClickable != null && bookClickable.gameObject != null)
        {
            RoomInteractionSceneMigrationEditor.EnsureUiClickForwarder(
                bookClickable.gameObject,
                controller,
                "book");
        }
        else
        {
            Debug.LogWarning("[BedRoomSceneMigrator] Book_Clicked clickable not found; UI book click may still use Fungus.");
        }

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static bool MigrateUnlockScene()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<BedRoomInteractionController>();

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

    static bool MigratePanelBackspaceScene()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<BedRoomInteractionController>();

        var panelCloseSpecs = new List<RoomInteractionSceneMigrationEditor.PanelCloseSpec>();
        foreach (PanelCloseTarget target in PanelCloseTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelObjectName);
            if (panel == null)
            {
                Debug.LogWarning(
                    $"[BedRoomSceneMigrator] R1-C: panel '{target.PanelObjectName}' not found for '{target.PanelCloseId}'.");
                continue;
            }

            panelCloseSpecs.Add(new RoomInteractionSceneMigrationEditor.PanelCloseSpec
            {
                PanelCloseId = target.PanelCloseId,
                Panel = panel,
            });
        }

        GameObject safePanel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, "SafePanel");
        GameObject safeItemEffect = FindSafeItemEffectInScene(scene);

        var so = new SerializedObject(controller);
        RoomInteractionSceneMigrationEditor.WritePanelCloses(
            so.FindProperty("panelCloses"),
            panelCloseSpecs.ToArray());
        so.FindProperty("safePanel").objectReferenceValue = safePanel;
        so.FindProperty("safeItemEffect").objectReferenceValue = safeItemEffect;
        so.ApplyModifiedPropertiesWithoutUndo();

        WirePanelBackspaceClosers(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static bool MigrateBackReturnScene()
    {
        var scene = EditorSceneManager.OpenScene(BedRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<BedRoomInteractionController>();
        if (controller == null)
        {
            Debug.LogError("[BedRoomSceneMigrator] R1-D: BedRoomInteractionController missing. Run R1 click migration first.");
            return false;
        }

        BackNavigator backNavigator = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<BackNavigator>(scene)
            .FirstOrDefault();

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
        so.ApplyModifiedPropertiesWithoutUndo();

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
            RoomInteractionSceneMigrationEditor.BedRoomControllerTypeName);

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

    static int VerifyBackRoute(BedRoomInteractionController controller)
    {
        var so = new SerializedObject(controller);
        SerializedProperty routes = so.FindProperty("routes");
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            if (route.FindPropertyRelative("interactionId").stringValue != BackRoute.InteractionId)
                continue;

            if (route.FindPropertyRelative("fungusBlockName").stringValue != BackRoute.BlockName)
            {
                Debug.LogError("[BedRoomSceneMigrator] R1-D: back route maps to unexpected block.");
                return 1;
            }

            return 0;
        }

        Debug.LogError("[BedRoomSceneMigrator] R1-D: missing back route on BedRoomInteractionController.");
        return 1;
    }

    static int VerifyBackOutcomes(BedRoomInteractionController controller)
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
                Debug.LogError("[BedRoomSceneMigrator] R1-D: Select_Yes outcome must set goBack.");
            }

            if (blockName == "Select_No" && !outcome.FindPropertyRelative("resetIsClicked").boolValue)
            {
                violations++;
                Debug.LogError("[BedRoomSceneMigrator] R1-D: Select_No outcome must set resetIsClicked.");
            }
        }

        foreach (RoomInteractionSceneMigrationEditor.BlockOutcomeSpec expected in BackOutcomes)
        {
            if (!found.Contains(expected.BlockName))
            {
                violations++;
                Debug.LogError($"[BedRoomSceneMigrator] R1-D: missing block outcome for '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static GameObject FindSafeItemEffectInScene(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "SafeItemEffect")
                    return transform.gameObject;
            }
        }

        return null;
    }

    static void WirePanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        BedRoomInteractionController controller)
    {
        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
            {
                Debug.LogWarning($"[BedRoomSceneMigrator] R1-C: backspace target panel '{target.PanelName}' not found.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                Debug.LogWarning($"[BedRoomSceneMigrator] R1-C: PanelBackspaceCloser missing on '{target.PanelName}'.");
                continue;
            }

            RoomInteractionSceneMigrationEditor.WirePanelBackspaceCloser(
                closer,
                controller,
                target.PanelCloseId);
        }
    }

    static int VerifyPanelCloseBindings(BedRoomInteractionController controller, UnityEngine.SceneManagement.Scene scene)
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
                Debug.LogError($"[BedRoomSceneMigrator] R1-C: panelCloses[{i}] has no panel reference.");
            }
        }

        if (!foundIds.Contains(BookPanelBackspaceId))
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-C: missing bookpanel_backspace panel close binding.");
        }

        if (!foundIds.Contains(PanelBackspaceId))
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-C: missing panel_backspace panel close binding.");
        }

        if (so.FindProperty("safePanel").objectReferenceValue == null)
        {
            violations++;
            Debug.LogError("[BedRoomSceneMigrator] R1-C: safePanel reference missing on BedRoomInteractionController.");
        }

        return violations;
    }

    static int VerifyPanelBackspaceClosers(
        UnityEngine.SceneManagement.Scene scene,
        BedRoomInteractionController controller)
    {
        int violations = 0;

        foreach (PanelBackspaceTarget target in PanelBackspaceTargets)
        {
            GameObject panel = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.PanelName);
            if (panel == null)
            {
                violations++;
                Debug.LogError($"[BedRoomSceneMigrator] R1-C: panel '{target.PanelName}' not found.");
                continue;
            }

            PanelBackspaceCloser closer = panel.GetComponentsInChildren<PanelBackspaceCloser>(true)
                .FirstOrDefault();
            if (closer == null)
            {
                violations++;
                Debug.LogError($"[BedRoomSceneMigrator] R1-C: PanelBackspaceCloser missing on '{target.PanelName}'.");
                continue;
            }

            var closerSo = new SerializedObject(closer);
            if (closerSo.FindProperty("interactionController").objectReferenceValue != controller)
            {
                violations++;
                Debug.LogError($"[BedRoomSceneMigrator] R1-C: '{target.PanelName}' closer controller reference is wrong.");
            }

            if (closerSo.FindProperty("panelCloseInteractionId").stringValue != target.PanelCloseId)
            {
                violations++;
                Debug.LogError(
                    $"[BedRoomSceneMigrator] R1-C: '{target.PanelName}' closer panelCloseInteractionId expected '{target.PanelCloseId}'.");
            }

            if (!string.IsNullOrWhiteSpace(closerSo.FindProperty("executeBlockName").stringValue))
            {
                violations++;
                Debug.LogError(
                    $"[BedRoomSceneMigrator] R1-C: '{target.PanelName}' closer still has executeBlockName set.");
            }
        }

        return violations;
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
        BedRoomInteractionController controller)
    {
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
                RoomInteractionSceneMigrationEditor.BedRoomControllerTypeName,
                nameof(BedRoomInteractionController.OnInteraction),
                UnlockRoute.InteractionId);
            dropZoneSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dropZone);
        }

        foreach (UISafeLockController safeLock in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<UISafeLockController>(scene))
        {
            var safeLockSo = new SerializedObject(safeLock);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    safeLockSo,
                    "onUnlock",
                    UnlockRoute.BlockName))
                continue;

            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                safeLockSo,
                "onUnlock",
                controller,
                RoomInteractionSceneMigrationEditor.BedRoomControllerTypeName,
                nameof(BedRoomInteractionController.OnInteraction),
                UnlockRoute.InteractionId);
            safeLockSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(safeLock);
        }
    }

    static int VerifyUnlockRoute(BedRoomInteractionController controller)
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

            Debug.LogError("[BedRoomSceneMigrator] unlock route maps to wrong Fungus block.");
            return 1;
        }

        Debug.LogError("[BedRoomSceneMigrator] unlock route missing on BedRoomInteractionController.");
        return 1;
    }

    static int VerifyUnlockEventSources(
        UnityEngine.SceneManagement.Scene scene,
        BedRoomInteractionController controller)
    {
        int violations = 0;

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
                $"[BedRoomSceneMigrator] WorldItemDropZone '{dropZone.gameObject.name}' still calls ExecuteBlock('{UnlockRoute.BlockName}').",
                dropZone);
        }

        foreach (UISafeLockController safeLock in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<UISafeLockController>(scene))
        {
            var safeLockSo = new SerializedObject(safeLock);
            if (!RoomInteractionSceneMigrationEditor.UnityEventCallsExecuteBlock(
                    safeLockSo,
                    "onUnlock",
                    UnlockRoute.BlockName))
                continue;

            violations++;
            Debug.LogError(
                $"[BedRoomSceneMigrator] UISafeLockController '{safeLock.gameObject.name}' still calls ExecuteBlock('{UnlockRoute.BlockName}').",
                safeLock);
        }

        return violations;
    }

    static void RemoveMisplacedButtonForwarders(UnityEngine.SceneManagement.Scene scene)
    {
        var removeNames = new HashSet<string>(RemoveButtonForwarderFromObjectNames, StringComparer.Ordinal);
        foreach (RoomUiClickForwarder forwarder in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<RoomUiClickForwarder>(scene))
        {
            if (forwarder == null || forwarder.gameObject == null)
                continue;

            var so = new SerializedObject(forwarder);
            if (so.FindProperty("interactionId").stringValue != "button")
                continue;

            if (!removeNames.Contains(forwarder.gameObject.name))
                continue;

            UnityEngine.Object.DestroyImmediate(forwarder, true);
            EditorUtility.SetDirty(forwarder.gameObject);
        }
    }

    static void WireButtonClickEntry(
        UnityEngine.SceneManagement.Scene scene,
        BedRoomInteractionController controller,
        UnityEngine.UI.Button resolvedButton)
    {
        GameObject buttonClickGo = resolvedButton != null
            ? resolvedButton.gameObject
            : RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, ButtonClickTargetObjectName);

        if (buttonClickGo == null)
        {
            Debug.LogWarning("[BedRoomSceneMigrator] Button_Clicked target not found; button click may be unrouted.");
            return;
        }

        if (buttonClickGo.GetComponent<Collider2D>() != null &&
            buttonClickGo.GetComponent<UnityEngine.UI.Button>() == null)
        {
            Debug.LogWarning(
                "[BedRoomSceneMigrator] Button_Clicked target has Collider2D but no UI Button; add a worldClicks binding manually.");
            return;
        }

        RoomInteractionSceneMigrationEditor.EnsureUiClickForwarder(
            buttonClickGo,
            controller,
            "button");
    }

    static int VerifyButtonClickForwarder(
        UnityEngine.SceneManagement.Scene scene,
        BedRoomInteractionController controller)
    {
        var buttonForwarders = new List<(GameObject gameObject, RoomUiClickForwarder forwarder)>();
        foreach (RoomUiClickForwarder forwarder in RoomInteractionSceneMigrationEditor
                     .FindSceneComponents<RoomUiClickForwarder>(scene))
        {
            if (forwarder == null || forwarder.gameObject == null)
                continue;

            var so = new SerializedObject(forwarder);
            if (so.FindProperty("interactionId").stringValue != "button")
                continue;

            buttonForwarders.Add((forwarder.gameObject, forwarder));
        }

        int violations = 0;
        foreach (string forbiddenName in RemoveButtonForwarderFromObjectNames)
        {
            int onForbidden = buttonForwarders.Count(entry => entry.gameObject.name == forbiddenName);
            if (onForbidden == 0)
                continue;

            violations += onForbidden;
            Debug.LogError(
                $"[BedRoomSceneMigrator] RoomUiClickForwarder(interactionId=button) must not be on '{forbiddenName}' ({onForbidden} found).");
        }

        var onSetButton = buttonForwarders
            .Where(entry => entry.gameObject.name == ButtonClickTargetObjectName)
            .ToList();
        if (onSetButton.Count != 1)
        {
            violations++;
            Debug.LogError(
                $"[BedRoomSceneMigrator] Expected exactly 1 button forwarder on '{ButtonClickTargetObjectName}', found {onSetButton.Count}.");
        }
        else
        {
            var so = new SerializedObject(onSetButton[0].forwarder);
            if (so.FindProperty("controller").objectReferenceValue != controller)
            {
                violations++;
                Debug.LogError("[BedRoomSceneMigrator] SetButton forwarder controller reference is wrong.");
            }
        }

        int unexpected = buttonForwarders.Count - onSetButton.Count -
                         buttonForwarders.Count(entry => RemoveButtonForwarderFromObjectNames.Contains(entry.gameObject.name));
        if (unexpected > 0)
        {
            violations += unexpected;
            foreach (var entry in buttonForwarders.Where(entry =>
                         entry.gameObject.name != ButtonClickTargetObjectName &&
                         !RemoveButtonForwarderFromObjectNames.Contains(entry.gameObject.name)))
            {
                Debug.LogError(
                    $"[BedRoomSceneMigrator] Unexpected button forwarder on '{entry.gameObject.name}'.");
            }
        }

        if (violations == 0)
        {
            Debug.Log(
                $"[BedRoomSceneMigrator] button forwarder OK: 1 on '{ButtonClickTargetObjectName}', 0 elsewhere.");
        }

        return violations;
    }
}
