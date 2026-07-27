using NUnit.Framework;
using UnityEngine;

public class PlayDataPrefsCleanerTests
{
    const string JunkKey = "__PlayDataCleanerTest_Junk__";
    const string LastBookPageKey = "LastBookPage_TestBook";

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(JunkKey);
        PlayerPrefs.DeleteKey(LastBookPageKey);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.BgmVolume);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.SfxVolume);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.Fullscreen);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.ResolutionIndex);
        PlayerPrefs.Save();
    }

    [Test]
    public void ClearProgress_RemovesUnknownKeys_PreservesSettings()
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, 0.42f);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, 0.55f);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, 0);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, 3);
        PlayerPrefs.SetInt(JunkKey, 99);
        PlayerPrefs.SetInt(LastBookPageKey, 4);
        PlayerPrefs.Save();

        PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings(deleteEditorFungusSaveFiles: false);

        Assert.That(PlayerPrefs.HasKey(JunkKey), Is.False, "진행용 임의 키는 삭제되어야 합니다.");
        Assert.That(PlayerPrefs.HasKey(LastBookPageKey), Is.False, "LastBookPage_* 진행 키는 삭제되어야 합니다.");
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume), Is.EqualTo(0.42f).Within(0.001f));
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume), Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen), Is.EqualTo(0));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex), Is.EqualTo(3));
    }

    [Test]
    public void ClearProgress_WhenResolutionNeverSaved_DoesNotWriteResolutionKey()
    {
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.ResolutionIndex);
        PlayerPrefs.SetInt(JunkKey, 1);
        PlayerPrefs.Save();

        PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings(deleteEditorFungusSaveFiles: false);

        Assert.That(PlayerPrefs.HasKey(JunkKey), Is.False);
        Assert.That(PlayerPrefs.HasKey(SettingPlayerPrefsKeys.ResolutionIndex), Is.False);
    }

    // -----------------------------------------------------------------------
    // CaptureAudioVideoSettings / ApplyAudioVideoSettings - extracted explicit key
    // classification reused by Godlotto.QA.Profile.QaProfileService (Task 4).
    // -----------------------------------------------------------------------

    [Test]
    public void CaptureAudioVideoSettings_ThenApply_RoundTripsExactly()
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, 0.11f);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, 0.22f);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, 1);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, 7);
        PlayerPrefs.Save();

        AudioVideoSettingsSnapshot snapshot = PlayDataPrefsCleaner.CaptureAudioVideoSettings();

        // Mutate everything away from the captured snapshot to prove Apply restores it.
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, 0.99f);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, 0.01f);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, 0);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, 0);
        PlayerPrefs.Save();

        PlayDataPrefsCleaner.ApplyAudioVideoSettings(snapshot);
        PlayerPrefs.Save();

        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume), Is.EqualTo(0.11f).Within(0.0001f));
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume), Is.EqualTo(0.22f).Within(0.0001f));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen), Is.EqualTo(1));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex), Is.EqualTo(7));
        Assert.IsTrue(AudioVideoSettingsSnapshot.AreEqual(snapshot, PlayDataPrefsCleaner.CaptureAudioVideoSettings()));
    }

    [Test]
    public void CaptureAudioVideoSettings_WhenResolutionNeverSaved_HadResolutionIsFalse()
    {
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.ResolutionIndex);
        PlayerPrefs.Save();

        AudioVideoSettingsSnapshot snapshot = PlayDataPrefsCleaner.CaptureAudioVideoSettings();

        Assert.IsFalse(snapshot.HadResolution);
    }
}
