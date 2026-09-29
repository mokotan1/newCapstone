using System;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BasementHallway Start→FadeScreen Flowchart entry fade를 C# BasementRoomEnterFade로 옮기고
/// Flowchart를 제거합니다. Sequence 문 라우트/Host는 유지합니다.
/// </summary>
public static class BasementHallwayStartFadeRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/Basement/BasementHallway.unity";
    const string InteractionRootName = "BasementHallwaySequenceInteraction";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Basement Hallway Remove Start Flowchart")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing BasementHallway.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Flowchart flowchart = UnityEngine.Object.FindFirstObjectByType<Flowchart>();
        if (flowchart == null)
            throw new InvalidOperationException("BasementHallway Flowchart not found; scene was not saved.");

        Block start = Array.Find(flowchart.GetComponents<Block>(), b => b != null && b.BlockName == "Start");
        if (start == null)
            throw new InvalidOperationException("BasementHallway Start block missing; scene was not saved.");

        BasementHallwayInteractionController controller =
            UnityEngine.Object.FindFirstObjectByType<BasementHallwayInteractionController>();
        RoomInteractionSequenceHost host =
            UnityEngine.Object.FindFirstObjectByType<RoomInteractionSequenceHost>();
        if (controller == null || host == null)
            throw new InvalidOperationException(
                "BasementHallway Sequence host/controller missing; scene was not saved.");

        GameObject fadeRoot = GameObject.Find(InteractionRootName);
        if (fadeRoot == null)
            fadeRoot = new GameObject("BasementHallwayEnterFade");

        BasementRoomEnterFade fade =
            fadeRoot.GetComponent<BasementRoomEnterFade>() ?? fadeRoot.AddComponent<BasementRoomEnterFade>();
        SerializedObject fadeSo = new SerializedObject(fade);
        fadeSo.FindProperty("durationSeconds").floatValue =
            GameplayScreenFade.BasementTransitionDurationSeconds;
        fadeSo.FindProperty("targetAlpha").floatValue =
            GameplayScreenFade.BasementDoorFadeTargetAlpha;
        fadeSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEngine.Object.DestroyImmediate(flowchart.gameObject);

        GameObject fungusState = GameObject.Find("_FungusState");
        if (fungusState != null)
            UnityEngine.Object.DestroyImmediate(fungusState);

        if (UnityEngine.Object.FindFirstObjectByType<Flowchart>() != null)
            throw new InvalidOperationException("Flowchart still present after removal.");
        if (UnityEngine.Object.FindFirstObjectByType<BasementHallwayInteractionController>() == null
            || UnityEngine.Object.FindFirstObjectByType<RoomInteractionSequenceHost>() == null
            || UnityEngine.Object.FindFirstObjectByType<BasementRoomEnterFade>() == null)
            throw new InvalidOperationException("Sequence host or enter fade missing after removal.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "[BasementHallwayStartFadeRemovalPilot] Start fade moved to BasementRoomEnterFade; "
            + "Flowchart removed. Sequence door routes preserved.");
    }

    [MenuItem("Tools/Godlotto/Fungus Deletion/Basement Hallway Remove FungusState Leftover")]
    public static int RemoveFungusStateLeftover()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing BasementHallway.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject fungusState = GameObject.Find("_FungusState");
        if (fungusState != null)
            UnityEngine.Object.DestroyImmediate(fungusState);

        if (GameObject.Find("_FungusState") != null)
            throw new InvalidOperationException("_FungusState still present.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BasementHallwayStartFadeRemovalPilot] Removed leftover _FungusState.");
        return 1;
    }
}
