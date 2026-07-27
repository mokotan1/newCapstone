#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 3: BedRoom book DeveloperQa capability registration.
/// </summary>
public class BedRoomQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "bedroom.book.click",
        "bedroom.book.probe",
        "bedroom.book.assert-controller",
        "bedroom.book.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllBookIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BedRoomQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownBedRoomCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BedRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "bedroom.book.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutBedRoomScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BedRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "bedroom.book.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertController_WhenControllerNotFound_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BedRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "assertion", "invoke", "bedroom.book.assert-controller"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
    }
}
#endif
