using NUnit.Framework;
using UnityEngine;

public class DeveloperModeGuiTypographyTests
{
    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(DeveloperModeGuiTypography.PlayerPrefsKey);
        PlayerPrefs.Save();
        DeveloperModeGuiTypography.Load();
    }

    [Test]
    public void Clamp_BelowMin_ReturnsMin()
    {
        Assert.AreEqual(DeveloperModeGuiTypography.MinFontSize, DeveloperModeGuiTypography.Clamp(1f));
    }

    [Test]
    public void Clamp_AboveMax_Returns25()
    {
        Assert.AreEqual(DeveloperModeGuiTypography.MaxFontSize, DeveloperModeGuiTypography.Clamp(99f));
    }

    [Test]
    public void SetFontSize_PersistsToPlayerPrefs()
    {
        DeveloperModeGuiTypography.SetFontSize(22f);

        Assert.AreEqual(22f, PlayerPrefs.GetFloat(DeveloperModeGuiTypography.PlayerPrefsKey), 0.001f);
        Assert.AreEqual(22f, DeveloperModeGuiTypography.FontSize, 0.001f);
    }

    [Test]
    public void Load_RestoresSavedValue()
    {
        PlayerPrefs.SetFloat(DeveloperModeGuiTypography.PlayerPrefsKey, 25f);
        PlayerPrefs.Save();

        DeveloperModeGuiTypography.Load();

        Assert.AreEqual(25f, DeveloperModeGuiTypography.FontSize, 0.001f);
    }

    [Test]
    public void ScaledLength_ScalesWithFontSize()
    {
        DeveloperModeGuiTypography.SetFontSize(DeveloperModeGuiTypography.MaxFontSize);
        float scaled = DeveloperModeGuiTypography.ScaledLength(100f);

        Assert.Greater(scaled, 100f);
    }
}
