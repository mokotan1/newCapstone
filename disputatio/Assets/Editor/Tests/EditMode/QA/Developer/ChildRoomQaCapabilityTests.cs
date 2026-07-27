#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 3: ChildRoom seals DeveloperQa capability registration.
/// </summary>
public class ChildRoomQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "childroom.seals.click-seal5",
        "childroom.seals.probe",
        "childroom.seals.assert-controller",
        "childroom.seals.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllSealIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        ChildRoomQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownChildRoomCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        ChildRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "childroom.seals.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutChildRoomScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        ChildRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "childroom.seals.click-seal5"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertController_WhenControllerNotFound_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        ChildRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "childroom.seals.assert-controller"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
    }
}
#endif
