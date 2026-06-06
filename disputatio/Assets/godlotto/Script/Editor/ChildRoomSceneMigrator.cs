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
}
