#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// BasementHallway Sequence door QA capabilities (no Fungus execution).
/// </summary>
public class BasementHallwayQaCapabilityTests
{
    private static readonly string[] ExpectedIds =
    {
        BasementHallwayQaAdapter.ClickDoorBrickCapabilityId,
        BasementHallwayQaAdapter.ClickDoorExtractionCapabilityId,
        BasementHallwayQaAdapter.ClickDoorObservationCapabilityId,
        BasementHallwayQaAdapter.ClickDoorResearchCapabilityId,
        BasementHallwayQaAdapter.ClickEntryUpperCapabilityId,
        BasementHallwayQaAdapter.NavProbeCapabilityId,
        BasementHallwayQaAdapter.NavAssertRouteCapabilityId,
        BasementHallwayQaAdapter.NavCaptureCapabilityId,
        BasementHallwayQaAdapter.ResetToHallwayCapabilityId
    };

    [SetUp]
    public void SetUp()
    {
        BasementHallwayQaAdapter.ResetLastExpectedDestinationForTests();
    }

    [TearDown]
    public void TearDown()
    {
        BasementHallwayQaAdapter.ResetLastExpectedDestinationForTests();
    }

    [Test]
    public void RegisterCapabilities_ListsAllNavIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BasementHallwayQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedIds, ids);
    }

    [Test]
    public async Task Describe_UnknownCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BasementHallwayQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "basement-hallway.nav.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_Click_WithoutHallwayScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BasementHallwayQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c1",
                "interaction",
                "invoke",
                BasementHallwayQaAdapter.ClickDoorBrickCapabilityId),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_AssertRoute_WithoutPriorClick_ReturnsAssertionFailed()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        BasementHallwayQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c1",
                "interaction",
                "invoke",
                BasementHallwayQaAdapter.NavAssertRouteCapabilityId),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code);
        StringAssert.Contains(BasementHallwayQaRouteAssertion.ReasonNoExpectedDestination, result.Message);
    }

    [Test]
    public void DestinationForInteraction_MapsAllFiveDoors()
    {
        Assert.AreEqual(
            SceneNames.BasementBrickRoom,
            BasementHallwayQaAdapter.DestinationForInteraction(
                BasementHallwayQaAdapter.DoorBrickInteractionId));
        Assert.AreEqual(
            SceneNames.BasementExtractionRoom,
            BasementHallwayQaAdapter.DestinationForInteraction(
                BasementHallwayQaAdapter.DoorExtractionInteractionId));
        Assert.AreEqual(
            SceneNames.BasementObservationRoom,
            BasementHallwayQaAdapter.DestinationForInteraction(
                BasementHallwayQaAdapter.DoorObservationInteractionId));
        Assert.AreEqual(
            SceneNames.BasementResearchRoom,
            BasementHallwayQaAdapter.DestinationForInteraction(
                BasementHallwayQaAdapter.DoorResearchInteractionId));
        Assert.AreEqual(
            SceneNames.Basement,
            BasementHallwayQaAdapter.DestinationForInteraction(
                BasementHallwayQaAdapter.EntryUpperInteractionId));
    }
}
#endif
