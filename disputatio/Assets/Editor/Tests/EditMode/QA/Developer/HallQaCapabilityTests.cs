#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 2: Hall left-nav (kitchen wing) DeveloperQa capability registration.
/// </summary>
public class HallQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "hall.nav.click-kitchen-entry",
        "hall.nav.probe",
        "hall.nav.assert-route",
        "hall.nav.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllNavIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        HallQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownHallCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        HallQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "hall.nav.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutHallScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        HallQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "hall.nav.click-kitchen-entry"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertRoute_WhenControllerNotFound_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        HallQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "hall.nav.assert-route"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
    }
}
#endif
