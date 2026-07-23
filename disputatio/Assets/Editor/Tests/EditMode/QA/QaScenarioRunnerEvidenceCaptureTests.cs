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
/// Root-cause regression coverage for the QA manifest PASS bug: <c>QaScenarioRunner</c> used to
/// record assertion evidence but never attached any screenshot mid-run, so
/// <see cref="QaRunManifest.AggregateVerdict"/> (which requires at least one passed assertion
/// AND at least one <see cref="QaEvidenceEventType.ScreenshotAttached"/> event) could never reach
/// <see cref="QaRunVerdictCode.Pass"/> -- every successful run finalized as
/// <see cref="QaRunVerdictCode.Blocked"/> instead.
///
/// These EditMode tests exercise the real <see cref="QaScenarioRunner"/> (with a fake PNG
/// provider + fake evidence recorder) end-to-end and assert the fix directly against the real
/// <see cref="QaRunManifest"/> produced by <see cref="QaRunManifest.Create"/>. Every step here
/// resolves synchronously (an immediately-true <c>state.assert</c> never needs to await a real
/// frame via <c>QaConditionWaiter</c>'s frame-yield path -- see that type's remarks: it always
/// evaluates at least once before yielding), so this can run as a plain <c>[Test]</c> without a
/// PlayMode frame pump.
/// </summary>
[TestFixture]
public class QaScenarioRunnerEvidenceCaptureTests
{
    private const string SceneName = "Kitchen";
    private const string TargetId = "kitchen.sink.faucet";

    private static QaSceneRegistry BuildRegistry()
    {
        var registry = new QaSceneRegistry();
        registry.Register(new FakeSceneAdapter(SceneName, new[] { TargetId }, Array.Empty<string>()));
        return registry;
    }

    private static QaScenarioDefinition BuildAssertThenCaptureScenario()
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.evidence-capture-smoke",
            Scene = SceneName,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "assert-unlocked",
                    Command = QaScenarioSchema.CommandStateAssert,
                    TimeoutMs = 1000,
                    Assertion = new QaScenarioAssertionDefinition { Kind = "inputUnlocked" }
                },
                new QaScenarioStepDefinition
                {
                    Id = "capture",
                    Command = QaScenarioSchema.CommandEvidenceCapture,
                    TimeoutMs = 1000
                }
            }
        };
    }

    // ---------------------------------------------------------------
    //  Root-cause fix: a provider lets evidence.capture reach Pass
    // ---------------------------------------------------------------

    [Test]
    public void RunAsync_EvidenceCaptureStepWithProvider_AttachesScreenshotAndManifestAggregatesToPass()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe { InputGateLocked = false };
        byte[] fakePng = { 1, 2, 3, 4 };

        var runner = new QaScenarioRunner(
            driver, registry, profile, lease, input, evidence, probe.Capture,
            captureScreenshotPng: () => fakePng);

        QaScenarioRunOutcome outcome = runner.RunAsync(BuildAssertThenCaptureScenario()).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        Assert.IsTrue(
            evidence.Events.Exists(e => e.Type == QaEvidenceEventType.ScreenshotAttached),
            "evidence.capture must attach at least one ScreenshotAttached event when a provider is injected.");

        Assert.IsNotNull(evidence.LastManifest, "Finalize must produce a manifest.");
        Assert.AreEqual(
            QaRunVerdictCode.Pass, evidence.LastManifest.Verdict, evidence.LastManifest.VerdictReason);
        Assert.GreaterOrEqual(evidence.LastManifest.ScreenshotCount, 1);
        Assert.GreaterOrEqual(evidence.LastManifest.AssertionPassedCount, 1);
    }

    [Test]
    public void RunAsync_NoExplicitCaptureStep_SafetyNetStillAttachesScreenshotBeforeFinalize()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe { InputGateLocked = false };
        byte[] fakePng = { 5, 6, 7 };

        var runner = new QaScenarioRunner(
            driver, registry, profile, lease, input, evidence, probe.Capture,
            captureScreenshotPng: () => fakePng);

        var scenario = new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.assert-only",
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

        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        Assert.IsNotNull(evidence.LastManifest);
        Assert.AreEqual(
            QaRunVerdictCode.Pass, evidence.LastManifest.Verdict, evidence.LastManifest.VerdictReason);
        Assert.GreaterOrEqual(
            evidence.LastManifest.ScreenshotCount, 1,
            "The pre-Finalize safety net must attach a screenshot even without an explicit evidence.capture step.");
    }

    // ---------------------------------------------------------------
    //  Fail-safe: never fabricate evidence when no provider exists
    // ---------------------------------------------------------------

    [Test]
    public void RunAsync_EvidenceCaptureStepWithoutProvider_FailsExplicitlyInsteadOfFabricatingEvidence()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe { InputGateLocked = false };

        // Deliberately no captureScreenshotPng injected.
        var runner = new QaScenarioRunner(driver, registry, profile, lease, input, evidence, probe.Capture);

        QaScenarioRunOutcome outcome = runner.RunAsync(BuildAssertThenCaptureScenario()).GetAwaiter().GetResult();

        Assert.AreEqual(QaScenarioRunOutcomeCode.Failed, outcome.Code);
        Assert.IsFalse(
            evidence.Events.Exists(e => e.Type == QaEvidenceEventType.ScreenshotAttached),
            "Without a provider, evidence.capture must fail loudly rather than fabricate a screenshot.");
    }

    // ---------------------------------------------------------------
    //  Test doubles (mirrors QaScenarioRunnerTests' conventions)
    // ---------------------------------------------------------------

    private sealed class FakeSceneAdapter : IQaSceneAdapter
    {
        private readonly List<QaTargetId> targetIds;
        private readonly List<string> presetIds;

        public FakeSceneAdapter(string sceneName, IEnumerable<string> rawTargetIds, IEnumerable<string> presetIds)
        {
            SceneName = sceneName;
            targetIds = new List<QaTargetId>();
            foreach (string raw in rawTargetIds)
            {
                if (QaTargetId.TryCreate(raw, out QaTargetId id, out _))
                {
                    targetIds.Add(id);
                }
            }

            this.presetIds = new List<string>(presetIds);
        }

        public string SceneName { get; }

        public IReadOnlyCollection<QaTargetId> TargetIds => targetIds;

        public IReadOnlyCollection<string> PresetIds => presetIds;

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return presetIds.Contains(presetId)
                ? QaScenePresetResult.Success()
                : QaScenePresetResult.UnknownPreset(presetId);
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
            return QaProfileOperationResult.Success("Fake QA profile began.");
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.Success("Fake QA profile reset.");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            IsQaProfileActive = false;
            return QaProfileOperationResult.Success("Fake QA profile restored.");
        }

        public QaProfileOperationResult RecoverInterruptedSession()
        {
            return QaProfileOperationResult.NothingToRecover("Nothing to recover.");
        }
    }

    private sealed class FakeInputDriver : IQaInputDriver
    {
        public QaInteractionMode Mode => QaInteractionMode.Api;

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(targetId, QaInteractionMode.Api));
        }

        public Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId, QaTargetId destinationTargetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(sourceTargetId, QaInteractionMode.Api));
        }

        public Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(QaInputResult.Success(targetId, QaInteractionMode.Api));
        }
    }

    /// <summary>
    /// Same shape as <c>QaScenarioRunnerTests.FakeEvidenceRecorder</c>, plus <see cref="LastManifest"/>
    /// so these tests can assert directly against the real <see cref="QaRunManifest.AggregateVerdict"/>
    /// output instead of only the runner's own outcome code.
    /// </summary>
    private sealed class FakeEvidenceRecorder : IQaEvidenceRecorder
    {
        public List<QaEvidenceEvent> Events { get; } = new List<QaEvidenceEvent>();

        public QaRunManifest LastManifest { get; private set; }

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            return QaEvidenceOperationResult.Success("Fake evidence run began.");
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            Events.Add(evidenceEvent);
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            if (pngBytes == null || pngBytes.Length == 0)
            {
                return QaEvidenceOperationResult.Invalid("pngBytes must not be null or empty.");
            }

            Events.Add(QaEvidenceEvent.Create(
                QaEvidenceEventType.ScreenshotAttached, commandId, message: "Fake screenshot attached."));
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceOperationResult RecordConsole(string logText)
        {
            Events.Add(QaEvidenceEvent.Create(QaEvidenceEventType.ConsoleRecorded, message: logText));
            return QaEvidenceOperationResult.Success();
        }

        public QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null)
        {
            LastManifest = QaRunManifest.Create(
                "fake-run", "fake-dir", DateTime.UtcNow, DateTime.UtcNow, Events,
                "events.jsonl", "console.log", "screenshots", "report.md");
            return QaEvidenceFinalizeResult.Success(LastManifest, "fake-dir");
        }
    }

    private sealed class MutableProbe
    {
        public bool InputGateLocked { get; set; }

        public QaDriverSnapshot Capture()
        {
            return QaDriverSnapshot.Create(inputGateLocked: InputGateLocked);
        }
    }
}
