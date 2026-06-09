using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenPanelRegistryTests
{
    GameObject burner;
    GameObject fripan;
    GameObject parrot;
    GameObject sink;
    GameObject bottle;
    KitchenPanelRegistry registry;

    [SetUp]
    public void SetUp()
    {
        registry = new GameObject("Registry").AddComponent<KitchenPanelRegistry>();
        burner = CreatePanel("burner");
        fripan = CreatePanel("firpan_Panel");
        parrot = CreatePanel("Parret");
        sink = CreatePanel("Sink_Pannel");
        bottle = CreatePanel("Bottle");

        SetPanelField("burnerPanel", burner);
        SetPanelField("fripanPanel", fripan);
        SetPanelField("parrotPanel", parrot);
        SetPanelField("sinkPanel", sink);
        SetPanelField("bottlePanel", bottle);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(registry.gameObject);
        Object.DestroyImmediate(burner);
        Object.DestroyImmediate(fripan);
        Object.DestroyImmediate(parrot);
        Object.DestroyImmediate(sink);
        Object.DestroyImmediate(bottle);
    }

    [Test]
    public void OpenAndCloseBurnerPanel_TogglesActiveState()
    {
        burner.SetActive(false);

        registry.OpenBurnerPanel();
        Assert.IsTrue(burner.activeSelf);

        registry.CloseBurnerPanel();
        Assert.IsFalse(burner.activeSelf);
    }

    [Test]
    public void OpenAndCloseFripanPanel_TogglesActiveState()
    {
        fripan.SetActive(false);

        registry.OpenFripanPanel();
        Assert.IsTrue(fripan.activeSelf);

        registry.CloseFripanPanel();
        Assert.IsFalse(fripan.activeSelf);
    }

    [Test]
    public void OpenAndCloseParrotPanel_TogglesActiveState()
    {
        parrot.SetActive(false);

        registry.OpenParrotPanel();
        Assert.IsTrue(parrot.activeSelf);

        registry.CloseParrotPanel();
        Assert.IsFalse(parrot.activeSelf);
    }

    [Test]
    public void OpenAndCloseSinkPanel_TogglesActiveState()
    {
        sink.SetActive(false);

        registry.OpenSinkPanel();
        Assert.IsTrue(sink.activeSelf);

        registry.CloseSinkPanel();
        Assert.IsFalse(sink.activeSelf);
    }

    [Test]
    public void OpenAndCloseBottlePanel_TogglesActiveState()
    {
        bottle.SetActive(false);

        registry.OpenBottlePanel();
        Assert.IsTrue(bottle.activeSelf);

        registry.CloseBottlePanel();
        Assert.IsFalse(bottle.activeSelf);
    }

    [Test]
    public void CloseAllPanels_DeactivatesRegisteredPanels()
    {
        burner.SetActive(true);
        fripan.SetActive(true);
        parrot.SetActive(true);
        sink.SetActive(true);
        bottle.SetActive(true);

        registry.CloseAllPanels();

        Assert.IsFalse(burner.activeSelf);
        Assert.IsFalse(fripan.activeSelf);
        Assert.IsFalse(parrot.activeSelf);
        Assert.IsFalse(sink.activeSelf);
        Assert.IsFalse(bottle.activeSelf);
    }

    void SetPanelField(string fieldName, GameObject panel)
    {
        typeof(KitchenPanelRegistry)
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(registry, panel);
    }

    static GameObject CreatePanel(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        return go;
    }
}
