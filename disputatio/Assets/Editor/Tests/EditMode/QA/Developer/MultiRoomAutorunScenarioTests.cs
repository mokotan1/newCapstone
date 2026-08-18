#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Wave 1/2/3: multi-room capability autorun scenarios load from Resources.
/// </summary>
[TestFixture]
public sealed class MultiRoomAutorunScenarioTests
{
    [Test]
    public void KitchenFaucetAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/kitchen-faucet-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("kitchen.faucet.click"));
    }

    [Test]
    public void MainMenuStartAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/mainmenu-start-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("mainmenu.start.click"));
    }

    [Test]
    public void MaidRoomFoodAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/maidroom-food-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("maidroom.food.click-tray"));
    }

    [Test]
    public void HallNavAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/hall-nav-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("hall.nav.click-kitchen-entry"));
    }

    [Test]
    public void ChildRoomSealsAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/childroom-seals-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("childroom.seals.click-seal5"));
    }

    [Test]
    public void WifeRoomWallclockAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/wiferoom-wallclock-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("wiferoom.wallclock.click"));
    }

    [Test]
    public void BedRoomBookAutorun_Json_LoadsAndHasClickStep()
    {
        TextAsset json = Resources.Load<TextAsset>("QA/Scenarios/bedroom-book-autorun");
        Assert.IsNotNull(json);
        Assert.IsTrue(json.text.Contains("bedroom.book.click"));
    }
}
#endif
