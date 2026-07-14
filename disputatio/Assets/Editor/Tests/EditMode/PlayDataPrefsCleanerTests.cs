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
}
