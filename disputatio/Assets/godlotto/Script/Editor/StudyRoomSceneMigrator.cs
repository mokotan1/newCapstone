using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StudyRoomSceneMigrator
{
    const string StudyRoomScenePath = "Assets/Scenes/Mokotan/First Floor/1floorRight/StudyRoom.unity";

    static readonly RoomInteractionSceneMigrationEditor.InteractionRouteSpec[] ClickRoutes =
    {
        new() { InteractionId = "cardstack", BlockName = "CardStack_Clicked" },
        new() { InteractionId = "diary", BlockName = "Diary_Clicked" },
    };

    static readonly HashSet<string> ClickBlockNamesForVerification = new(
        ClickRoutes.Select(route => route.BlockName),
        StringComparer.Ordinal);

    static readonly string[] DisconnectButtonClickedBlockNames =
    {
        "Diary_Clicked",
    };

    sealed class UiButtonClickTarget
    {
        public string ObjectName;
        public string InteractionId;
        public string BlockName;
    }

    static readonly UiButtonClickTarget[] UiButtonClickTargets =
    {
        new() { ObjectName = "CardStack_Button", InteractionId = "cardstack", BlockName = "CardStack_Clicked" },
        new() { ObjectName = "Diary_Button", InteractionId = "diary", BlockName = "Diary_Clicked" },
    };

    [MenuItem("Tools/godlotto/Migrate StudyRoom Phase R4-A Click Entry")]
    public static void MigrateStudyRoomClickEntry()
    {
        try
        {
            if (MigrateScene())
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[StudyRoomSceneMigrator] StudyRoom Phase R4-A migration complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[StudyRoomSceneMigrator] Migration failed: {ex.Message}\n{ex.StackTrace}");
        }

        VerifyStudyRoomClickMigration();
    }

    [MenuItem("Tools/godlotto/Verify StudyRoom Click Migration")]
    public static void VerifyStudyRoomClickMigration()
    {
        var scene = EditorSceneManager.OpenScene(StudyRoomScenePath, OpenSceneMode.Single);
        int violations = RoomInteractionSceneMigrationEditor.CountDirectExecuteBlockCalls(
            scene,
            ClickBlockNamesForVerification);

        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[StudyRoomSceneMigrator] Verification failed: Flowchart not found.");
            return;
        }

        var controller = flowchart.GetComponent<StudyRoomPuzzleController>();
        if (controller == null)
        {
            violations++;
            Debug.LogError("[StudyRoomSceneMigrator] Verification failed: StudyRoomPuzzleController missing on Flowchart.");
        }
        else
        {
            violations += VerifyClickRoutes(controller);
            violations += VerifyUiButtonClickWiring(scene, controller);
        }

        foreach (string blockName in DisconnectButtonClickedBlockNames)
        {
            Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
            if (block == null)
                continue;

            if (block._EventHandler != null)
            {
                violations++;
                Debug.LogError($"[StudyRoomSceneMigrator] Block '{blockName}' still has an event handler.");
            }
        }

        if (violations == 0)
            Debug.Log("[StudyRoomSceneMigrator] Verification passed: click entry points routed through StudyRoomPuzzleController.");
        else
            Debug.LogError($"[StudyRoomSceneMigrator] Verification failed: {violations} issue(s).");
    }

    static bool MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(StudyRoomScenePath, OpenSceneMode.Single);
        Flowchart flowchart = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);

        if (flowchart == null)
        {
            Debug.LogError("[StudyRoomSceneMigrator] Flowchart not found.");
            return false;
        }

        var controller = flowchart.GetComponent<StudyRoomPuzzleController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<StudyRoomPuzzleController>();

        BackNavigator backNavigator = RoomInteractionSceneMigrationEditor
            .FindSceneComponents<BackNavigator>(scene)
            .FirstOrDefault();

        var so = new SerializedObject(controller);
        so.FindProperty("flowchart").objectReferenceValue = flowchart;
        so.FindProperty("backNavigator").objectReferenceValue = backNavigator;
        so.FindProperty("enableDebugLogging").boolValue = false;
        so.FindProperty("worldClicks").arraySize = 0;
        RoomInteractionSceneMigrationEditor.WriteRoutes(
            so.FindProperty("routes"),
            ClickRoutes);
        RoomInteractionSceneMigrationEditor.WriteOutcomes(
            so.FindProperty("blockOutcomes"),
            Array.Empty<RoomInteractionSceneMigrationEditor.BlockOutcomeSpec>());
        so.ApplyModifiedPropertiesWithoutUndo();

        RoomInteractionSceneMigrationEditor.DisconnectFungusButtonClickedHandlers(
            flowchart,
            DisconnectButtonClickedBlockNames);

        RewireUiButtonClicks(scene, controller);

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static void RewireUiButtonClicks(
        UnityEngine.SceneManagement.Scene scene,
        StudyRoomPuzzleController controller)
    {
        foreach (UiButtonClickTarget target in UiButtonClickTargets)
        {
            GameObject buttonObject = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.ObjectName);
            if (buttonObject == null)
            {
                Debug.LogWarning(
                    $"[StudyRoomSceneMigrator] UI button '{target.ObjectName}' not found; skipping rewire.");
                continue;
            }

            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                Debug.LogWarning(
                    $"[StudyRoomSceneMigrator] '{target.ObjectName}' has no UnityEngine.UI.Button; skipping rewire.");
                continue;
            }

            var buttonSo = new SerializedObject(button);
            RoomInteractionSceneMigrationEditor.SetPersistentStringUnityEvent(
                buttonSo,
                "m_OnClick",
                controller,
                RoomInteractionSceneMigrationEditor.StudyRoomControllerTypeName,
                nameof(StudyRoomPuzzleController.OnInteraction),
                target.InteractionId);
            buttonSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(button);
        }
    }

    static int VerifyClickRoutes(StudyRoomPuzzleController controller)
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
                    $"[StudyRoomSceneMigrator] Missing or wrong route for '{expected.InteractionId}' -> '{expected.BlockName}'.");
            }
        }

        return violations;
    }

    static int VerifyUiButtonClickWiring(
        UnityEngine.SceneManagement.Scene scene,
        StudyRoomPuzzleController controller)
    {
        int violations = 0;

        foreach (UiButtonClickTarget target in UiButtonClickTargets)
        {
            GameObject buttonObject = RoomInteractionSceneMigrationEditor.FindGameObjectInScene(scene, target.ObjectName);
            if (buttonObject == null)
            {
                violations++;
                Debug.LogError($"[StudyRoomSceneMigrator] UI button '{target.ObjectName}' not found.");
                continue;
            }

            var button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                violations++;
                Debug.LogError($"[StudyRoomSceneMigrator] '{target.ObjectName}' has no UnityEngine.UI.Button.");
                continue;
            }

            var buttonSo = new SerializedObject(button);
            SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            bool wired = false;
            if (calls != null)
            {
                for (int i = 0; i < calls.arraySize; i++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller)
                        continue;
                    if (call.FindPropertyRelative("m_MethodName").stringValue
                        != nameof(RoomInteractionController.OnInteraction))
                        continue;
                    if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue != target.InteractionId)
                        continue;

                    wired = true;
                    break;
                }
            }

            if (!wired)
            {
                violations++;
                Debug.LogError(
                    $"[StudyRoomSceneMigrator] '{target.ObjectName}' is not wired to OnInteraction('{target.InteractionId}').",
                    button);
            }
        }

        return violations;
    }
}
