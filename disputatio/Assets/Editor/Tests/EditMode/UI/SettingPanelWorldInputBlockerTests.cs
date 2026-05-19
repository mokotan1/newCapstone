using NUnit.Framework;
using UnityEngine;

public class SettingPanelWorldInputBlockerTests
{
    private GameObject settingPanel;
    private GameObject worldObject;
    private GameObject settingChild;
    private GameObject disabledWorldObject;

    [SetUp]
    public void SetUp()
    {
        SettingPanelWorldInputBlocker.End();

        settingPanel = new GameObject("SettingPanel");
        settingChild = new GameObject("SettingChild");
        settingChild.transform.SetParent(settingPanel.transform);
        settingChild.AddComponent<BoxCollider2D>();

        worldObject = new GameObject("WorldObject");
        worldObject.AddComponent<BoxCollider2D>();

        disabledWorldObject = new GameObject("AlreadyDisabledWorldObject");
        var disabledCollider = disabledWorldObject.AddComponent<BoxCollider2D>();
        disabledCollider.enabled = false;
    }

    [TearDown]
    public void TearDown()
    {
        SettingPanelWorldInputBlocker.End();
        Object.DestroyImmediate(settingPanel);
        Object.DestroyImmediate(worldObject);
        Object.DestroyImmediate(disabledWorldObject);
    }

    [Test]
    public void Begin_DisablesEnabledWorldCollidersButKeepsSettingPanelColliders()
    {
        var settingCollider = settingChild.GetComponent<BoxCollider2D>();
        var worldCollider = worldObject.GetComponent<BoxCollider2D>();
        var alreadyDisabledCollider = disabledWorldObject.GetComponent<BoxCollider2D>();

        SettingPanelWorldInputBlocker.Begin(settingPanel);

        Assert.IsTrue(settingCollider.enabled);
        Assert.IsFalse(worldCollider.enabled);
        Assert.IsFalse(alreadyDisabledCollider.enabled);
    }

    [Test]
    public void End_RestoresOnlyCollidersDisabledByBlocker()
    {
        var worldCollider = worldObject.GetComponent<BoxCollider2D>();
        var alreadyDisabledCollider = disabledWorldObject.GetComponent<BoxCollider2D>();

        SettingPanelWorldInputBlocker.Begin(settingPanel);
        SettingPanelWorldInputBlocker.End();

        Assert.IsTrue(worldCollider.enabled);
        Assert.IsFalse(alreadyDisabledCollider.enabled);
    }
}
