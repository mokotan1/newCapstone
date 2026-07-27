#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Evidence;

namespace Godlotto.QA.Scenarios
{
    /// <summary><see cref="QaConditionWaiter.WaitUntilAsync"/>가 반환할 수 있는 명시적 결과 코드.</summary>
    public enum QaWaitResultCode
    {
        Passed,
        TimedOut,
        Cancelled
    }

    /// <summary>
    /// <see cref="QaConditionWaiter.WaitUntilAsync"/> 호출 한 건의 불변 결과. 타임아웃 시에도
    /// 마지막으로 관측한 어서션 결과·스냅샷·경과 시간을 항상 담아, "무엇을 왜 얼마나
    /// 기다렸는지"가 재현 가능하게 남습니다(Task 8 인터페이스: "timeout diagnostics with
    /// last observed value and elapsed time").
    /// </summary>
    public sealed class QaWaitResult
    {
        public QaWaitResultCode Code { get; }

        /// <summary>마지막으로 평가된 어서션 결과. 최소 한 번은 평가가 시도되므로 취소된 경우가 아니면 항상 값이 있습니다.</summary>
        public QaAssertionResult LastAssertionResult { get; }

        /// <summary>마지막으로 캡처된 스냅샷(타임아웃 진단용). 캡처가 한 번도 성공하지 못했으면 <c>null</c>.</summary>
        public QaDriverSnapshot FinalSnapshot { get; }

        /// <summary>대기 시작부터 종료까지 실제 경과한 실시간(wall-clock) 시간.</summary>
        public TimeSpan Elapsed { get; }

        /// <summary>수행된 폴링(캡처+평가) 횟수.</summary>
        public int PollCount { get; }

        private QaWaitResult(
            QaWaitResultCode code,
            QaAssertionResult lastAssertionResult,
            QaDriverSnapshot finalSnapshot,
            TimeSpan elapsed,
            int pollCount)
        {
            Code = code;
            LastAssertionResult = lastAssertionResult;
            FinalSnapshot = finalSnapshot;
            Elapsed = elapsed;
            PollCount = pollCount;
        }

        public bool IsSuccess
        {
            get { return Code == QaWaitResultCode.Passed; }
        }

        public static QaWaitResult Passed(
            QaAssertionResult lastAssertionResult, QaDriverSnapshot finalSnapshot, TimeSpan elapsed, int pollCount)
        {
            return new QaWaitResult(QaWaitResultCode.Passed, lastAssertionResult, finalSnapshot, elapsed, pollCount);
        }

        public static QaWaitResult TimedOut(
            QaAssertionResult lastAssertionResult, QaDriverSnapshot finalSnapshot, TimeSpan elapsed, int pollCount)
        {
            return new QaWaitResult(QaWaitResultCode.TimedOut, lastAssertionResult, finalSnapshot, elapsed, pollCount);
        }

        public static QaWaitResult Cancelled(
            QaAssertionResult lastAssertionResult, QaDriverSnapshot finalSnapshot, TimeSpan elapsed, int pollCount)
        {
            return new QaWaitResult(QaWaitResultCode.Cancelled, lastAssertionResult, finalSnapshot, elapsed, pollCount);
        }
    }

    /// <summary>
    /// <see cref="QaAssertion"/>이 통과할 때까지(또는 데드라인/취소까지) 실시간(wall-clock)
    /// 데드라인과 프레임 양보(<c>await Task.Yield()</c>)로 폴링하는 조건 기반 대기자
    /// (Task 8 §Step 3). 스냅샷 캡처 방법은 생성자 콜백으로만 주입받으므로(DIP), 이 타입은
    /// 게임 상태를 직접 알지 못합니다. 절대 어떤 게임플레이 상태도 강제로 변경하지 않으며
    /// (읽기 전용 폴링만 수행), 타임아웃되어도 마지막으로 관측한 스냅샷/어서션 결과를 그대로
    /// 보고합니다 — 나머지 QA 인프라와 동일하게 "예외 없음"을 성공으로 추론하지 않습니다.
    /// </summary>
    public sealed class QaConditionWaiter
    {
        private readonly Func<QaDriverSnapshot> captureSnapshot;
        private readonly Func<DateTime> utcNowProvider;
        private readonly Func<Task> frameYieldProvider;

        /// <param name="captureSnapshot">매 폴링마다 현재 상태 스냅샷을 캡처하는 콜백. 필수입니다.</param>
        /// <param name="utcNowProvider">테스트용 시각 주입 훅. 생략하면 <see cref="DateTime.UtcNow"/> 사용.</param>
        /// <param name="frameYieldProvider">
        /// 폴링 사이에 양보할 방법. 생략하면 <c>await Task.Yield()</c>를 사용합니다(Unity
        /// 플레이어 루프에서는 다음 프레임까지 자연스럽게 양보됩니다). PlayMode 테스트에서는
        /// 실제 프레임 진행과 결합하기 위해 그대로 사용해도 되고, 필요하면 대체할 수 있습니다.
        /// </param>
        public QaConditionWaiter(
            Func<QaDriverSnapshot> captureSnapshot,
            Func<DateTime> utcNowProvider = null,
            Func<Task> frameYieldProvider = null)
        {
            this.captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
            this.frameYieldProvider = frameYieldProvider ?? DefaultFrameYield;
        }

        /// <summary>
        /// <paramref name="assertion"/>이 통과할 때까지 폴링합니다. <paramref name="timeout"/>이
        /// 지나기 전에 통과하면 <see cref="QaWaitResultCode.Passed"/>를, 취소되면
        /// <see cref="QaWaitResultCode.Cancelled"/>를, 그 외에는 <see cref="QaWaitResultCode.TimedOut"/>을
        /// 반환합니다. 항상 최소 한 번은 평가를 시도합니다(즉시 통과하는 조건도 정상적으로
        /// 감지됩니다).
        /// </summary>
        public async Task<QaWaitResult> WaitUntilAsync(
            QaAssertion assertion,
            TimeSpan timeout,
            QaDriverSnapshot baseline = null,
            CancellationToken cancellationToken = default)
        {
            if (assertion == null)
            {
                throw new ArgumentNullException(nameof(assertion));
            }

            DateTime startUtc = utcNowProvider();
            DateTime deadlineUtc = startUtc + timeout;
            int pollCount = 0;
            QaAssertionResult lastResult = null;
            QaDriverSnapshot lastSnapshot = null;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return QaWaitResult.Cancelled(lastResult, lastSnapshot, utcNowProvider() - startUtc, pollCount);
                }

                lastSnapshot = SafeCapture();
                lastResult = assertion.Evaluate(lastSnapshot, baseline);
                pollCount++;

                if (lastResult.Passed)
                {
                    return QaWaitResult.Passed(lastResult, lastSnapshot, utcNowProvider() - startUtc, pollCount);
                }

                DateTime now = utcNowProvider();
                if (now >= deadlineUtc)
                {
                    return QaWaitResult.TimedOut(lastResult, lastSnapshot, now - startUtc, pollCount);
                }

                try
                {
                    await frameYieldProvider().ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return QaWaitResult.Cancelled(lastResult, lastSnapshot, utcNowProvider() - startUtc, pollCount);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return QaWaitResult.Cancelled(lastResult, lastSnapshot, utcNowProvider() - startUtc, pollCount);
                }
            }
        }

        private QaDriverSnapshot SafeCapture()
        {
            try
            {
                return captureSnapshot();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaConditionWaiter] captureSnapshot threw: " + ex.GetType().Name);
                return null;
            }
        }

        private static async Task DefaultFrameYield()
        {
            await Task.Yield();
        }
    }
}
#endif
