using System;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Start+FadeScreen만 있는 지하 방에서 Flowchart를 제거하고 C# 입장 페이드를 붙입니다.
/// </summary>
public static class BasementSimpleRoomFlowchartRemovalPilot
{
    static readonly (string ScenePath, float EnterTargetAlpha)[] Targets =
    {
        ("Assets/Scenes/Mokotan/Basement/BasementBrickRoom.unity", 1f),
        ("Assets/Scenes/Mokotan/Basement/BasementExtractionRoom.unity", 0f),
        ("Assets/Scenes/Mokotan/Basement/BasementObservationRoom.unity", 1f),
        ("Assets/Scenes/Mokotan/Basement/BasementResearchRoom.unity", 0f),
    };

    [MenuItem("Tools/Godlotto/Fungus Deletion/Basement Simple Rooms Remove Flowchart")]
    public static void ApplyAll()
    {
        int changed = 0;
        foreach ((string scenePath, float enterAlpha) in Targets)
        {
            if (ApplyScene(scenePath, enterAlpha))
                changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            "[BasementSimpleRoomFlowchartRemovalPilot] Updated "
            + changed
            + "/"
            + Targets.Length
            + " scene(s).");
    }

    static bool ApplyScene(string scenePath, float enterTargetAlpha)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Flowchart flowchart = UnityEngine.Object.FindFirstObjectByType<Flowchart>();
        if (flowchart == null)
        {
            Debug.LogWarning("[BasementSimpleRoomFlowchartRemovalPilot] No Flowchart in " + scenePath);
            return false;
        }

        GameObject roomRoot = FindRoomRoot(scene) ?? flowchart.gameObject;
        BasementRoomEnterFade fade =
            roomRoot.GetComponent<BasementRoomEnterFade>() ?? roomRoot.AddComponent<BasementRoomEnterFade>();
        SerializedObject fadeSo = new SerializedObject(fade);
        fadeSo.FindProperty("durationSeconds").floatValue =
            GameplayScreenFade.BasementTransitionDurationSeconds;
        fadeSo.FindProperty("targetAlpha").floatValue = enterTargetAlpha;
        fadeSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEngine.Object.DestroyImmediate(flowchart.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BasementSimpleRoomFlowchartRemovalPilot] Removed Flowchart in " + scenePath);
        return true;
    }

    static GameObject FindRoomRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name.StartsWith("Room_", StringComparison.Ordinal))
                return roots[i];
        }

        return null;
    }
}
