#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SettingScene·IntroScene 설정 패널에 해상도/전체화면 UI를 보장하고 IntegratedSettingUI에 연결합니다.
/// </summary>
public static class SettingDisplayControlsApplier
{
    const string SettingScenePath = "Assets/Scenes/godlotto/SettingScene.unity";
    const string IntroScenePath = "Assets/Scenes/godlotto/IntroScene.unity";
    const string MainMixerPath = "Assets/godlotto/MainMixer.mixer";

    [MenuItem("Tools/Godlotto/Apply Setting Display Controls (SettingScene)")]
    public static void ApplyToSettingScene()
    {
        ApplyToScene(SettingScenePath);
    }

    [MenuItem("Tools/Godlotto/Apply Setting Display Controls (IntroScene SettingPanel)")]
    public static void ApplyToIntroScene()
    {
        ApplyToScene(IntroScenePath);
    }

    [MenuItem("Tools/Godlotto/Apply Setting Display Controls (All)")]
    public static void ApplyToAllScenes()
    {
        ApplyToSettingScene();
        ApplyToIntroScene();
    }

    static void ApplyToScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        IntegratedSettingUI[] settings = Object.FindObjectsByType<IntegratedSettingUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (settings.Length == 0)
        {
            Debug.LogError($"[SettingDisplayControlsApplier] {scenePath}에서 IntegratedSettingUI를 찾을 수 없습니다.");
            return;
        }

        for (int i = 0; i < settings.Length; i++)
            ApplyToIntegratedSetting(settings[i]);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SettingDisplayControlsApplier] {scenePath} 설정 UI를 적용했습니다.");
    }

    static void ApplyToIntegratedSetting(IntegratedSettingUI settings)
    {
        Transform panelRoot = settings.panelRoot != null ? settings.panelRoot.transform : settings.transform;
        TMP_Dropdown resolutionDropdown = settings.resolutionDropdown;
        Toggle fullscreenToggle = settings.fullscreenToggle;

        SettingDisplayControlsFactory.EnsureDisplayControls(panelRoot, ref resolutionDropdown, ref fullscreenToggle);

        SerializedObject serialized = new SerializedObject(settings);
        serialized.Update();

        if (settings.audioMixer == null)
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MainMixerPath);
            SerializedProperty mixerProperty = serialized.FindProperty("audioMixer");
            if (mixerProperty != null && mixer != null)
                mixerProperty.objectReferenceValue = mixer;
        }

        SerializedProperty dropdownProperty = serialized.FindProperty("resolutionDropdown");
        SerializedProperty toggleProperty = serialized.FindProperty("fullscreenToggle");
        if (dropdownProperty != null)
            dropdownProperty.objectReferenceValue = resolutionDropdown;
        if (toggleProperty != null)
            toggleProperty.objectReferenceValue = fullscreenToggle;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }
}
#endif
