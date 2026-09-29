using System;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Opening_Mention _open Flowchart → OpeningMentionOpenController (Codex AC).</summary>
public static class OpeningMentionOpenFlowchartRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/Opening_Mention _open.unity";
    const string ControllerHostName = "OpeningMentionOpenController";
    const string BellSoundAssetPath = "Assets/Sound/Bell_Sound.mp3";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Opening Mention Open Remove Flowchart")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing Opening_Mention _open.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject flowchart = Find(scene, "Flowchart");
        if (flowchart == null)
            throw new InvalidOperationException("Opening_Mention _open preflight failed; no Flowchart.");

        OpeningMentionController legacy = UnityEngine.Object.FindFirstObjectByType<OpeningMentionController>();
        if (legacy != null)
            UnityEngine.Object.DestroyImmediate(legacy);

        OpeningMentionOpenController existingOpen = UnityEngine.Object.FindFirstObjectByType<OpeningMentionOpenController>();
        if (existingOpen != null)
            UnityEngine.Object.DestroyImmediate(existingOpen.gameObject);

        GameObject host = new GameObject(ControllerHostName);
        OpeningMentionOpenController controller = host.AddComponent<OpeningMentionOpenController>();

        GameObject door = Find(scene, "Door");
        GameObject bell = Find(scene, "Bell");
        GameObject light = Find(scene, "light");
        GameObject mansionLights = Find(scene, "mansion_lights_0");
        if (door == null || bell == null)
            throw new InvalidOperationException("Opening_Mention _open missing Door or Bell.");

        Collider2D doorCollider = door.GetComponent<Collider2D>();
        Clickable2D doorClickable = door.GetComponent<Clickable2D>();
        if (doorClickable != null)
            doorClickable.enabled = false;

        AudioClip bellClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BellSoundAssetPath);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("doorCollider").objectReferenceValue = doorCollider;
        so.FindProperty("doorClickable").objectReferenceValue = doorClickable;
        so.FindProperty("lightObject").objectReferenceValue = light;
        so.FindProperty("mansionLightsObject").objectReferenceValue = mansionLights;
        so.FindProperty("bellSoundClip").objectReferenceValue = bellClip;
        so.FindProperty("doorSfxIndex").intValue = 7;
        so.FindProperty("startFadeDurationSeconds").floatValue = 1f;
        so.FindProperty("startFadeTargetAlpha").floatValue = 0f;
        so.FindProperty("exitFadeDurationSeconds").floatValue = 0.5f;
        so.FindProperty("exitFadeTargetAlpha").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();

        Button button = bell.GetComponent<Button>();
        if (button == null)
            throw new InvalidOperationException("Opening_Mention _open Bell has no Button.");

        SerializedObject buttonSo = new SerializedObject(button);
        SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.ClearArray();
        calls.InsertArrayElementAtIndex(0);
        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
            "OpeningMentionOpenController, Assembly-CSharp";
        call.FindPropertyRelative("m_MethodName").stringValue = "OnBellClicked";
        call.FindPropertyRelative("m_Mode").enumValueIndex = 1;
        call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
        buttonSo.ApplyModifiedPropertiesWithoutUndo();

        UnityEngine.Object.DestroyImmediate(flowchart);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[OpeningMentionOpenFlowchartRemovalPilot] Flowchart removed; open controller wired.");
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
