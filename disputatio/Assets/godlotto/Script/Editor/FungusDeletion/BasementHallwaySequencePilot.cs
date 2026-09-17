using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지하 복도 문 클릭 Fungus 블록을 Sequence 라우트로 대체합니다. (*SceneMigrator 아님, 단일 파일럿)
/// </summary>
public static class BasementHallwaySequencePilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/Basement/BasementHallway.unity";
    const string InteractionRootName = "BasementHallwaySequenceInteraction";

    static readonly string[] RetiredDoorBlockNames =
    {
        "Door_Brick_Clicked",
        "Door_Extraction_Clicked",
        "Door_Observation_Clicked",
        "Door_Research_Clicked",
        "Entry_ToUpperBasement_Clicked",
    };

    static readonly (string InteractionId, string GameObjectName, string JsonAssetPath)[] Routes =
    {
        ("door_brick", "Door_\uBCBD\uB3CC\uBC29", "Assets/godlotto/SequenceExamples/BasementHallway/door-brick.json"),
        ("door_extraction", "Door_\uCD94\uCD9C\uC2E4", "Assets/godlotto/SequenceExamples/BasementHallway/door-extraction.json"),
        ("door_observation", "Door_\uAD00\uCC30\uC2E4", "Assets/godlotto/SequenceExamples/BasementHallway/door-observation.json"),
        ("door_research", "Door_\uC5F0\uAD6C\uC2E4", "Assets/godlotto/SequenceExamples/BasementHallway/door-research.json"),
        ("entry_upper", "Entry_FromUpper", "Assets/godlotto/SequenceExamples/BasementHallway/entry-upper-basement.json"),
    };

    [MenuItem("Tools/Godlotto/Fungus Deletion/Basement Hallway Sequence Pilot")]
    public static void ApplyPilot()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Flowchart flowchart = UnityEngine.Object.FindFirstObjectByType<Flowchart>();
        if (flowchart == null)
        {
            Debug.LogError("[BasementHallwaySequencePilot] Flowchart not found.");
            return;
        }

        GameObject root = GameObject.Find(InteractionRootName);
        if (root == null)
            root = new GameObject(InteractionRootName);

        BasementHallwayInteractionController controller =
            root.GetComponent<BasementHallwayInteractionController>()
            ?? root.AddComponent<BasementHallwayInteractionController>();
        RoomInteractionSequenceHost sequenceHost =
            root.GetComponent<RoomInteractionSequenceHost>()
            ?? root.AddComponent<RoomInteractionSequenceHost>();

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("flowchart").objectReferenceValue = flowchart;
        controllerSo.FindProperty("sequenceHost").objectReferenceValue = sequenceHost;
        WriteWorldClicks(controllerSo.FindProperty("worldClicks"), scene);
        WriteSequenceRoutes(controllerSo.FindProperty("routes"));
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject hostSo = new SerializedObject(sequenceHost);
        hostSo.FindProperty("controller").objectReferenceValue = controller;
        hostSo.ApplyModifiedPropertiesWithoutUndo();

        int disabled = DisableRetiredDoorHandlers(flowchart);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "[BasementHallwaySequencePilot] Applied Sequence routes and disabled "
            + disabled
            + " Fungus ObjectClicked handler(s). Scene loads use GameplayScreenFade (Basement parity).");
    }

    static void WriteWorldClicks(SerializedProperty worldClicksProp, Scene scene)
    {
        worldClicksProp.arraySize = Routes.Length;
        for (int i = 0; i < Routes.Length; i++)
        {
            (string interactionId, string gameObjectName, _) = Routes[i];
            SerializedProperty element = worldClicksProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = interactionId;

            GameObject go = FindInScene(scene, gameObjectName);
            Collider2D collider = go != null ? go.GetComponent<Collider2D>() : null;
            Clickable2D clickable = go != null ? go.GetComponent<Clickable2D>() : null;
            element.FindPropertyRelative("collider").objectReferenceValue = collider;
            element.FindPropertyRelative("clickable").objectReferenceValue = clickable;

            if (go == null || collider == null)
            {
                Debug.LogWarning(
                    "[BasementHallwaySequencePilot] Missing collider for '"
                    + gameObjectName
                    + "'.");
            }
        }
    }

    static void WriteSequenceRoutes(SerializedProperty routesProp)
    {
        routesProp.arraySize = Routes.Length;
        for (int i = 0; i < Routes.Length; i++)
        {
            (string interactionId, _, string jsonPath) = Routes[i];
            TextAsset document = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
            SerializedProperty element = routesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = interactionId;
            element.FindPropertyRelative("fungusBlockName").stringValue = string.Empty;
            element.FindPropertyRelative("sequenceDocument").objectReferenceValue = document;
            element.FindPropertyRelative("sequenceStartBlock").stringValue = "start";

            if (document == null)
            {
                Debug.LogError(
                    "[BasementHallwaySequencePilot] Missing TextAsset at "
                    + jsonPath);
            }
        }
    }

    static int DisableRetiredDoorHandlers(Flowchart flowchart)
    {
        int disabled = 0;
        var retired = new HashSet<string>(RetiredDoorBlockNames, StringComparer.Ordinal);
        Block[] blocks = flowchart.GetComponentsInChildren<Block>(true);
        for (int i = 0; i < blocks.Length; i++)
        {
            Block block = blocks[i];
            if (block == null || !retired.Contains(block.BlockName))
                continue;

            EventHandler handler = block.GetComponent<EventHandler>();
            if (handler != null && handler.enabled)
            {
                handler.enabled = false;
                disabled++;
                EditorUtility.SetDirty(handler);
            }
        }

        return disabled;
    }

    static GameObject FindInScene(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int t = 0; t < transforms.Length; t++)
            {
                if (string.Equals(transforms[t].name, name, StringComparison.Ordinal))
                    return transforms[t].gameObject;
            }
        }

        return null;
    }
}
