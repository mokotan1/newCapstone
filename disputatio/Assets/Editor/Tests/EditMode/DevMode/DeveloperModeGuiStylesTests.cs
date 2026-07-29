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
    public void MarkDirty_ClearsReadyStateWithoutTouchingGuiSkin()
    {
        var styles = new DeveloperModeGuiStyles();
        ForceMarkReadyForTest(styles);
        Assert.IsTrue(styles.IsReady);

        styles.MarkDirty();

        Assert.IsFalse(styles.IsReady);
    }

    static void ForceMarkReadyForTest(DeveloperModeGuiStyles styles)
    {
        var type = typeof(DeveloperModeGuiStyles);
        type.GetProperty(nameof(DeveloperModeGuiStyles.Label))!.SetValue(styles, GUIStyle.none);
        type.GetProperty(nameof(DeveloperModeGuiStyles.Button))!.SetValue(styles, GUIStyle.none);
        type.GetProperty(nameof(DeveloperModeGuiStyles.TextField))!.SetValue(styles, GUIStyle.none);
        type.GetProperty(nameof(DeveloperModeGuiStyles.Box))!.SetValue(styles, GUIStyle.none);
        type.GetProperty(nameof(DeveloperModeGuiStyles.Window))!.SetValue(styles, GUIStyle.none);
        type.GetProperty(nameof(DeveloperModeGuiStyles.ToggleButton))!.SetValue(styles, GUIStyle.none);
    }

    [Test]
    public void ScaledHeight_UsesTypographyScaleFactor()
    {
        DeveloperModeGuiTypography.SetFontSize(DeveloperModeGuiTypography.MaxFontSize);
        var styles = new DeveloperModeGuiStyles();

        Assert.Greater(styles.ScaledHeight(100f), 100f);
    }
}
