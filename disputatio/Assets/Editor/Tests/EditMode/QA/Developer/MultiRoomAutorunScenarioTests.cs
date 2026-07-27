#if UNITY_EDITOR || DEVELOPMENT_BUILD
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Wave 1 Task 4: Kitchen and MainMenu capability autorun scenarios load from Resources.
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
}
#endif
