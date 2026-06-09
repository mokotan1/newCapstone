#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// UtilityRoom 전기 패널에 <see cref="ElectricSwitchUiSync"/>를 부착합니다.
/// </summary>
public static class ElectricSwitchUiSceneApplier
{
    const string UtilityRoomScenePath = "Assets/Scenes/Mokotan/First Floor/1foorLeft/UtilityRoom.unity";
    const string PanelObjectName = "electrical control panel_Panel";

    [MenuItem("Tools/Godlotto/Apply Electric Switch UI Sync (UtilityRoom)")]
    public static void ApplyToUtilityRoom()
    {
        Scene scene = EditorSceneManager.OpenScene(UtilityRoomScenePath, OpenSceneMode.Single);
        GameObject panel = GameObject.Find(PanelObjectName);
        if (panel == null)
        {
            Debug.LogError($"[ElectricSwitchUiSceneApplier] '{PanelObjectName}'를 찾을 수 없습니다.");
            return;
        }

        ElectricSwitchUiSync sync = panel.GetComponent<ElectricSwitchUiSync>();
        if (sync == null)
            sync = Undo.AddComponent<ElectricSwitchUiSync>(panel);

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[ElectricSwitchUiSceneApplier] UtilityRoom 전기 패널에 ElectricSwitchUiSync를 적용했습니다.");
    }
}
#endif
