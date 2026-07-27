#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 3: WifeRoom wallclock DeveloperQa capability registration.
/// </summary>
public class WifeRoomQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "wiferoom.wallclock.click",
        "wiferoom.wallclock.probe",
        "wiferoom.wallclock.assert-controller",
        "wiferoom.wallclock.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllWallclockIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        WifeRoomQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownWifeRoomCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        WifeRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "wiferoom.wallclock.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutWifeRoomScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        WifeRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "wiferoom.wallclock.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertController_WhenControllerNotFound_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        WifeRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "assertion", "invoke", "wiferoom.wallclock.assert-controller"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
    }
}
#endif
