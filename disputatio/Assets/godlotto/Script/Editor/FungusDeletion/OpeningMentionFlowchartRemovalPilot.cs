using System;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ensures Opening_Mention has a wired OpeningMentionController and no Flowchart.
/// Controller must live on a host outside Flowchart so destroying Flowchart cannot orphan Bell.
/// </summary>
public static class OpeningMentionFlowchartRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/Opening_Mention.unity";
    const string ControllerHostName = "OpeningMentionController";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Opening Mention Remove Flowchart")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing Opening_Mention.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        OpeningMentionController controller = EnsureController(scene);
        WireBell(scene, controller);
        WireFence(scene, controller);

        GameObject flowchart = Find(scene, "Flowchart");
        if (flowchart != null)
            UnityEngine.Object.DestroyImmediate(flowchart);

        if (UnityEngine.Object.FindFirstObjectByType<OpeningMentionController>() == null)
            throw new InvalidOperationException("Opening_Mention repair failed; controller missing after apply.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[OpeningMentionFlowchartRemovalPilot] OpeningMentionController wired; Flowchart removed if present.");
    }

    static OpeningMentionController EnsureController(Scene scene)
    {
        OpeningMentionController existing = UnityEngine.Object.FindFirstObjectByType<OpeningMentionController>();
        if (existing != null)
            return existing;

        GameObject host = Find(scene, ControllerHostName);
        if (host == null)
            host = new GameObject(ControllerHostName);

        return host.AddComponent<OpeningMentionController>();
    }

    static void WireBell(Scene scene, OpeningMentionController controller)
    {
        GameObject bell = Find(scene, "Bell");
        if (bell == null)
            throw new InvalidOperationException("Opening_Mention Bell GameObject missing.");

        Button button = bell.GetComponent<Button>();
        if (button == null)
            throw new InvalidOperationException("Opening_Mention Bell has no Button.");

        SerializedObject so = new SerializedObject(button);
        SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.ClearArray();
        calls.InsertArrayElementAtIndex(0);
        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "OpeningMentionController, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue = "OnBellClicked";
        call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Event
        call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireFence(Scene scene, OpeningMentionController controller)
    {
        GameObject fence = Find(scene, "fance") ?? Find(scene, "fence") ?? Find(scene, "Fence");
        if (fence == null)
            throw new InvalidOperationException("Opening_Mention fence GameObject missing.");

        Collider2D collider = fence.GetComponent<Collider2D>();
        if (collider == null)
            throw new InvalidOperationException("Opening_Mention fence has no Collider2D.");

        Clickable2D clickable = fence.GetComponent<Clickable2D>();
        if (clickable != null)
            clickable.enabled = false;

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("fenceCollider").objectReferenceValue = collider;
        so.FindProperty("fenceClickable").objectReferenceValue = clickable;
        so.FindProperty("enableFenceInteraction").boolValue = true;
        so.FindProperty("openSceneName").stringValue = SceneNames.OpeningMentionOpen;
        so.ApplyModifiedPropertiesWithoutUndo();
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
