#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;
using Task = System.Threading.Tasks.Task;

/// <summary>
/// Async capability dispatch: Kitchen Faucet Say(waitForClick) needs frame yields
/// before OnBlockEnd can set FaucetClicked.
/// </summary>
[TestFixture]
public sealed class DeveloperQaAsyncCapabilityTests
{
    [Test]
    public async Task ExecuteAsync_RegisterAsyncHandler_IsAwaited()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        registry.RegisterAsync(
            new DeveloperQaCapability(
                "test.async.wait",
                "TestScene",
                DeveloperQaCapabilityKind.Interaction,
                "{}",
                "{awaited:bool}"),
            async (_, cancellationToken) =>
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "async handler completed",
                    data: new Dictionary<string, string> { ["awaited"] = "True" });
            });

        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "test.async.wait"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        Assert.AreEqual("True", result.Data["awaited"]);
    }

    [Test]
    public void FungusSayPump_TryAdvance_WhenNoActiveDialog_ReturnsZero()
    {
        int advanced = DeveloperQaFungusSayPump.TryAdvanceActiveWriters();
        Assert.AreEqual(0, advanced);
    }

    [Test]
    public void KitchenFaucetClick_IsRegisteredAsAsyncHandler()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);

        Assert.IsTrue(
            registry.TryGetAsyncHandler(KitchenQaAdapter.FaucetClickCapabilityId, out _),
            "kitchen.faucet.click must be async so Say(waitForClick) can finish.");
        Assert.IsTrue(
            registry.TryGetAsyncHandler(KitchenQaAdapter.FaucetAssertClickedCapabilityId, out _),
            "kitchen.faucet.assert-clicked must wait+pump until FaucetClicked.");
    }
}
#endif
