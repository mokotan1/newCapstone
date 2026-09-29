using System;
using Fungus;
using Godlotto.Interaction;
using Godlotto.ModalInput;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-scene migration of the research room's fade and desk panel.</summary>
public static class BasementResearchRoomFlowchartRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/Basement/BasementResearchRoom.unity";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Basement Research Room Remove Flowchart")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing the research room.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Flowchart flowchart = UnityEngine.Object.FindFirstObjectByType<Flowchart>();
        GameObject desk = Find(scene, "Desk");
        GameObject panel = Find(scene, "Panel");
        if (flowchart == null || desk == null || panel == null
            || desk.GetComponent<Collider2D>() == null
            || desk.GetComponent<Clickable2D>() == null
            || panel.GetComponentInChildren<PanelBackspaceCloser>(true) == null)
            throw new InvalidOperationException("Research room preflight failed; scene was not saved.");

        Block[] blocks = flowchart.GetComponents<Block>();
        if (blocks.Length != 3
            || Array.Find(blocks, b => b.BlockName == "Start") == null
            || Array.Find(blocks, b => b.BlockName == "Desk_Clicked") == null
            || Array.Find(blocks, b => b.BlockName == "BackSpace_Panel") == null)
            throw new InvalidOperationException("Unexpected research room Flowchart; scene was not saved.");

        BasementResearchDeskOpener opener = desk.GetComponent<BasementResearchDeskOpener>()
            ?? desk.AddComponent<BasementResearchDeskOpener>();
        SerializedObject openerSo = new SerializedObject(opener);
        openerSo.FindProperty("panel").objectReferenceValue = panel;
        openerSo.ApplyModifiedPropertiesWithoutUndo();
        desk.GetComponent<Clickable2D>().enabled = false;

        if (panel.GetComponent<ModalInputScope>() == null)
            panel.AddComponent<ModalInputScope>();

        GameObject fadeRoot = new GameObject("BasementResearchRoomEnterFade");
        BasementRoomEnterFade fade = fadeRoot.AddComponent<BasementRoomEnterFade>();
        SerializedObject fadeSo = new SerializedObject(fade);
        fadeSo.FindProperty("durationSeconds").floatValue = GameplayScreenFade.BasementTransitionDurationSeconds;
        fadeSo.FindProperty("targetAlpha").floatValue = 0f;
        fadeSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEngine.Object.DestroyImmediate(flowchart.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BasementResearchRoomFlowchartRemovalPilot] Migrated fade and desk panel; removed Flowchart.");
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.gameObject;
        return null;
    }
}
