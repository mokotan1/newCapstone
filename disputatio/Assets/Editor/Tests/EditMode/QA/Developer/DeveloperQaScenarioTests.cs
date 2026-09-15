#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Developer;
using Godlotto.QA.Profile;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Task 9: studyroom-mirror-diary scenario JSON validates/loads, and
/// scenario.status reports meaningful state after run start and cancel.
/// </summary>
[TestFixture]
public class DeveloperQaScenarioTests
{
    private const string ScenarioId = "studyroom-mirror-diary";

    [Test]
    public void Validate_StudyRoomMirrorDiaryJson_FromResourcesPath_Succeeds()
    {
        string path = LocateScenarioJsonPath(ScenarioId);
        Assert.IsTrue(File.Exists(path), "Expected scenario JSON at " + path);

        string json = File.ReadAllText(path);
        var validator = new DeveloperQaScenarioValidator();
        DeveloperQaScenarioValidationResult result = validator.Validate(json);

        Assert.IsTrue(result.IsValid, string.Join("; ", result.Errors));
        Assert.IsNotNull(result.Scenario);
        Assert.AreEqual(ScenarioId, result.Scenario.Id);
        Assert.AreEqual("StudyRoom", result.Scenario.Scene);
        Assert.IsNotNull(result.Scenario.Steps);
        Assert.GreaterOrEqual(result.Scenario.Steps.Count, 8);

        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.preset.before-placement");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.grant-bookmark");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.place-bookmark");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.probe");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.capture");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.assert-solved");
        AssertContainsStepTarget(result.Scenario, "studyroom.mirror.reset");
        AssertContainsFamilyName(result.Scenario, "evidence", "capture");
    }

    [Test]
    public void Validate_UnknownFamily_Fails()
    {
        var validator = new DeveloperQaScenarioValidator();
        string json =
            "{"
            + "\"schemaVersion\":1,"
            + "\"id\":\"bad\","
            + "\"scene\":\"StudyRoom\","
            + "\"steps\":[{"
            + "\"id\":\"s1\",\"family\":\"not-a-family\",\"name\":\"x\",\"targetId\":\"t\""
            + "}]}";

        DeveloperQaScenarioValidationResult result = validator.Validate(json);

        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Errors);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioStatus_AfterRunStart_ReportsMeaningfulState()
    {
        var profile = new FakeQaProfileService();
        var registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapterRegister(registry);
        var service = new DeveloperQaService(registry, profile);

        DeveloperQaResult run = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "run-status-1",
                "scenario",
                "run",
                parameters: new Dictionary<string, string>
                {
                    ["scenario_id"] = ScenarioId,
                    ["scenario_path"] = LocateScenarioJsonPath(ScenarioId),
                    // EditMode has no StudyRoom Play Mode objects; defer steps so
                    // status reflects a started session rather than an immediate fail.
                    ["execute"] = "false"
                }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, run.Code, run.Message);
        Assert.AreEqual(1, profile.BeginCallCount);

        DeveloperQaResult status = await service.ExecuteAsync(
            DeveloperQaCommand.Create("status-1", "scenario", "status"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, status.Code, status.Message);
        Assert.IsNotNull(status.Data);
        Assert.IsTrue(status.Data.ContainsKey("state"), "status must include state");
        Assert.IsTrue(status.Data.ContainsKey("scenario_id"), "status must include scenario_id");
        Assert.AreEqual(ScenarioId, status.Data["scenario_id"]);
        Assert.AreEqual(DeveloperQaScenarioStates.Running, status.Data["state"]);
        Assert.IsTrue(status.Data.ContainsKey("step_index"));
        Assert.IsTrue(status.Data.ContainsKey("step_count"));
    }

    [Test]
    public async Task ExecuteAsync_ScenarioStatus_AfterCancel_ReportsCancelled()
    {
        var profile = new FakeQaProfileService();
        var registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapterRegister(registry);
        var service = new DeveloperQaService(registry, profile);

        DeveloperQaResult run = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "run-cancel-1",
                "scenario",
                "run",
                parameters: new Dictionary<string, string>
                {
                    ["scenario_id"] = ScenarioId,
                    ["scenario_path"] = LocateScenarioJsonPath(ScenarioId),
                    // Defer step execution so cancel can land while session is running.
                    ["execute"] = "false"
                }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, run.Code, run.Message);

        DeveloperQaResult running = await service.ExecuteAsync(
            DeveloperQaCommand.Create("status-before-cancel", "scenario", "status"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, running.Code, running.Message);
        Assert.AreEqual(DeveloperQaScenarioStates.Running, running.Data["state"]);

        DeveloperQaResult cancel = await service.ExecuteAsync(
            DeveloperQaCommand.Create("cancel-1", "scenario", "cancel"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, cancel.Code, cancel.Message);
        Assert.AreEqual(1, profile.RestoreCallCount);

        DeveloperQaResult status = await service.ExecuteAsync(
            DeveloperQaCommand.Create("status-after-cancel", "scenario", "status"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, status.Code, status.Message);
        Assert.AreEqual(DeveloperQaScenarioStates.Cancelled, status.Data["state"]);
        Assert.AreEqual(ScenarioId, status.Data["scenario_id"]);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioResume_AfterDeferredRun_AdvancesOrReportsState()
    {
        var profile = new FakeQaProfileService();
        var registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapterRegister(registry);
        var service = new DeveloperQaService(registry, profile);

        await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "run-resume-1",
                "scenario",
                "run",
                parameters: new Dictionary<string, string>
                {
                    ["scenario_id"] = ScenarioId,
                    ["scenario_path"] = LocateScenarioJsonPath(ScenarioId),
                    ["execute"] = "false"
                }),
            CancellationToken.None);

        DeveloperQaResult resume = await service.ExecuteAsync(
            DeveloperQaCommand.Create("resume-1", "scenario", "resume"),
            CancellationToken.None);

        // Without Play Mode StudyRoom objects, first capability step should fail
        // EnvironmentBlocked — still a meaningful, non-Unsupported result.
        Assert.AreNotEqual(DeveloperQaResultCode.UnsupportedCommand, resume.Code, resume.Message);

        DeveloperQaResult status = await service.ExecuteAsync(
            DeveloperQaCommand.Create("status-after-resume", "scenario", "status"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, status.Code, status.Message);
        Assert.IsTrue(status.Data.ContainsKey("state"));
        Assert.IsTrue(status.Data.ContainsKey("step_index"));
    }

    [Test]
    public async Task ExecuteAsync_ScenarioRun_WhenExecutionFails_RestoresQaProfile()
    {
        var profile = new FakeQaProfileService();
        var registry = new DeveloperQaCapabilityRegistry();
        StudyRoomQaAdapterRegister(registry);
        var service = new DeveloperQaService(registry, profile);

        DeveloperQaResult run = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "run-failure-restores-profile",
                "scenario",
                "run",
                parameters: new Dictionary<string, string>
                {
                    ["scenario_id"] = ScenarioId,
                    ["scenario_path"] = LocateScenarioJsonPath(ScenarioId)
                }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, run.Code, run.Message);
        Assert.AreEqual(DeveloperQaScenarioStates.Failed, run.Data["state"]);
        Assert.AreEqual(1, profile.RestoreCallCount);
        Assert.IsFalse(profile.IsQaProfileActive);
    }

    private static void StudyRoomQaAdapterRegister(DeveloperQaCapabilityRegistry registry)
    {
        Godlotto.QA.SceneAdapters.StudyRoomQaAdapter.RegisterCapabilities(registry);
    }

    private static void AssertContainsStepTarget(
        DeveloperQaScenarioDefinition scenario,
        string targetId)
    {
        for (int i = 0; i < scenario.Steps.Count; i++)
        {
            if (scenario.Steps[i] != null
                && string.Equals(scenario.Steps[i].TargetId, targetId, System.StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail("Scenario missing step targetId '" + targetId + "'.");
    }

    private static void AssertContainsFamilyName(
        DeveloperQaScenarioDefinition scenario,
        string family,
        string name)
    {
        for (int i = 0; i < scenario.Steps.Count; i++)
        {
            DeveloperQaScenarioStepDefinition step = scenario.Steps[i];
            if (step != null
                && string.Equals(step.Family, family, System.StringComparison.Ordinal)
                && string.Equals(step.Name, name, System.StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail("Scenario missing step " + family + "." + name + ".");
    }

    private static string LocateScenarioJsonPath(string scenarioId)
    {
        string fileName = scenarioId + ".json";
        string fromDataPath = Path.Combine(
            Application.dataPath,
            "Resources",
            "QA",
            "Scenarios",
            fileName);
        if (File.Exists(fromDataPath))
        {
            return fromDataPath;
        }

        string cwd = Directory.GetCurrentDirectory();
        string[] relatives =
        {
            Path.Combine("disputatio", "Assets", "Resources", "QA", "Scenarios", fileName),
            Path.Combine("Assets", "Resources", "QA", "Scenarios", fileName)
        };

        for (int i = 0; i < relatives.Length; i++)
        {
            string candidate = Path.GetFullPath(Path.Combine(cwd, relatives[i]));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return fromDataPath;
    }

    private sealed class FakeQaProfileService : IQaProfileService
    {
        public int BeginCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public QaRunId LastRunId { get; private set; } = QaRunId.None;

        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            BeginCallCount++;
            LastRunId = runId;
            IsQaProfileActive = true;
            return QaProfileOperationResult.Success("QA profile started.");
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.NotActive("not used");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            RestoreCallCount++;
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("Normal gameplay progress restored.");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("not used");
        }
    }
}
#endif
