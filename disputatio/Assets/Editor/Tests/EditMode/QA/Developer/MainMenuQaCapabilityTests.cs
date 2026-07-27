#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 1 Task 2: MainMenu start DeveloperQa capability registration.
/// </summary>
public class MainMenuQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "mainmenu.start.click",
        "mainmenu.start.probe",
        "mainmenu.start.assert-invoked",
        "mainmenu.start.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllStartIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MainMenuQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownMainMenuCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MainMenuQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "mainmenu.start.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutMainMenuScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MainMenuQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "mainmenu.start.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertInvoked_WhenMainMenuNotFound_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MainMenuQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "assertion", "invoke", "mainmenu.start.assert-invoked"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
    }
}
#endif
