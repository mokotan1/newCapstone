using System.IO;
using UnityEngine;

/// <summary>
/// 진행·세이브 관련 PlayerPrefs(및 에디터에서는 Fungus 디스크 세이브)를 비우되,
/// <see cref="SettingPlayerPrefsKeys"/> 로 저장되는 그래픽·오디오 설정은 유지합니다.
/// </summary>
public static class PlayDataPrefsCleaner
{
    /// <param name="deleteEditorFungusSaveFiles">
    /// 에디터에서만 적용. Standalone 빌드에서는 무시됩니다.
    /// 테스트 등에서 사용자 <c>FungusSaves</c> 폴더를 건드리지 않으려면 false 로 호출하세요.
    /// </param>
    public static void ClearProgressPreserveAudioVideoSettings(bool deleteEditorFungusSaveFiles = true)
    {
        float bgm = PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
        float sfx = PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
        int fullscreen = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen, SettingPlayerPrefsKeys.FullscreenDefaultEnabled);
        bool hadResolution = PlayerPrefs.HasKey(SettingPlayerPrefsKeys.ResolutionIndex);
        int resolutionIndex = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex, -1);

        PlayerPrefs.DeleteAll();

        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, bgm);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, sfx);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, fullscreen);
        if (hadResolution)
            PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, resolutionIndex);

        PlayerPrefs.Save();

#if UNITY_EDITOR
        if (deleteEditorFungusSaveFiles)
            TryDeletePersistentFungusSaveFiles();
#endif
    }

#if UNITY_EDITOR
    static void TryDeletePersistentFungusSaveFiles()
    {
        try
        {
            string dir = Application.persistentDataPath + "/FungusSaves";
            if (!Directory.Exists(dir))
                return;

            foreach (string path in Directory.GetFiles(dir))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            Debug.LogWarning("[PlayDataPrefsCleaner] FungusSaves 폴더 정리 실패: " + ex.Message);
        }
    }
#endif
}
