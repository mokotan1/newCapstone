using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingDisplayControlsFactoryTests
{
    GameObject panelRoot;

    [SetUp]
    public void SetUp()
    {
        panelRoot = new GameObject("SettingPanel", typeof(RectTransform));
    }

    [TearDown]
    public void TearDown()
    {
        if (panelRoot != null)
            Object.DestroyImmediate(panelRoot);
    }

    [Test]
    public void EnsureDisplayControls_CreatesResolutionDropdownAndFullscreenToggle()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;

        SettingDisplayControlsFactory.EnsureDisplayControls(panelRoot.transform, ref dropdown, ref toggle);

        Assert.That(dropdown, Is.Not.Null);
        Assert.That(toggle, Is.Not.Null);
        Assert.That(dropdown.name, Is.EqualTo("Resolution_Dropdown"));
        Assert.That(toggle.name, Is.EqualTo("Fullscreen Toggle"));
    }

    [Test]
    public void EnsureDisplayControls_IsIdempotentWhenControlsAlreadyExist()
    {
        TMP_Dropdown dropdown = null;
        Toggle toggle = null;
        SettingDisplayControlsFactory.EnsureDisplayControls(panelRoot.transform, ref dropdown, ref toggle);

        TMP_Dropdown firstDropdown = dropdown;
        Toggle firstToggle = toggle;
        int childCountAfterFirst = panelRoot.transform.childCount;

        SettingDisplayControlsFactory.EnsureDisplayControls(panelRoot.transform, ref dropdown, ref toggle);

        Assert.That(dropdown, Is.SameAs(firstDropdown));
        Assert.That(toggle, Is.SameAs(firstToggle));
        Assert.That(panelRoot.transform.childCount, Is.EqualTo(childCountAfterFirst));
    }

    [Test]
    public void EnsureDisplayControls_RestylesExistingDropdownToWoodContract()
    {
        GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
        dropdownObject.name = "Resolution_Dropdown";
        dropdownObject.transform.SetParent(panelRoot.transform, false);
        TMP_Dropdown existing = dropdownObject.GetComponent<TMP_Dropdown>();
        existing.captionText.enableAutoSizing = true;
        existing.captionText.fontSizeMin = 4f;
        existing.captionText.enableWordWrapping = true;
        existing.captionText.fontSize = 48f;
        Image face = existing.GetComponent<Image>();
        face.color = Color.white;

        GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
        toggleObject.name = "Fullscreen Toggle";
        toggleObject.transform.SetParent(panelRoot.transform, false);
        Toggle existingToggle = toggleObject.GetComponent<Toggle>();

        TMP_Dropdown dropdown = existing;
        Toggle toggle = existingToggle;
        SettingDisplayControlsFactory.EnsureDisplayControls(panelRoot.transform, ref dropdown, ref toggle);

        Assert.That(dropdown, Is.SameAs(existing));
        Assert.That(dropdown.captionText.enableAutoSizing, Is.False);
        Assert.That(dropdown.captionText.enableWordWrapping, Is.False);
        Assert.That(dropdown.captionText.fontSize, Is.EqualTo(SettingsWoodPanelSpec.UiFontSize));
        Assert.That(dropdown.GetComponent<Image>().color, Is.EqualTo(SettingsWoodPanelSpec.InputBackground));
        LayoutElement layout = dropdown.GetComponent<LayoutElement>();
        Assert.That(layout.preferredWidth, Is.EqualTo(SettingsWoodPanelSpec.ControlWidth));
        Assert.That(layout.preferredHeight, Is.EqualTo(SettingsWoodPanelSpec.ControlHeight));
    }
}
