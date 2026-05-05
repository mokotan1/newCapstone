using UnityEditor;
using UnityEngine;

/// <summary>
/// 에디터에서 플레이 버튼으로 진입할 때 진행 데이터를 초기화합니다.
/// (설정 볼륨·전체화면·해상도는 <see cref="PlayDataPrefsCleaner"/> 에서 유지)
/// </summary>
[InitializeOnLoad]
internal static class PlayModePlayDataReset
{
    static PlayModePlayDataReset()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings(deleteEditorFungusSaveFiles: true);
        Debug.Log("[PlayModePlayDataReset] 플레이 데이터 초기화 완료 (오디오·화면 설정 유지).");
    }
}
