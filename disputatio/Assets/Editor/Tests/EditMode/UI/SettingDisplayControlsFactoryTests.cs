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
}
