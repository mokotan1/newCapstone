using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Evidence;
using Godlotto.QA.Scenarios;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Task 8 §Step 3의 <see cref="QaConditionWaiter"/>를 PlayMode에서 검증합니다. 실제 Unity
/// 플레이어 루프(프레임 진행, <c>SynchronizationContext</c> 기반 <c>Task</c> 연속 실행)와 함께
/// 동작해야만 <c>await Task.Yield()</c> 기반 폴링이 의미를 갖기 때문에 EditMode가 아니라
/// PlayMode 테스트로 작성합니다. <see cref="MutableFakeProbe"/>는 매 폴링마다 호출자가 직접
/// 값을 바꿀 수 있는 가변 페이크로, 시간이 지나면서 게임 상태가 변하는 상황(퍼즐 해결, 대사
/// 종료 등)을 흉내냅니다. 모든 테스트는 (1) 즉시 통과, (2) 지연 후 통과, (3) 데드라인까지
/// 통과하지 못해 <c>TimedOut</c>과 함께 마지막 관측 스냅샷/경과 시간을 보고, (4) 취소 시
/// 절대 성공으로 위장하지 않으며, (5) 타임아웃되어도 게임플레이 상태(입력 게이트)를 절대
/// 강제로 바꾸지 않는다는 설계 계약을 확인합니다.
/// </summary>
public sealed class QaConditionWaiterTests
{
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

    // ---------------------------------------------------------------
    //  Passing conditions
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator WaitUntilAsync_ConditionAlreadyTrue_PassesOnTheFirstPollWithoutWaiting()
    {
        var state = new MutableFakeProbe { InputGateLocked = false };
        var waiter = new QaConditionWaiter(state.Capture);

        Task<QaWaitResult> task = waiter.WaitUntilAsync(QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(5));
        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.AreEqual(QaWaitResultCode.Passed, result.Code);
        Assert.IsTrue(result.IsSuccess, result.LastAssertionResult?.Message);
        Assert.AreEqual(1, result.PollCount, "A condition that is already true must be detected on the very first poll.");
        Assert.IsNotNull(result.FinalSnapshot);
    }

    [UnityTest]
    public IEnumerator WaitUntilAsync_ConditionBecomesTrueAfterSeveralFrames_EventuallyPasses()
    {
        // Models gameplay progressing while the waiter polls: the fake starts locked and only
        // flips open after a few real frames, so the waiter must poll more than once to observe it.
        var state = new MutableFakeProbe { InputGateLocked = true };
        var waiter = new QaConditionWaiter(state.Capture);

        Task<QaWaitResult> task = waiter.WaitUntilAsync(QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(10));

        for (int frame = 0; frame < 3; frame++)
        {
            yield return null;
        }

        state.InputGateLocked = false;
        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.IsTrue(result.IsSuccess, result.LastAssertionResult?.Message);
        Assert.Greater(result.PollCount, 1, "The waiter must have polled more than once while the gate was still locked.");
        Assert.IsFalse(result.FinalSnapshot.InputGateLocked);
    }

    // ---------------------------------------------------------------
    //  Timeout diagnostics (Step 3: "TimedOut with a final snapshot"; never force-unlock)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator WaitUntilAsync_ConditionNeverTrue_TimesOutWithLastObservedSnapshotAndElapsed()
    {
        var state = new MutableFakeProbe { InputGateLocked = true };
        var clock = new AutoAdvanceClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
        var waiter = new QaConditionWaiter(state.Capture, clock.UtcNow);

        Task<QaWaitResult> task = waiter.WaitUntilAsync(QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(5));
        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.AreEqual(QaWaitResultCode.TimedOut, result.Code);
        Assert.IsFalse(result.IsSuccess);
        Assert.GreaterOrEqual(result.Elapsed, TimeSpan.FromSeconds(5));
        Assert.Greater(result.PollCount, 1);
        Assert.IsNotNull(result.FinalSnapshot, "A timeout must still report the last observed snapshot for diagnostics.");
        Assert.IsTrue(
            result.FinalSnapshot.InputGateLocked,
            "The waiter must never force-unlock gameplay state; the final snapshot must reflect reality (still locked).");
        Assert.IsFalse(result.LastAssertionResult.Passed);
        StringAssert.Contains("True", result.LastAssertionResult.ObservedValue);
    }

    [UnityTest]
    public IEnumerator WaitUntilAsync_CaptureAlwaysThrows_NeverCrashesAndFailsSafeToTimedOut()
    {
        var clock = new AutoAdvanceClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
        var waiter = new QaConditionWaiter(() => throw new InvalidOperationException("boom"), clock.UtcNow);

        Task<QaWaitResult> task = null;
        Assert.DoesNotThrow(
            () => task = waiter.WaitUntilAsync(QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(5)),
            "Starting the wait must never throw, even if the injected capture callback is broken.");

        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.AreEqual(QaWaitResultCode.TimedOut, result.Code);
        Assert.IsNull(result.FinalSnapshot, "If capture never succeeds even once, there is no snapshot to report.");
        Assert.IsFalse(result.LastAssertionResult.Passed);
    }

    // ---------------------------------------------------------------
    //  Cancellation (never reports success on cancellation; never fabricates state)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator WaitUntilAsync_AlreadyCancelledToken_ReturnsCancelledWithoutProbingStateAtAll()
    {
        var state = new MutableFakeProbe { InputGateLocked = true };
        var waiter = new QaConditionWaiter(state.Capture);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task<QaWaitResult> task = waiter.WaitUntilAsync(
            QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(5), cancellationToken: cts.Token);
        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.AreEqual(QaWaitResultCode.Cancelled, result.Code);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(0, state.CaptureCount, "A pre-cancelled token must short-circuit before any state is probed.");
        Assert.IsNull(result.FinalSnapshot);
        Assert.IsNull(result.LastAssertionResult);
    }

    [UnityTest]
    public IEnumerator WaitUntilAsync_CancelledAfterFirstPoll_ReturnsCancelledWithLastObservedSnapshot()
    {
        var state = new MutableFakeProbe { InputGateLocked = true };
        var waiter = new QaConditionWaiter(state.Capture);
        using var cts = new CancellationTokenSource();

        // WaitUntilAsync runs synchronously up to its first `await`, so exactly one poll has
        // already happened by the time this call returns control here (the assertion is still
        // failing, so it awaits a frame yield before looping again) — cancelling immediately after
        // deterministically exercises the "cancelled mid-poll" path without any frame-timing race.
        Task<QaWaitResult> task = waiter.WaitUntilAsync(
            QaAssertion.InputUnlocked(), TimeSpan.FromSeconds(30), cancellationToken: cts.Token);
        cts.Cancel();

        yield return ToCoroutine(task);

        QaWaitResult result = task.Result;
        Assert.AreEqual(QaWaitResultCode.Cancelled, result.Code);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(1, state.CaptureCount);
        Assert.IsNotNull(result.FinalSnapshot, "Cancellation after at least one poll must retain the last observed snapshot.");
        Assert.IsTrue(
            result.FinalSnapshot.InputGateLocked,
            "Cancellation must report the true last-observed state, never a fabricated success.");
    }

    // ---------------------------------------------------------------
    //  Argument validation
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator WaitUntilAsync_NullAssertion_FaultsWithArgumentNullException()
    {
        var waiter = new QaConditionWaiter(() => QaDriverSnapshot.Create());

        Task<QaWaitResult> task = waiter.WaitUntilAsync(null, TimeSpan.FromSeconds(1));

        while (!task.IsCompleted)
        {
            yield return null;
        }

        Assert.IsTrue(task.IsFaulted);
        Assert.IsInstanceOf<ArgumentNullException>(task.Exception.GetBaseException());
    }

    // ---------------------------------------------------------------
    //  Test doubles
    // ---------------------------------------------------------------

    /// <summary>
    /// 매 폴링마다 호출자가 값을 직접 바꿀 수 있는 가변 페이크 프로브. 실제 <see cref="QaStateProbe"/>를
    /// 대체하여, "시간이 지나며 게임 상태가 바뀌는 상황"을 시뮬레이션합니다.
    /// </summary>
    private sealed class MutableFakeProbe
    {
        public bool InputGateLocked { get; set; }

        public int CaptureCount { get; private set; }

        public QaDriverSnapshot Capture()
        {
            CaptureCount++;
            return QaDriverSnapshot.Create(inputGateLocked: InputGateLocked);
        }
    }

    /// <summary>
    /// 호출될 때마다 고정된 간격만큼 스스로 전진하는 가짜 시계. 실제 벽시계 시간을 몇 초씩
    /// 기다리지 않고도 <see cref="QaConditionWaiter"/>의 데드라인 도달을 몇 프레임 안에 결정적으로
    /// 재현하기 위한 테스트 전용 훅입니다.
    /// </summary>
    private sealed class AutoAdvanceClock
    {
        private readonly TimeSpan stepPerCall;
        private DateTime current;

        public AutoAdvanceClock(DateTime startUtc, TimeSpan stepPerCall)
        {
            current = startUtc;
            this.stepPerCall = stepPerCall;
        }

        public DateTime UtcNow()
        {
            DateTime value = current;
            current += stepPerCall;
            return value;
        }
    }
}
