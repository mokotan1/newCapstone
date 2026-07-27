#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Task 5: panel bridge must emit the same DeveloperQaCommand payloads the CLI will use,
/// and yield the same <see cref="DeveloperQaResultCode"/> as a direct service call.
/// </summary>
public class DeveloperQaPanelBridgeTests
{
    private DeveloperQaCapabilityRegistry registry;
    private DeveloperQaService service;

    [SetUp]
    public void SetUp()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(true);

        DeveloperQaPanelBridge.ResetForTests();
        registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapter.RegisterCapabilities(registry);
        service = new DeveloperQaService(registry);
        DeveloperQaPanelBridge.Configure(service);
    }

    [TearDown]
    public void TearDown()
    {
        DeveloperQaPanelBridge.ResetForTests();
        DeveloperModeController.ResetTestOverrides();
    }

    [Test]
    public void BuildGrantBookmarkCommand_MatchesCliInteractionInvokePayload()
    {
        DeveloperQaCommand command = DeveloperQaPanelBridge.BuildGrantBookmarkCommand("panel-grant-1");

        Assert.AreEqual("panel-grant-1", command.Id);
        Assert.AreEqual("interaction", command.Family);
        Assert.AreEqual("invoke", command.Name);
        Assert.AreEqual(StudyRoomQaAdapter.GrantBookmarkCapabilityId, command.TargetId);
    }

    [Test]
    public void BuildResetCommand_MatchesCliInteractionInvokePayload()
    {
        DeveloperQaCommand command = DeveloperQaPanelBridge.BuildResetCommand("panel-reset-1");

        Assert.AreEqual("panel-reset-1", command.Id);
        Assert.AreEqual("interaction", command.Family);
        Assert.AreEqual("invoke", command.Name);
        Assert.AreEqual(StudyRoomQaAdapter.ResetCapabilityId, command.TargetId);
    }

    [Test]
    public void BuildProbeCommand_MatchesCliStateCapturePayload()
    {
        DeveloperQaCommand command = DeveloperQaPanelBridge.BuildProbeCommand("panel-probe-1");

        Assert.AreEqual("panel-probe-1", command.Id);
        Assert.AreEqual("state", command.Family);
        Assert.AreEqual("capture", command.Name);
        Assert.AreEqual(StudyRoomQaAdapter.ProbeCapabilityId, command.TargetId);
    }

    [Test]
    public async Task ExecuteAsync_GrantBookmark_SamePayload_EqualsDirectServiceResultCode()
    {
        DeveloperQaCommand viaBridgeCommand =
            DeveloperQaPanelBridge.BuildGrantBookmarkCommand("same-grant");
        DeveloperQaCommand viaDirectCommand = DeveloperQaCommand.Create(
            "same-grant",
            "interaction",
            "invoke",
            StudyRoomQaAdapter.GrantBookmarkCapabilityId);

        DeveloperQaResult viaBridge = await DeveloperQaPanelBridge.ExecuteAsync(
            viaBridgeCommand,
            CancellationToken.None);
        DeveloperQaResult viaDirect = await service.ExecuteAsync(
            viaDirectCommand,
            CancellationToken.None);

        Assert.AreEqual(viaDirect.Code, viaBridge.Code);
    }

    [Test]
    public async Task ExecuteAsync_Reset_SamePayload_EqualsDirectServiceResultCode()
    {
        DeveloperQaCommand viaBridgeCommand =
            DeveloperQaPanelBridge.BuildResetCommand("same-reset");
        DeveloperQaCommand viaDirectCommand = DeveloperQaCommand.Create(
            "same-reset",
            "interaction",
            "invoke",
            StudyRoomQaAdapter.ResetCapabilityId);

        DeveloperQaResult viaBridge = await DeveloperQaPanelBridge.ExecuteAsync(
            viaBridgeCommand,
            CancellationToken.None);
        DeveloperQaResult viaDirect = await service.ExecuteAsync(
            viaDirectCommand,
            CancellationToken.None);

        Assert.AreEqual(viaDirect.Code, viaBridge.Code);
    }

    [Test]
    public async Task ExecuteAsync_Probe_SamePayload_EqualsDirectServiceResultCode()
    {
        DeveloperQaCommand viaBridgeCommand =
            DeveloperQaPanelBridge.BuildProbeCommand("same-probe");
        DeveloperQaCommand viaDirectCommand = DeveloperQaCommand.Create(
            "same-probe",
            "state",
            "capture",
            StudyRoomQaAdapter.ProbeCapabilityId);

        DeveloperQaResult viaBridge = await DeveloperQaPanelBridge.ExecuteAsync(
            viaBridgeCommand,
            CancellationToken.None);
        DeveloperQaResult viaDirect = await service.ExecuteAsync(
            viaDirectCommand,
            CancellationToken.None);

        Assert.AreEqual(viaDirect.Code, viaBridge.Code);
        Assert.AreEqual(DeveloperQaResultCode.Ok, viaBridge.Code);
    }

    [Test]
    public void TryGrantBookmark_WhenServiceUnconfigured_ReturnsFalseWithoutThrowing()
    {
        DeveloperQaPanelBridge.ResetForTests();
        DeveloperQaPanelBridge.DisableDefaultServiceCreationForTests = true;

        bool ok = DeveloperQaPanelBridge.TryGrantBookmark(out DeveloperQaResult result);

        Assert.IsFalse(ok);
        Assert.IsNull(result);
    }
}
#endif
