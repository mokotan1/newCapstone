using Fungus;
using NUnit.Framework;
using UnityEngine;

public class SettingsDisplayPreferencesTests
{
    string _previousLanguage;
    int _previousScale;

    [SetUp]
    public void SetUp()
    {
        _previousLanguage = SetLanguage.mostRecentLanguage;
        _previousScale = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.AnswerTextScale, 100);
    }

    [TearDown]
    public void TearDown()
    {
        SetLanguage.mostRecentLanguage = _previousLanguage;
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.AnswerTextScale, _previousScale);
        PlayerPrefs.Save();
    }

    [Test]
    public void PersistAndReadAnswerTextScale()
    {
        SettingsDisplayPreferences.SetAnswerTextScale(140);
        Assert.AreEqual(140, SettingsDisplayPreferences.GetAnswerTextScale());
        Assert.AreEqual(37.8f, SettingsDisplayPreferences.ResolveAnswerFontSize(), 0.01f);
    }

    [Test]
    public void ApplyLocaleNormalizesAndSetsFungusLanguage()
    {
        SettingsDisplayPreferences.ApplyLocale("en-US");
        Assert.AreEqual(CheshireLocaleResolver.English, SetLanguage.mostRecentLanguage);
        Assert.AreEqual(CheshireLocaleResolver.English, SettingsDisplayPreferences.ResolveLocale());
    }

    [Test]
    public void InvalidScaleFallsBackTo100()
    {
        SettingsDisplayPreferences.SetAnswerTextScale(0);
        Assert.AreEqual(100, SettingsDisplayPreferences.GetAnswerTextScale());
    }
}
