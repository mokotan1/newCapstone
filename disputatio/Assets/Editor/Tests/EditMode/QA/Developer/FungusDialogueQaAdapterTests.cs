#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Fungus Say/Menu QA hops are registered and refuse Edit Mode fake Ok.
/// </summary>
public class FungusDialogueQaAdapterTests
{
    [Test]
    public void RegisterCapabilities_ListsProbeAdvanceChoose()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        FungusDialogueQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.AreEquivalent(
            new[]
            {
                FungusDialogueQaAdapter.ProbeCapabilityId,
                FungusDialogueQaAdapter.AdvanceCapabilityId,
                FungusDialogueQaAdapter.ChooseCapabilityId
            },
            ids);
    }

    [Test]
    public void MapProbe_OutsidePlayMode_IsEnvironmentBlocked()
    {
        DeveloperQaResult result = FungusDialogueQaAdapter.MapProbe();
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        StringAssert.Contains("Play Mode", result.Message);
    }

    [Test]
    public void MapAdvance_OutsidePlayMode_IsEnvironmentBlocked()
    {
        DeveloperQaResult result = FungusDialogueQaAdapter.MapAdvance();
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        StringAssert.Contains("Play Mode", result.Message);
    }

    [Test]
    public void MapChoose_OutsidePlayMode_IsEnvironmentBlocked()
    {
        DeveloperQaResult result = FungusDialogueQaAdapter.MapChoose();
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        StringAssert.Contains("Play Mode", result.Message);
    }
}
#endif
