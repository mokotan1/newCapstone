#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using Godlotto.QA.EditorCli;
using Godlotto.QA.SceneAdapters;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

/// <summary>
/// Task 8: CLI bridge and panel bridge must accept the same DeveloperQaCommand payloads
/// and yield the same <see cref="DeveloperQaResultCode"/> (and MissingCapabilityId when unknown).
/// </summary>
[TestFixture]
public sealed class DeveloperQaCliPanelParityTests
{
    private IDeveloperQaService sharedService;

    [SetUp]
    public void SetUp()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(true);

        DeveloperQaPanelBridge.ResetForTests();
        DeveloperQaCliBridge.ResetForTests();

        sharedService = DeveloperQaServiceFactory.Create();
        DeveloperQaPanelBridge.Configure(sharedService);
        DeveloperQaCliBridge.Configure(sharedService);
    }

    [TearDown]
    public void TearDown()
    {
        DeveloperQaCliBridge.ResetForTests();
        DeveloperQaPanelBridge.ResetForTests();
        DeveloperModeController.ResetTestOverrides();
        sharedService = null;
    }

    [Test]
    public void Factory_RegistersStudyRoomCapabilities()
    {
        var ids = sharedService.ListCapabilities();
        CollectionAssert.Contains(
            System.Linq.Enumerable.Select(ids, c => c.Id),
            StudyRoomQaAdapter.GrantBookmarkCapabilityId);
        CollectionAssert.Contains(
            System.Linq.Enumerable.Select(ids, c => c.Id),
            StudyRoomQaAdapter.ProbeCapabilityId);
    }

    [Test]
    public void BuildCommandForCli_Grant_MatchesPanelPayload()
    {
        DeveloperQaCommand panel = DeveloperQaPanelBridge.BuildGrantBookmarkCommand("parity-grant");
        DeveloperQaCommand cli = DeveloperQaCliBridge.BuildCommandForCli(new JObject
        {
            ["command_id"] = "parity-grant",
            ["family"] = DeveloperQaPanelBridge.FamilyInteraction,
            ["name"] = DeveloperQaPanelBridge.NameInvoke,
            ["target"] = StudyRoomQaAdapter.GrantBookmarkCapabilityId
        });

        AssertCommandsEquivalent(panel, cli);
    }

    [Test]
    public void BuildCommandForCli_Reset_MatchesPanelPayload()
    {
        DeveloperQaCommand panel = DeveloperQaPanelBridge.BuildResetCommand("parity-reset");
        DeveloperQaCommand cli = DeveloperQaCliBridge.BuildCommandForCli(new JObject
        {
            ["command_id"] = "parity-reset",
            ["family"] = DeveloperQaPanelBridge.FamilyInteraction,
            ["name"] = DeveloperQaPanelBridge.NameInvoke,
            ["target"] = StudyRoomQaAdapter.ResetCapabilityId
        });

        AssertCommandsEquivalent(panel, cli);
    }

    [Test]
    public void BuildCommandForCli_Probe_MatchesPanelPayload()
    {
        DeveloperQaCommand panel = DeveloperQaPanelBridge.BuildProbeCommand("parity-probe");
        DeveloperQaCommand cli = DeveloperQaCliBridge.BuildCommandForCli(new JObject
        {
            ["command_id"] = "parity-probe",
            ["family"] = DeveloperQaPanelBridge.FamilyState,
            ["name"] = DeveloperQaPanelBridge.NameCapture,
            ["target"] = StudyRoomQaAdapter.ProbeCapabilityId
        });

        AssertCommandsEquivalent(panel, cli);
    }

    [Test]
    public async Task ExecuteAsync_Grant_CliAndPanel_EqualResultCode()
    {
        await AssertParityAsync(
            DeveloperQaPanelBridge.BuildGrantBookmarkCommand("same-grant"),
            new JObject
            {
                ["command_id"] = "same-grant",
                ["family"] = "interaction",
                ["name"] = "invoke",
                ["target"] = StudyRoomQaAdapter.GrantBookmarkCapabilityId
            });
    }

    [Test]
    public async Task ExecuteAsync_Reset_CliAndPanel_EqualResultCode()
    {
        await AssertParityAsync(
            DeveloperQaPanelBridge.BuildResetCommand("same-reset"),
            new JObject
            {
                ["command_id"] = "same-reset",
                ["family"] = "interaction",
                ["name"] = "invoke",
                ["target"] = StudyRoomQaAdapter.ResetCapabilityId
            });
    }

    [Test]
    public async Task ExecuteAsync_Probe_CliAndPanel_EqualResultCode()
    {
        await AssertParityAsync(
            DeveloperQaPanelBridge.BuildProbeCommand("same-probe"),
            new JObject
            {
                ["command_id"] = "same-probe",
                ["family"] = "state",
                ["name"] = "capture",
                ["target"] = StudyRoomQaAdapter.ProbeCapabilityId
            });
    }

    [Test]
    public async Task ExecuteAsync_UnknownCapability_CliAndPanel_EqualMissingCapabilityId()
    {
        const string unknownId = "studyroom.mirror.does-not-exist";
        var cliParams = new JObject
        {
            ["command_id"] = "missing-cap-1",
            ["family"] = "interaction",
            ["name"] = "invoke",
            ["target"] = unknownId
        };

        DeveloperQaCommand panelCommand = DeveloperQaCommand.Create(
            "missing-cap-1",
            "interaction",
            "invoke",
            unknownId);
        DeveloperQaCommand cliCommand = DeveloperQaCliBridge.BuildCommandForCli(cliParams);

        DeveloperQaResult panelResult = await DeveloperQaPanelBridge.ExecuteAsync(
            panelCommand,
            CancellationToken.None);
        DeveloperQaResult cliResult = await DeveloperQaCliBridge.ExecuteAsync(
            cliCommand,
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, panelResult.Code);
        Assert.AreEqual(panelResult.Code, cliResult.Code);
        Assert.AreEqual(unknownId, panelResult.MissingCapabilityId);
        Assert.AreEqual(panelResult.MissingCapabilityId, cliResult.MissingCapabilityId);
    }

    [Test]
    public void CreateProductionService_InjectsEvidenceRecorder_EvidenceCaptureNotEnvironmentBlocked()
    {
        string tempRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "DeveloperQaCliParity_" + System.Guid.NewGuid().ToString("N"));

        try
        {
            IDeveloperQaService production = DeveloperQaCliBridge.CreateProductionService(
                runsRootDirectoryOverride: tempRoot);

            DeveloperQaResult result = production.ExecuteAsync(
                    DeveloperQaCommand.Create(
                        "ev-prod-1",
                        "evidence",
                        "capture",
                        parameters: new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["run_id"] = "dddddddddddddddddddddddddddddddd"
                        }),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.AreNotEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code, result.Message);
            Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        }
        finally
        {
            try
            {
                if (System.IO.Directory.Exists(tempRoot))
                {
                    System.IO.Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch (System.IO.IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    private static async Task AssertParityAsync(DeveloperQaCommand panelCommand, JObject cliParams)
    {
        DeveloperQaCommand cliCommand = DeveloperQaCliBridge.BuildCommandForCli(cliParams);
        AssertCommandsEquivalent(panelCommand, cliCommand);

        DeveloperQaResult panelResult = await DeveloperQaPanelBridge.ExecuteAsync(
            panelCommand,
            CancellationToken.None);
        DeveloperQaResult cliResult = await DeveloperQaCliBridge.ExecuteAsync(
            cliCommand,
            CancellationToken.None);

        Assert.AreEqual(panelResult.Code, cliResult.Code, panelResult.Message + " | " + cliResult.Message);
    }

    private static void AssertCommandsEquivalent(DeveloperQaCommand a, DeveloperQaCommand b)
    {
        Assert.IsNotNull(a);
        Assert.IsNotNull(b);
        Assert.AreEqual(a.Id, b.Id);
        Assert.AreEqual(a.Family, b.Family);
        Assert.AreEqual(a.Name, b.Name);
        Assert.AreEqual(a.TargetId, b.TargetId);
    }
}
#endif
