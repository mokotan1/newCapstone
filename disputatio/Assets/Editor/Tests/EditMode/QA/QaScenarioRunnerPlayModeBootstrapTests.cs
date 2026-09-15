#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenarios;
using Godlotto.QA.Scenes;
using NUnit.Framework;

/// <summary>
/// P0: <see cref="QaScenarioRunner"/> must bootstrap Play Mode / scenario.scene before presets.
/// </summary>
[TestFixture]
public sealed class QaScenarioRunnerPlayModeBootstrapTests
{
    private const string SceneName = "Kitchen";
    private const string TargetId = "kitchen.sink.faucet";
    private const string PresetId = "before-faucet";

    private static int sharedSequence;

    [SetUp]
    public void SetUp()
    {
        sharedSequence = 0;
    }

    [Test]
    public void RunAsync_CallsBootstrapBeforeApplyingPreset()
    {
        var adapter = new RecordingPresetAdapter(SceneName, new[] { TargetId }, new[] { PresetId });
        var registry = new QaSceneRegistry();
        registry.Register(adapter);
        var bootstrap = new RecordingBootstrap(QaPlayModeBootstrapResult.Success(enteredPlayMode: true));

        var runner = new QaScenarioRunner(
            new QaDriverCore(),
            registry,
            new FakeProfileService(),
            new QaLeaseService(),
            new FakeInputDriver(),
            new FakeEvidenceRecorder(),
            () => QaDriverSnapshot.Create(inputGateLocked: false),
            playModeSceneBootstrap: bootstrap);

        QaScenarioRunOutcome outcome = runner.RunAsync(BuildPresetScenario()).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        Assert.AreEqual(1, bootstrap.EnsureCallCount);
        Assert.AreEqual(SceneName, bootstrap.LastSceneName);
        Assert.AreEqual(1, adapter.ApplyPresetCallCount);
        Assert.Less(bootstrap.EnsureSequence, adapter.ApplyPresetSequence);
        Assert.AreEqual(1, bootstrap.RestoreCallCount);
    }

    [Test]
    public void RunAsync_BootstrapBlocked_DoesNotApplyPreset_AndRestores()
    {
        var adapter = new RecordingPresetAdapter(SceneName, new[] { TargetId }, new[] { PresetId });
        var registry = new QaSceneRegistry();
        registry.Register(adapter);
        var bootstrap = new RecordingBootstrap(
            QaPlayModeBootstrapResult.Blocked(
                "BLOCKED: timed out loading scene 'Kitchen' into Play Mode.",
                enteredPlayMode: true));

        var runner = new QaScenarioRunner(
            new QaDriverCore(),
            registry,
            new FakeProfileService(),
            new QaLeaseService(),
            new FakeInputDriver(),
            new FakeEvidenceRecorder(),
            () => QaDriverSnapshot.Create(inputGateLocked: false),
            playModeSceneBootstrap: bootstrap);

        QaScenarioRunOutcome outcome = runner.RunAsync(BuildPresetScenario()).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Blocked, outcome.Code, outcome.Message);
        Assert.IsTrue(outcome.Message.IndexOf("BLOCKED", StringComparison.Ordinal) >= 0, outcome.Message);
        Assert.AreEqual(0, adapter.ApplyPresetCallCount);
        Assert.AreEqual(1, bootstrap.RestoreCallCount);
    }

    private static QaScenarioDefinition BuildPresetScenario()
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.bootstrap-order",
            Scene = SceneName,
            Preset = PresetId,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "assert-unlocked",
                    Command = QaScenarioSchema.CommandStateAssert,
                    TimeoutMs = 1000,
                    Assertion = new QaScenarioAssertionDefinition { Kind = "inputUnlocked" }
                }
            }
        };
    }

    private sealed class RecordingBootstrap : IQaPlayModeSceneBootstrap
    {
        private readonly QaPlayModeBootstrapResult result;

        public RecordingBootstrap(QaPlayModeBootstrapResult result)
        {
            this.result = result;
        }

        public int EnsureCallCount { get; private set; }

        public int RestoreCallCount { get; private set; }

        public int EnsureSequence { get; private set; }

        public string LastSceneName { get; private set; }

        public Task<QaPlayModeBootstrapResult> EnsureReadyAsync(
            string sceneName,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            EnsureCallCount++;
            EnsureSequence = ++sharedSequence;
            LastSceneName = sceneName;
            return Task.FromResult(result);
        }

        public void RestoreIfOwned()
        {
            RestoreCallCount++;
        }
    }

    private sealed class RecordingPresetAdapter : IQaSceneAdapter
    {
        public RecordingPresetAdapter(
            string sceneName,
            IReadOnlyCollection<string> targetIds,
            IReadOnlyCollection<string> presetIds)
        {
            SceneName = sceneName;
            var targets = new List<QaTargetId>();
            foreach (string id in targetIds)
            {
                if (QaTargetId.TryCreate(id, out QaTargetId parsed, out _))
                {
                    targets.Add(parsed);
                }
            }

            TargetIds = targets;
            PresetIds = presetIds;
        }

        public string SceneName { get; }

        public IReadOnlyCollection<QaTargetId> TargetIds { get; }

        public IReadOnlyCollection<string> PresetIds { get; }

        public int ApplyPresetCallCount { get; private set; }

        public int ApplyPresetSequence { get; private set; }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            ApplyPresetCallCount++;
            ApplyPresetSequence = ++sharedSequence;
            return QaScenePresetResult.Success("applied " + presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow);
        }
    }

    private sealed class FakeProfileService : IQaProfileService
    {
        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            IsQaProfileActive = true;
            return QaProfileOperationResult.Success("begun");
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.Success("reset");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("restored");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("none");
        }
    }

    private sealed class FakeInputDriver : IQaInputDriver
    {
        public QaInteractionMode Mode
        {
            get { return QaInteractionMode.Api; }
        }

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(targetId, Mode, "ok"));
        }

        public Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId,
            QaTargetId destinationTargetId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(sourceTargetId, Mode, "ok"));
        }

        public Task<QaInputResult> KeyAsync(
            QaTargetId targetId,
            string text,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(targetId, Mode, "ok"));
        }
    }

    private sealed class FakeEvidenceRecorder : IQaEvidenceRecorder
    {
        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            return QaEvidenceOperationResult.Success("ok");
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            return QaEvidenceFinalizeResult.Success(
                QaRunManifest.Create(
                    "fake",
                    "fake-dir",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    Array.Empty<QaEvidenceEvent>(),
                    "events.jsonl",
                    "console.log",
                    "screenshots",
                    "report.md"),
                "fake-dir");
        }
    }
}
#endif
