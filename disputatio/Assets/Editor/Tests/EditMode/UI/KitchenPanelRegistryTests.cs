using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenPanelRegistryTests
{
    GameObject burner;
    GameObject fripan;
    GameObject parrot;
    KitchenPanelRegistry registry;

    [SetUp]
    public void SetUp()
    {
        registry = new GameObject("Registry").AddComponent<KitchenPanelRegistry>();
        burner = CreatePanel("burner");
        fripan = CreatePanel("firpan_Panel");
        parrot = CreatePanel("Parret");

        typeof(KitchenPanelRegistry)
            .GetField("burnerPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(registry, burner);
        typeof(KitchenPanelRegistry)
            .GetField("fripanPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(registry, fripan);
        typeof(KitchenPanelRegistry)
            .GetField("parrotPanel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(registry, parrot);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(registry.gameObject);
        Object.DestroyImmediate(burner);
        Object.DestroyImmediate(fripan);
        Object.DestroyImmediate(parrot);
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
    public void CloseAllPanels_DeactivatesRegisteredPanels()
    {
        burner.SetActive(true);
        fripan.SetActive(true);
        parrot.SetActive(true);

        registry.CloseAllPanels();

        Assert.IsFalse(burner.activeSelf);
        Assert.IsFalse(fripan.activeSelf);
        Assert.IsFalse(parrot.activeSelf);
    }

    static GameObject CreatePanel(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        return go;
    }
}
