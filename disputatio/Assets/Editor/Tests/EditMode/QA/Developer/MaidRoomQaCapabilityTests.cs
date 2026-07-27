#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 2: MaidRoom food-tray DeveloperQa capability registration.
/// </summary>
public class MaidRoomQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "maidroom.food.click-tray",
        "maidroom.food.probe",
        "maidroom.food.assert-effect",
        "maidroom.food.capture"
    };

    [Test]
    public void RegisterCapabilities_ListsAllFoodIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MaidRoomQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownMaidRoomCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MaidRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "maidroom.food.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutMaidRoomScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        MaidRoomQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "maidroom.food.click-tray"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }
}
#endif
