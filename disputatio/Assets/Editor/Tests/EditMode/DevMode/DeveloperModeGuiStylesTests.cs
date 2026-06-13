using NUnit.Framework;
using UnityEngine;

public class DeveloperModeGuiStylesTests
{
    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(DeveloperModeGuiTypography.PlayerPrefsKey);
        PlayerPrefs.Save();
        DeveloperModeGuiTypography.Load();
    }

    [Test]
    public void IsReady_IsFalseBeforeOnGuiBuild()
    {
        var styles = new DeveloperModeGuiStyles();

        Assert.IsFalse(styles.IsReady);
    }

    [Test]
    public void ScaledHeight_UsesTypographyScaleFactor()
    {
        DeveloperModeGuiTypography.SetFontSize(DeveloperModeGuiTypography.MaxFontSize);
        var styles = new DeveloperModeGuiStyles();

        Assert.Greater(styles.ScaledHeight(100f), 100f);
    }
}
