#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Developer;
using Godlotto.QA.Profile;
using NUnit.Framework;

/// <summary>
/// DeveloperQaService session boundary: scenario.run begins an isolated QA profile;
/// cancel/abort restores the previous profile. Full scenario runner is Task 9.
/// </summary>
public class DeveloperQaProfileSessionTests
{
    [Test]
    public async Task ExecuteAsync_ScenarioRun_CallsBeginQaProfile()
    {
        var profile = new FakeQaProfileService();
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("run-cmd-1", "scenario", "run"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual(1, profile.BeginCallCount);
        Assert.IsFalse(profile.LastRunId.IsNone);
        Assert.AreEqual(0, profile.RestoreCallCount);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioCancel_CallsRestorePreviousProfile()
    {
        var profile = new FakeQaProfileService();
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile);

        await service.ExecuteAsync(
            DeveloperQaCommand.Create("run-cmd-1", "scenario", "run"),
            CancellationToken.None);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("cancel-cmd-1", "scenario", "cancel"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual(1, profile.RestoreCallCount);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioAbort_CallsRestorePreviousProfile()
    {
        var profile = new FakeQaProfileService();
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile);

        await service.ExecuteAsync(
            DeveloperQaCommand.Create("run-cmd-1", "scenario", "run"),
            CancellationToken.None);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("abort-cmd-1", "scenario", "abort"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual(1, profile.RestoreCallCount);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioRun_WhenAlreadyActive_ReturnsEnvironmentBlocked()
    {
        var profile = new FakeQaProfileService
        {
            BeginResult = QaProfileOperationResult.AlreadyActive(
                "A QA profile is already active. Call RestorePreviousProfile before beginning a new one.")
        };
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("run-cmd-2", "scenario", "run"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        Assert.IsTrue(
            result.Message.IndexOf("already active", System.StringComparison.OrdinalIgnoreCase) >= 0,
            result.Message);
        Assert.AreEqual(1, profile.BeginCallCount);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioRun_WhenProfileServiceNull_ReturnsEnvironmentBlocked()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), null);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("run-cmd-3", "scenario", "run"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        Assert.AreEqual("QA profile service unavailable", result.Message);
    }

    [Test]
    public async Task ExecuteAsync_ParameterlessCtor_ScenarioCancel_ReturnsEnvironmentBlocked()
    {
        var service = new DeveloperQaService();

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("cancel-cmd-2", "scenario", "cancel"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        Assert.AreEqual("QA profile service unavailable", result.Message);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioRun_UsesRunIdParameterAsQaRunId()
    {
        var profile = new FakeQaProfileService();
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile);
        const string runIdText = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "opaque-command-id",
                "scenario",
                "run",
                parameters: new Dictionary<string, string> { ["run_id"] = runIdText }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual(runIdText, profile.LastRunId.ToString());
    }

    private sealed class FakeQaProfileService : IQaProfileService
    {
        public int BeginCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public QaRunId LastRunId { get; private set; } = QaRunId.None;

        public QaProfileOperationResult BeginResult { get; set; } =
            QaProfileOperationResult.Success("QA profile started.");

        public QaProfileOperationResult RestoreResult { get; set; } =
            QaProfileOperationResult.Success("Normal gameplay progress restored.");

        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            BeginCallCount++;
            LastRunId = runId;
            if (BeginResult.IsSuccess)
            {
                IsQaProfileActive = true;
            }

            return BeginResult;
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.NotActive("not used");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            RestoreCallCount++;
            if (RestoreResult.IsSuccess)
            {
                IsQaProfileActive = false;
            }

            return RestoreResult;
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("not used");
        }
    }
}
#endif
