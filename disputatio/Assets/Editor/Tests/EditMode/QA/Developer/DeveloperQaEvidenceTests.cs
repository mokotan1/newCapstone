#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Developer;
using Godlotto.QA.Evidence;
using Godlotto.QA.Profile;
using NUnit.Framework;

/// <summary>
/// Task 7: <c>evidence.capture</c> / <c>scenario.run</c> must materialize an immutable run
/// directory under an injected runs root (temp in tests; <c>docs/qa/runs</c> in Editor).
/// </summary>
[TestFixture]
public class DeveloperQaEvidenceTests
{
    private string tempRoot;
    private FakeClock clock;
    private DevelopmentQaEvidenceRecorder recorder;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "DeveloperQaEvidenceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        clock = new FakeClock(new DateTime(2026, 7, 27, 5, 0, 0, DateTimeKind.Utc));
        recorder = new DevelopmentQaEvidenceRecorder(tempRoot, clock.UtcNow);
    }

    [TearDown]
    public void TearDown()
    {
        recorder = null;
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Test]
    public async Task ExecuteAsync_EvidenceCapture_CreatesRunDirectoryWithExpectedLayout()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), null, recorder);
        const string runId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "ev-1",
                "evidence",
                "capture",
                parameters: new Dictionary<string, string> { ["run_id"] = runId }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        Assert.IsNotNull(recorder.RunDirectoryPath);
        Assert.AreEqual("20260727T050000Z-run-" + runId, Path.GetFileName(recorder.RunDirectoryPath));
        AssertExpectedRunLayout(recorder.RunDirectoryPath);
        Assert.IsTrue(result.Data.ContainsKey("run_directory"));
        Assert.AreEqual(recorder.RunDirectoryPath, result.Data["run_directory"]);
    }

    [Test]
    public async Task ExecuteAsync_ScenarioRun_BeginsEvidenceRunDirectory()
    {
        var profile = new FakeQaProfileService();
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), profile, recorder);
        const string runId = "cccccccccccccccccccccccccccccccc";

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "run-1",
                "scenario",
                "run",
                parameters: new Dictionary<string, string> { ["run_id"] = runId }),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        Assert.AreEqual(1, profile.BeginCallCount);
        Assert.IsNotNull(recorder.RunDirectoryPath);
        Assert.AreEqual("20260727T050000Z-run-" + runId, Path.GetFileName(recorder.RunDirectoryPath));
        AssertExpectedRunLayout(recorder.RunDirectoryPath);
        Assert.IsTrue(result.Data.ContainsKey("run_directory"));
    }

    [Test]
    public async Task ExecuteAsync_EvidenceCapture_WhenRecorderNull_ReturnsEnvironmentBlocked()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), null, null);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("ev-2", "evidence", "capture"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        Assert.AreEqual("QA evidence recorder unavailable", result.Message);
    }

    [Test]
    public async Task ExecuteAsync_EvidenceCapture_DoesNotRequireCapabilityTarget()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry(), null, recorder);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("ev-3", "evidence", "capture"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
        Assert.AreNotEqual(DeveloperQaResultCode.MissingCapability, result.Code);
        AssertExpectedRunLayout(recorder.RunDirectoryPath);
    }

    private static void AssertExpectedRunLayout(string runDirectory)
    {
        Assert.IsTrue(Directory.Exists(runDirectory), "run directory missing");
        Assert.IsTrue(File.Exists(Path.Combine(runDirectory, "manifest.json")), "manifest.json");
        Assert.IsTrue(File.Exists(Path.Combine(runDirectory, "journal.jsonl")), "journal.jsonl");
        Assert.IsTrue(File.Exists(Path.Combine(runDirectory, "report.md")), "report.md");
        Assert.IsTrue(File.Exists(Path.Combine(runDirectory, "console.log")), "console.log");
        Assert.IsTrue(Directory.Exists(Path.Combine(runDirectory, "screenshots")), "screenshots/");
        Assert.IsTrue(Directory.Exists(Path.Combine(runDirectory, "patches")), "patches/");
    }

    private sealed class FakeClock
    {
        private DateTime utcNow;

        public FakeClock(DateTime startUtc)
        {
            utcNow = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        }

        public DateTime UtcNow()
        {
            return utcNow;
        }
    }

    private sealed class FakeQaProfileService : IQaProfileService
    {
        public int BeginCallCount { get; private set; }

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
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("restored");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("not used");
        }
    }
}
#endif
