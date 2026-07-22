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
        AudioVideoSettingsSnapshot snapshot = CaptureAudioVideoSettings();

        PlayerPrefs.DeleteAll();

        ApplyAudioVideoSettings(snapshot);
        PlayerPrefs.Save();

#if UNITY_EDITOR
        if (deleteEditorFungusSaveFiles)
            TryDeletePersistentFungusSaveFiles();
#endif
    }

    /// <summary>
    /// BGM/SFX/전체화면/해상도 설정의 현재 값을 캡처합니다. <see cref="SettingPlayerPrefsKeys"/>만을
    /// 명시적으로 참조하는 단일 분류 지점입니다. QA 프로필 격리(<c>Godlotto.QA.Profile.QaProfileService</c>)
    /// 등 다른 호출자도 이 메서드를 재사용해, "무엇이 설정 키인가"에 대한 정의가 여러 곳에서
    /// 어긋나지 않도록 합니다(단일 정의 원칙, 값은 절대 변경하지 마세요 — 기존 저장 데이터와의 호환성).
    /// </summary>
    public static AudioVideoSettingsSnapshot CaptureAudioVideoSettings()
    {
        float bgm = PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
        float sfx = PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
        int fullscreen = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen, SettingPlayerPrefsKeys.FullscreenDefaultEnabled);
        bool hadResolution = PlayerPrefs.HasKey(SettingPlayerPrefsKeys.ResolutionIndex);
        int resolutionIndex = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex, -1);

        return new AudioVideoSettingsSnapshot(bgm, sfx, fullscreen, hadResolution, resolutionIndex);
    }

    /// <summary>
    /// <see cref="CaptureAudioVideoSettings"/>로 캡처한 스냅샷을 PlayerPrefs에 다시 적용합니다.
    /// <c>PlayerPrefs.Save()</c>는 호출하지 않으며, 저장 시점은 호출자가 책임집니다.
    /// </summary>
    public static void ApplyAudioVideoSettings(AudioVideoSettingsSnapshot snapshot)
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, snapshot.BgmVolume);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, snapshot.SfxVolume);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, snapshot.Fullscreen);
        if (snapshot.HadResolution)
            PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, snapshot.ResolutionIndex);
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

/// <summary>
/// <see cref="PlayDataPrefsCleaner"/>가 다루는 오디오/비디오 설정 키의 명시적 분류 스냅샷.
/// "진행 초기화(새 게임)"와 "QA 프로필 전환" 모두 동일한 단일 정의를 재사용합니다.
/// </summary>
public readonly struct AudioVideoSettingsSnapshot
{
    public float BgmVolume { get; }
    public float SfxVolume { get; }
    public int Fullscreen { get; }
    public bool HadResolution { get; }
    public int ResolutionIndex { get; }

    public AudioVideoSettingsSnapshot(float bgmVolume, float sfxVolume, int fullscreen, bool hadResolution, int resolutionIndex)
    {
        BgmVolume = bgmVolume;
        SfxVolume = sfxVolume;
        Fullscreen = fullscreen;
        HadResolution = hadResolution;
        ResolutionIndex = resolutionIndex;
    }

    /// <summary>두 스냅샷이 완전히 동일한 값을 나타내는지 비교합니다(방어적 점검용).</summary>
    public static bool AreEqual(AudioVideoSettingsSnapshot a, AudioVideoSettingsSnapshot b)
    {
        return a.BgmVolume.Equals(b.BgmVolume)
            && a.SfxVolume.Equals(b.SfxVolume)
            && a.Fullscreen == b.Fullscreen
            && a.HadResolution == b.HadResolution
            && (!a.HadResolution || a.ResolutionIndex == b.ResolutionIndex);
    }
}
