using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-scene Hall_animate Parret_Animated → HallAnimateSequence; removes Flowchart.</summary>
public static class HallAnimateFlowchartRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/First Floor/Hall_animate.unity";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Hall Animate Remove Flowchart")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing Hall_animate.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject flowchart = Find(scene, "Flowchart");
        GameObject parrot = Find(scene, "Parret_Animate");
        GameObject destination = Find(scene, "destination");
        if (flowchart == null || parrot == null || destination == null)
            throw new InvalidOperationException("Hall_animate preflight failed; scene was not saved.");

        GameObject host = new GameObject("HallAnimateSequence");
        HallAnimateSequence sequence = host.AddComponent<HallAnimateSequence>();
        SerializedObject so = new SerializedObject(sequence);
        so.FindProperty("parrot").objectReferenceValue = parrot.transform;
        so.FindProperty("destination").objectReferenceValue = destination.transform;
        so.FindProperty("enterFadeDurationSeconds").floatValue = 1f;
        so.FindProperty("enterFadeTargetAlpha").floatValue = 0f;
        so.FindProperty("exitFadeDurationSeconds").floatValue = 0.25f;
        so.FindProperty("exitFadeTargetAlpha").floatValue = 1f;
        so.FindProperty("moveDurationSeconds").floatValue = 1f;
        so.FindProperty("sfxIndex").intValue = 9;
        so.ApplyModifiedPropertiesWithoutUndo();

        UnityEngine.Object.DestroyImmediate(flowchart);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HallAnimateFlowchartRemovalPilot] Parret_Animated moved to HallAnimateSequence; Flowchart removed.");
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child.gameObject;
            }
        }

        return null;
    }
}
