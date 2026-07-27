#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 1 Task 1: Kitchen faucet DeveloperQa capability registration.
/// </summary>
public class KitchenQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        "kitchen.faucet.preset.before-faucet",
        "kitchen.faucet.click",
        "kitchen.faucet.probe",
        "kitchen.faucet.assert-clicked",
        "kitchen.faucet.capture",
        "kitchen.faucet.reset"
    };

    [Test]
    public void RegisterCapabilities_ListsAllFaucetIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownKitchenCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "kitchen.faucet.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutKitchenScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "kitchen.faucet.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }
}
#endif
