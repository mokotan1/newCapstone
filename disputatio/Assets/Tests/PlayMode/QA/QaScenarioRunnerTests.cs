using System;
using System.Collections;
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
using UnityEngine.TestTools;

/// <summary>
/// Task 9 §Step 3-4의 <see cref="QaScenarioRunner"/>를 PlayMode에서 검증합니다.
/// <see cref="QaConditionWaiter"/> 기반 <c>state.assert</c> 대기가 실제 프레임 진행과 함께
/// 동작해야 하므로 EditMode가 아니라 PlayMode 테스트로 작성합니다. 실제 게임 씬을 로드하지
/// 않고, driver/lease는 실제(가벼운 인메모리) 구현을, profile/input/evidence는 페이크를
/// 사용합니다(DIP). 모든 테스트는 (1) 성공 시 프로필 복원·리스 반납·evidence finalize,
/// (2) 스텝 실패 시에도 동일한 정리, (3) 대기 중 취소 시 프로필 복원·리스 반납·최종 manifest
/// 상태 <c>Interrupted</c>·마지막 관측 스냅샷 보존을 확인합니다.
/// </summary>
public sealed class QaScenarioRunnerTests
{
    private const string SceneName = "Kitchen";
    private const string TargetId = "kitchen.sink.faucet";
    private const string PresetId = "before-faucet";

    private static IEnumerator ToCoroutine(Task task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            throw task.Exception.GetBaseException();
        }
    }

    private static QaSceneRegistry BuildRegistry()
    {
        var registry = new QaSceneRegistry();
        registry.Register(new FakeSceneAdapter(SceneName, new[] { TargetId }, new[] { PresetId }));
        return registry;
    }

    private static QaScenarioDefinition BuildPointerAndAssertScenario()
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.faucet-key",
            Scene = SceneName,
            Preset = PresetId,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "s1",
                    Command = QaScenarioSchema.CommandInteractionPointer,
                    Target = TargetId,
                    TimeoutMs = 5000
                },
                new QaScenarioStepDefinition
                {
                    Id = "s2",
                    Command = QaScenarioSchema.CommandStateAssert,
                    TimeoutMs = 5000,
                    Assertion = new QaScenarioAssertionDefinition { Kind = "inputUnlocked" }
                }
            }
        };
    }

    private static QaScenarioDefinition BuildLongWaitingAssertOnlyScenario(int timeoutMs)
    {
        return new QaScenarioDefinition
        {
            SchemaVersion = QaScenarioSchema.SupportedSchemaVersion,
            Id = "kitchen.long-wait",
            Scene = SceneName,
            Steps = new List<QaScenarioStepDefinition>
            {
                new QaScenarioStepDefinition
                {
                    Id = "waitForUnlock",
                    Command = QaScenarioSchema.CommandStateAssert,
                    TimeoutMs = timeoutMs,
                    Assertion = new QaScenarioAssertionDefinition { Kind = "inputUnlocked" }
                }
            }
        };
    }

    // ---------------------------------------------------------------
    //  Success path
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator RunAsync_AllStepsPass_ReturnsPassedAndCleansUpProfileAndLease()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe { InputGateLocked = false };

        var runner = new QaScenarioRunner(driver, registry, profile, lease, input, evidence, probe.Capture);

        Task<QaScenarioRunOutcome> task = runner.RunAsync(BuildPointerAndAssertScenario());
        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Passed, outcome.Code, outcome.Message);
        Assert.AreEqual(2, outcome.StepOutcomes.Count);
        Assert.IsTrue(outcome.StepOutcomes[0].IsSuccess);
        Assert.IsTrue(outcome.StepOutcomes[1].IsSuccess);

        Assert.AreEqual(1, profile.BeginCount);
        Assert.AreEqual(1, profile.RestoreCount, "Profile must be restored exactly once on a successful run.");
        Assert.IsFalse(profile.IsQaProfileActive);

        Assert.IsTrue(evidence.BeginRunCalled);
        Assert.IsTrue(evidence.FinalizeCalled);

        AssertLeaseIsFree(lease);
        Assert.AreEqual(QaRunPhase.Ended, driver.CurrentRun.Phase);
    }

    // ---------------------------------------------------------------
    //  Step failure still cleans up
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator RunAsync_StepFails_ReturnsFailedAndStillRestoresProfileAndReleasesLease()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver
        {
            ClickResultFactory = targetId => QaInputResult.Failure(
                targetId, QaInteractionMode.Api, QaInputResultCode.UnsupportedInteraction, "Simulated failure.")
        };
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe { InputGateLocked = false };

        var runner = new QaScenarioRunner(driver, registry, profile, lease, input, evidence, probe.Capture);

        Task<QaScenarioRunOutcome> task = runner.RunAsync(BuildPointerAndAssertScenario());
        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Failed, outcome.Code);
        Assert.AreEqual(1, outcome.StepOutcomes.Count, "Execution must stop at the first failing step.");
        Assert.IsFalse(outcome.StepOutcomes[0].IsSuccess);

        Assert.AreEqual(1, profile.RestoreCount, "Profile must be restored even when a step fails.");
        Assert.IsFalse(profile.IsQaProfileActive);

        AssertLeaseIsFree(lease);
        Assert.AreEqual(QaRunPhase.Aborted, driver.CurrentRun.Phase);
    }

    // ---------------------------------------------------------------
    //  Cancellation mid-wait: Step 4
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator RunAsync_CancelledDuringAssertionWait_ReturnsInterruptedRestoresProfileReleasesLeaseAndKeepsFinalSnapshot()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        // Input gate never unlocks, so the state.assert step below waits until cancelled.
        var probe = new MutableProbe { InputGateLocked = true };

        var runner = new QaScenarioRunner(driver, registry, profile, lease, input, evidence, probe.Capture);

        using var cts = new CancellationTokenSource();
        Task<QaScenarioRunOutcome> task = runner.RunAsync(BuildLongWaitingAssertOnlyScenario(30000), cts.Token);

        // Let a few real frames pass so QaConditionWaiter has polled at least once before we cancel
        // (mirrors the deterministic "cancel after first poll" pattern used for QaConditionWaiter itself).
        for (int frame = 0; frame < 3; frame++)
        {
            yield return null;
        }

        Assert.IsTrue(profile.IsQaProfileActive, "Profile must still be active while the scenario is mid-run.");
        cts.Cancel();

        yield return ToCoroutine(task);

        QaScenarioRunOutcome outcome = task.Result;
        Assert.AreEqual(QaScenarioRunOutcomeCode.Interrupted, outcome.Code, outcome.Message);

        Assert.AreEqual(1, profile.RestoreCount, "Profile must be restored even when the run is cancelled mid-wait.");
        Assert.IsFalse(profile.IsQaProfileActive, "Profile must no longer be active after cancellation cleanup.");

        AssertLeaseIsFree(lease);

        Assert.IsNotNull(
            outcome.FinalSnapshot,
            "The last observed game-state snapshot must be captured and reported even on cancellation.");
        Assert.IsTrue(
            outcome.FinalSnapshot.InputGateLocked,
            "The final snapshot must reflect the true last-observed state, never a fabricated pass.");

        Assert.AreEqual(QaRunPhase.Aborted, driver.CurrentRun.Phase);
        Assert.IsTrue(evidence.FinalizeCalled, "Evidence must still be finalized after an interrupted run.");
    }

    [Test]
    public void RunAsync_NullScenario_ThrowsArgumentNullException()
    {
        var driver = new QaDriverCore();
        var registry = BuildRegistry();
        var profile = new FakeProfileService();
        var lease = new QaLeaseService();
        var input = new FakeInputDriver();
        var evidence = new FakeEvidenceRecorder();
        var probe = new MutableProbe();

        var runner = new QaScenarioRunner(driver, registry, profile, lease, input, evidence, probe.Capture);

        Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null));
    }

    // ---------------------------------------------------------------
    //  Helpers
    // ---------------------------------------------------------------

    private static void AssertLeaseIsFree(QaLeaseService lease)
    {
        QaLeaseAcquireResult reacquire = lease.TryAcquire("someone-else", QaRunId.NewId(), TimeSpan.FromMinutes(1));
        Assert.IsTrue(reacquire.IsAcquired, "Lease must have been released by the runner: " + reacquire.Message);
        lease.Release(reacquire.Lease.LeaseId);
    }

    // ---------------------------------------------------------------
    //  Test doubles
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
        public int BeginCount { get; private set; }

        public int RestoreCount { get; private set; }

        public bool IsQaProfileActive { get; private set; }

        public QaProfileOperationResult BeginQaProfile(QaRunId runId)
        {
            BeginCount++;
            IsQaProfileActive = true;
            return QaProfileOperationResult.Success("Fake QA profile began.");
        }

        public QaProfileOperationResult ResetGameplay()
        {
            return QaProfileOperationResult.Success("Fake QA profile reset.");
        }

        public QaProfileOperationResult RestorePreviousProfile()
        {
            RestoreCount++;
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
        public Func<QaTargetId, QaInputResult> ClickResultFactory { get; set; } =
            targetId => QaInputResult.Success(targetId, QaInteractionMode.Api);

        public Func<QaTargetId, QaTargetId, QaInputResult> DragResultFactory { get; set; } =
            (source, destination) => QaInputResult.Success(source, QaInteractionMode.Api);

        public Func<QaTargetId, string, QaInputResult> KeyResultFactory { get; set; } =
            (targetId, text) => QaInputResult.Success(targetId, QaInteractionMode.Api);

        public QaInteractionMode Mode => QaInteractionMode.Api;

        public Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ClickResultFactory(targetId));
        }

        public Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId, QaTargetId destinationTargetId, CancellationToken cancellationToken)
        {
            return Task.FromResult(DragResultFactory(sourceTargetId, destinationTargetId));
        }

        public Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(KeyResultFactory(targetId, text));
        }
    }

    private sealed class FakeEvidenceRecorder : IQaEvidenceRecorder
    {
        public bool BeginRunCalled { get; private set; }

        public bool FinalizeCalled { get; private set; }

        public List<QaEvidenceEvent> Events { get; } = new List<QaEvidenceEvent>();

        public QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null)
        {
            BeginRunCalled = true;
            return QaEvidenceOperationResult.Success("Fake evidence run began.");
        }

        public QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent)
        {
            Events.Add(evidenceEvent);
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
            FinalizeCalled = true;
            QaRunManifest manifest = QaRunManifest.Create(
                "fake-run", "fake-dir", DateTime.UtcNow, DateTime.UtcNow, Events,
                "events.jsonl", "console.log", "screenshots", "report.md");
            return QaEvidenceFinalizeResult.Success(manifest, "fake-dir");
        }
    }

    /// <summary>
    /// 매 폴링마다 호출자가 값을 직접 바꿀 수 있는 가변 페이크 프로브(<see cref="QaConditionWaiterTests"/>의
    /// <c>MutableFakeProbe</c>와 동일한 역할). 실제 <c>QaStateProbe</c>를 대체합니다.
    /// </summary>
    private sealed class MutableProbe
    {
        public bool InputGateLocked { get; set; }

        public int CaptureCount { get; private set; }

        public QaDriverSnapshot Capture()
        {
            CaptureCount++;
            return QaDriverSnapshot.Create(inputGateLocked: InputGateLocked);
        }
    }
}
