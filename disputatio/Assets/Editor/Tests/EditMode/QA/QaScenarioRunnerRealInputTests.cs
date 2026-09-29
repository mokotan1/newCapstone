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

[TestFixture]
public sealed class QaScenarioRunnerRealInputTests
{
    private const string SceneName = "Kitchen";
    private const string TargetId = "kitchen.sink.faucet";

    [Test]
    public void RunAsync_PointerWithoutRealInputDriver_FailsClosed()
    {
        var runner = BuildRunner(realInputDriver: null);
        var scenario = BuildPointerScenario();

        QaScenarioRunOutcome outcome = runner.RunAsync(scenario).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Failed, outcome.Code);
        StringAssert.Contains("RealInput", outcome.Message);
    }

    [Test]
    public void RunAsync_PointerWithRealInputDriver_RecordsRealInputEvidence()
    {
        var evidence = new RecordingEvidenceRecorder();
        var realInput = new RecordingInputDriver(QaInteractionMode.RealInput);
        var runner = BuildRunner(evidence, realInputDriver: realInput);
        var scenario = BuildPointerScenario();

        QaScenarioRunOutcome outcome = runner.RunAsync(scenario).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        Assert.AreEqual(1, realInput.ClickCount);
        Assert.IsTrue(evidence.HasCommandData("click-faucet", "executionDispatch", "real-input"));
        Assert.IsTrue(evidence.HasCommandData("click-faucet", "inputMode", "RealInput"));
    }

    [Test]
    public void RunAsync_InputUnlockedAssert_WithoutLiveObservation_Fails()
    {
        var probe = new QaStateProbe();
        var runner = BuildRunner(
            evidence: new RecordingEvidenceRecorder(),
            realInputDriver: null,
            capture: () => probe.Capture());
        var scenario = new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.assert-live",
            Scene = SceneName,
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

        QaScenarioRunOutcome outcome = runner.RunAsync(scenario).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Failed, outcome.Code);
        StringAssert.Contains("not observed", outcome.Message);
    }

    private static QaScenarioDefinition BuildPointerScenario()
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.pointer",
            Scene = SceneName,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "click-faucet",
                    Command = QaScenarioSchema.CommandInteractionPointer,
                    Target = TargetId,
                    TimeoutMs = 1000
                }
            }
        };
    }

    private static QaScenarioRunner BuildRunner(
        RecordingEvidenceRecorder evidence = null,
        IQaInputDriver realInputDriver = null,
        Func<QaDriverSnapshot> capture = null)
    {
        evidence = evidence ?? new RecordingEvidenceRecorder();
        var registry = new QaSceneRegistry();
        registry.Register(new FakeSceneAdapter(SceneName, new[] { TargetId }));
        return new QaScenarioRunner(
            new QaDriverCore(),
            registry,
            new FakeProfileService(),
            new QaLeaseService(),
            new FakeInputDriver(),
            evidence,
            capture ?? (() => QaDriverSnapshot.Create(inputGateLocked: false)),
            realInputDriver: realInputDriver);
    }

    private sealed class FakeSceneAdapter : IQaSceneAdapter
    {
        private readonly List<QaTargetId> targets;

        public FakeSceneAdapter(string sceneName, IEnumerable<string> rawTargetIds)
        {
            SceneName = sceneName;
            targets = new List<QaTargetId>();
            foreach (string raw in rawTargetIds)
            {
                if (QaTargetId.TryCreate(raw, out QaTargetId id, out _))
                {
                    targets.Add(id);
                }
            }
        }

        public string SceneName { get; }

        public IReadOnlyCollection<QaTargetId> TargetIds => targets;

        public IReadOnlyCollection<string> PresetIds => Array.Empty<string>();

        public QaScenePresetResult ApplyPreset(string presetId) => QaScenePresetResult.Success();

        public QaSceneSnapshot CaptureSnapshot() => QaSceneSnapshot.Create(SceneName, DateTime.UtcNow);
    }

    private sealed class FakeProfileService : IQaProfileService
    {
        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            IsQaProfileActive = true;
            return QaProfileOperationResult.Success("ok");
        }

        public QaProfileOperationResult ResetGameplay() => QaProfileOperationResult.Success("ok");

        public QaProfileOperationResult RestorePreviousProfile()
        {
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("ok");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
            => QaProfileOperationResult.NothingToRecover("ok");
    }

    private sealed class FakeInputDriver : IQaInputDriver
    {
        public QaInteractionMode Mode => QaInteractionMode.Api;

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
            => Task.FromResult(QaInputResult.Success(targetId, QaInteractionMode.Api));

        public Task<QaInputResult> DragAsync(QaTargetId source, QaTargetId dest, CancellationToken cancellationToken)
            => Task.FromResult(QaInputResult.Success(source, QaInteractionMode.Api));

        public Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
            => Task.FromResult(QaInputResult.Success(targetId, QaInteractionMode.Api));
    }

    private sealed class RecordingInputDriver : IQaInputDriver
    {
        public RecordingInputDriver(QaInteractionMode mode)
        {
            Mode = mode;
        }

        public QaInteractionMode Mode { get; }

        public int ClickCount { get; private set; }

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            ClickCount++;
            return Task.FromResult(QaInputResult.Success(targetId, Mode));
        }

        public Task<QaInputResult> DragAsync(QaTargetId source, QaTargetId dest, CancellationToken cancellationToken)
            => Task.FromResult(QaInputResult.Success(source, Mode));

        public Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
            => Task.FromResult(QaInputResult.Success(targetId, Mode));
    }

    private sealed class RecordingEvidenceRecorder : IQaEvidenceRecorder
    {
        private readonly List<QaEvidenceEvent> events = new List<QaEvidenceEvent>();

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
            => QaEvidenceOperationResult.Success("ok");

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            events.Add(evidenceEvent);
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
            => QaEvidenceOperationResult.Success();

        public QaEvidenceOperationResult RecordConsole(string logText)
            => QaEvidenceOperationResult.Success();

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
            => QaEvidenceFinalizeResult.Success(
                QaRunManifest.Create(
                    "fake",
                    "fake-dir",
                    DateTime.UtcNow,
                    DateTime.UtcNow,
                    events,
                    "events.jsonl",
                    "console.log",
                    "screenshots",
                    "report.md"),
                "fake-dir");

        public bool HasCommandData(string commandId, string key, string expected)
        {
            foreach (QaEvidenceEvent item in events)
            {
                if (!string.Equals(item.CommandId, commandId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.Data != null
                    && item.Data.TryGetValue(key, out string value)
                    && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
