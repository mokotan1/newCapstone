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
        RoomInteractionSceneMigrationEditor.WriteRoutes(so.FindProperty("routes"), ClickRoutes);
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
