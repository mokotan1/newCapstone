#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.Scenarios
{
    /// <summary><see cref="QaScenarioRunOutcome"/>가 나타낼 수 있는 명시적 최종 상태.</summary>
    public enum QaScenarioRunOutcomeCode
    {
        Passed,
        Failed,

        /// <summary>
        /// 실행 중(주로 <c>state.assert</c> 대기 중) 취소되었습니다. 실패한 어서션과 구분되는
        /// 별도 상태입니다 — 취소는 "무언가 잘못됨"이 아니라 "호출자가 중단을 요청함"이므로,
        /// evidence 상 실패로 오염시키지 않습니다.
        /// </summary>
        Interrupted
    }

    /// <summary><see cref="QaScenarioStepOutcome"/>가 나타낼 수 있는 명시적 스텝 결과.</summary>
    public enum QaScenarioStepOutcomeCode
    {
        Success,
        Failed,
        Cancelled
    }

    /// <summary>하나의 시나리오 스텝 실행 한 건의 불변 결과.</summary>
    public sealed class QaScenarioStepOutcome
    {
        public string StepId { get; }

        public QaScenarioStepOutcomeCode Code { get; }

        public string Message { get; }

        /// <summary>이 스텝 종료 시점에 관측된 스냅샷(있는 경우). 절대 강제로 상태를 바꾸지 않고 관측만 합니다.</summary>
        public QaDriverSnapshot Snapshot { get; }

        private QaScenarioStepOutcome(
            string stepId, QaScenarioStepOutcomeCode code, string message, QaDriverSnapshot snapshot)
        {
            StepId = stepId ?? string.Empty;
            Code = code;
            Message = message ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool IsSuccess
        {
            get { return Code == QaScenarioStepOutcomeCode.Success; }
        }

        public bool WasCancelled
        {
            get { return Code == QaScenarioStepOutcomeCode.Cancelled; }
        }

        public static QaScenarioStepOutcome Success(string stepId, string message, QaDriverSnapshot snapshot = null)
        {
            return new QaScenarioStepOutcome(stepId, QaScenarioStepOutcomeCode.Success, message, snapshot);
        }

        public static QaScenarioStepOutcome Failed(string stepId, string message, QaDriverSnapshot snapshot = null)
        {
            return new QaScenarioStepOutcome(stepId, QaScenarioStepOutcomeCode.Failed, message, snapshot);
        }

        public static QaScenarioStepOutcome Cancelled(string stepId, string message, QaDriverSnapshot snapshot = null)
        {
            return new QaScenarioStepOutcome(stepId, QaScenarioStepOutcomeCode.Cancelled, message, snapshot);
        }
    }

    /// <summary><see cref="QaScenarioRunner.RunAsync"/> 호출 한 건의 불변 최종 결과.</summary>
    public sealed class QaScenarioRunOutcome
    {
        private static readonly IReadOnlyList<QaScenarioStepOutcome> EmptySteps =
            new ReadOnlyCollection<QaScenarioStepOutcome>(new List<QaScenarioStepOutcome>());

        public string ScenarioId { get; }

        public QaRunId RunId { get; }

        public QaScenarioRunOutcomeCode Code { get; }

        public string Message { get; }

        public DateTime StartedAtUtc { get; }

        public DateTime EndedAtUtc { get; }

        /// <summary>실제로 실행이 시도된 스텝의 결과 전체(순서 보존). 절대 null이 아닙니다.</summary>
        public IReadOnlyList<QaScenarioStepOutcome> StepOutcomes { get; }

        /// <summary>정리(cleanup) 전에 관측된 마지막 스냅샷(있는 경우). 취소/실패 진단용입니다.</summary>
        public QaDriverSnapshot FinalSnapshot { get; }

        private QaScenarioRunOutcome(
            string scenarioId,
            QaRunId runId,
            QaScenarioRunOutcomeCode code,
            string message,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            IReadOnlyList<QaScenarioStepOutcome> stepOutcomes,
            QaDriverSnapshot finalSnapshot)
        {
            ScenarioId = scenarioId ?? string.Empty;
            RunId = runId;
            Code = code;
            Message = message ?? string.Empty;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
            StepOutcomes = stepOutcomes ?? EmptySteps;
            FinalSnapshot = finalSnapshot;
        }

        public bool IsSuccess
        {
            get { return Code == QaScenarioRunOutcomeCode.Passed; }
        }

        public static QaScenarioRunOutcome Create(
            string scenarioId,
            QaRunId runId,
            QaScenarioRunOutcomeCode code,
            string message,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            IReadOnlyList<QaScenarioStepOutcome> stepOutcomes,
            QaDriverSnapshot finalSnapshot)
        {
            return new QaScenarioRunOutcome(
                scenarioId, runId, code, message, startedAtUtc, endedAtUtc, stepOutcomes, finalSnapshot);
        }
    }

    /// <summary>
    /// 검증된 스키마 v1 시나리오를 순차 실행하는 러너(Task 9 §Step 3-4). 씬/대상 해석은
    /// <see cref="QaSceneRegistry"/>, 상호작용은 <see cref="IQaInputDriver"/>, 대기·어서션은
    /// Task 8의 <see cref="QaAssertion"/>/<see cref="QaConditionWaiter"/>, 프로필 격리는
    /// <see cref="IQaProfileService"/>, 동시 실행 방지는 <see cref="QaLeaseService"/>, 증거 기록은
    /// <see cref="IQaEvidenceRecorder"/>에만 위임합니다(DIP) — 이 타입 자체는 게임플레이 규칙을
    /// 전혀 알지 못합니다.
    ///
    /// 실행 경계(디자인 문서 Task 9 §Step 3): QA 프로필을 시작하고 리스를 획득한 뒤에만 스텝을
    /// 실행하며, 성공/실패/취소/예외 중 무엇으로 끝나든 항상 프로필을 복원하고 리스를 반납합니다
    /// (finally). "예외가 없었다"는 사실만으로 성공을 추론하지 않고, 각 스텝의 명시적 결과
    /// 코드만을 근거로 최종 <see cref="QaScenarioRunOutcomeCode"/>를 결정합니다.
    /// </summary>
    public sealed class QaScenarioRunner
    {
        private const string DefaultOwnerId = "QaScenarioRunner";
        private static readonly TimeSpan DefaultLeaseTtl = TimeSpan.FromMinutes(10);

        private readonly IQaDriver driver;
        private readonly QaSceneRegistry sceneRegistry;
        private readonly IQaProfileService profileService;
        private readonly QaLeaseService leaseService;
        private readonly IQaInputDriver inputDriver;
        private readonly IQaEvidenceRecorder evidenceRecorder;
        private readonly Func<QaDriverSnapshot> captureSnapshot;
        private readonly Func<DateTime> utcNowProvider;
        private readonly string ownerId;
        private readonly TimeSpan leaseTtl;

        /// <param name="driver">QA run의 session.* 생애주기를 소유하는 게이트웨이(필수).</param>
        /// <param name="sceneRegistry">씬/대상/프리셋을 해석하는 레지스트리(필수).</param>
        /// <param name="profileService">일반 진행 PlayerPrefs를 격리하는 서비스(필수).</param>
        /// <param name="leaseService">단일 활성 writer를 강제하는 리스 서비스(필수).</param>
        /// <param name="inputDriver">클릭/드래그/키 입력을 실행하는 드라이버(필수).</param>
        /// <param name="evidenceRecorder">append-only evidence 기록기(필수).</param>
        /// <param name="captureSnapshot">
        /// 어서션 평가·evidence 첨부에 쓸 <see cref="QaDriverSnapshot"/>을 캡처하는 콜백(필수).
        /// <see cref="QaConditionWaiter"/>와 동일한 계약(<c>Func&lt;QaDriverSnapshot&gt;</c>)이므로,
        /// 실제 호출자는 보통 <c>QaStateProbe.Capture</c>를 그대로 넘깁니다.
        /// </param>
        /// <param name="ownerId">리스 발급 시 사용할 소유자 식별자. 생략하면 고정 기본값을 사용합니다.</param>
        /// <param name="leaseTtl">리스 TTL. 생략하면 10분을 사용합니다.</param>
        /// <param name="utcNowProvider">테스트용 시각 주입 훅. 생략하면 <see cref="DateTime.UtcNow"/> 사용.</param>
        public QaScenarioRunner(
            IQaDriver driver,
            QaSceneRegistry sceneRegistry,
            IQaProfileService profileService,
            QaLeaseService leaseService,
            IQaInputDriver inputDriver,
            IQaEvidenceRecorder evidenceRecorder,
            Func<QaDriverSnapshot> captureSnapshot,
            string ownerId = null,
            TimeSpan? leaseTtl = null,
            Func<DateTime> utcNowProvider = null)
        {
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.sceneRegistry = sceneRegistry ?? throw new ArgumentNullException(nameof(sceneRegistry));
            this.profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            this.leaseService = leaseService ?? throw new ArgumentNullException(nameof(leaseService));
            this.inputDriver = inputDriver ?? throw new ArgumentNullException(nameof(inputDriver));
            this.evidenceRecorder = evidenceRecorder ?? throw new ArgumentNullException(nameof(evidenceRecorder));
            this.captureSnapshot = captureSnapshot ?? throw new ArgumentNullException(nameof(captureSnapshot));
            this.ownerId = string.IsNullOrWhiteSpace(ownerId) ? DefaultOwnerId : ownerId;
            this.leaseTtl = leaseTtl ?? DefaultLeaseTtl;
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// 검증된 시나리오를 순차 실행합니다. 스텝은 선언된 순서로 하나씩 실행되며, 실패하거나
        /// 취소되면 그 자리에서 멈춥니다. 성공/실패/취소/예외 경로 모두에서 QA 프로필 복원과
        /// 리스 반납을 보장합니다(cleanup boundary).
        /// </summary>
        public async Task<QaScenarioRunOutcome> RunAsync(
            QaScenarioDefinition scenario, CancellationToken cancellationToken = default)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            DateTime startedAtUtc = utcNowProvider();
            var stepOutcomes = new List<QaScenarioStepOutcome>();

            QaCommandResult beginSessionResult = await SafeExecuteDriverAsync(
                QaCommand.BeginSession(scenario.Id), cancellationToken).ConfigureAwait(false);

            if (!beginSessionResult.IsSuccess)
            {
                return QaScenarioRunOutcome.Create(
                    scenario.Id, QaRunId.None, QaScenarioRunOutcomeCode.Failed,
                    "Failed to begin QA session: " + beginSessionResult.Message,
                    startedAtUtc, utcNowProvider(), stepOutcomes, null);
            }

            QaRunId runId = beginSessionResult.RunId;
            QaScenarioRunOutcomeCode outcomeCode;
            string outcomeMessage;
            QaDriverSnapshot lastSnapshot = null;
            bool leaseAcquired = false;
            QaLeaseId leaseId = QaLeaseId.None;
            bool profileBegun = false;

            try
            {
                (outcomeCode, outcomeMessage, lastSnapshot) = await ExecuteWithinLeaseAsync(
                    scenario, runId, stepOutcomes, cancellationToken,
                    onLeaseAcquired: id => { leaseAcquired = true; leaseId = id; },
                    onProfileBegun: () => profileBegun = true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                outcomeCode = QaScenarioRunOutcomeCode.Failed;
                outcomeMessage = "Unhandled exception while running scenario '" + scenario.Id + "': " + ex.GetType().Name;
            }
            finally
            {
                // Cleanup boundary (Task 9 Step 3): whatever happened above (success, failure,
                // cancellation, or an unexpected exception), the QA profile must be restored and
                // the execution lease must be released so the next run starts from a clean slate.
                if (profileBegun)
                {
                    SafeRestoreProfile(scenario.Id);
                }

                if (leaseAcquired)
                {
                    SafeReleaseLease(scenario.Id, leaseId);
                }
            }

            QaDriverSnapshot endSnapshot = SafeCaptureSnapshot() ?? lastSnapshot;
            SafeFinalizeEvidence(endSnapshot);

            QaCommandType sessionCloseType = outcomeCode == QaScenarioRunOutcomeCode.Passed
                ? QaCommandType.SessionEnd
                : QaCommandType.SessionAbort;
            await SafeExecuteDriverAsync(
                QaCommand.Create(scenario.Id, sessionCloseType), CancellationToken.None).ConfigureAwait(false);

            return QaScenarioRunOutcome.Create(
                scenario.Id, runId, outcomeCode, outcomeMessage, startedAtUtc, utcNowProvider(),
                stepOutcomes, lastSnapshot);
        }

        /// <summary>
        /// 리스 획득 → 프로필 시작 → 프리셋 적용 → 스텝 순차 실행까지, cleanup 경계 "안쪽"에서만
        /// 일어나야 하는 작업 전체. 호출자(<see cref="RunAsync"/>)가 finally에서 무엇을 되돌려야
        /// 하는지 알 수 있도록, 리스/프로필이 실제로 시작된 시점을 콜백으로 보고합니다.
        /// </summary>
        private async Task<(QaScenarioRunOutcomeCode Code, string Message, QaDriverSnapshot LastSnapshot)>
            ExecuteWithinLeaseAsync(
                QaScenarioDefinition scenario,
                QaRunId runId,
                List<QaScenarioStepOutcome> stepOutcomes,
                CancellationToken cancellationToken,
                Action<QaLeaseId> onLeaseAcquired,
                Action onProfileBegun)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return (QaScenarioRunOutcomeCode.Interrupted, "Cancelled before acquiring the QA execution lease.", null);
            }

            QaLeaseAcquireResult leaseResult = leaseService.TryAcquire(ownerId, runId, leaseTtl);
            if (!leaseResult.IsAcquired)
            {
                return (QaScenarioRunOutcomeCode.Failed,
                    "Failed to acquire QA execution lease: " + leaseResult.Message, null);
            }

            onLeaseAcquired(leaseResult.Lease.LeaseId);

            QaProfileOperationResult profileResult = profileService.BeginQaProfile(runId);
            if (!profileResult.IsSuccess)
            {
                return (QaScenarioRunOutcomeCode.Failed,
                    "Failed to begin QA profile: " + profileResult.Message, null);
            }

            onProfileBegun();

            QaDriverSnapshot beginSnapshot = SafeCaptureSnapshot();
            QaEvidenceOperationResult evidenceBeginResult = SafeBeginEvidence(runId, beginSnapshot);
            if (!evidenceBeginResult.IsSuccess)
            {
                AppendNoteSafely(scenario.Id, "Evidence BeginRun did not succeed: " + evidenceBeginResult.Message);
            }

            if (!sceneRegistry.TryResolveScene(scenario.Scene, out IQaSceneAdapter sceneAdapter))
            {
                return (QaScenarioRunOutcomeCode.Failed,
                    "Scenario references unknown scene '" + scenario.Scene + "'.", beginSnapshot);
            }

            if (!string.IsNullOrWhiteSpace(scenario.Preset))
            {
                QaScenePresetResult presetResult = sceneAdapter.ApplyPreset(scenario.Preset);
                if (!presetResult.IsSuccess)
                {
                    return (QaScenarioRunOutcomeCode.Failed,
                        "Failed to apply preset '" + scenario.Preset + "': " + presetResult.Message, beginSnapshot);
                }
            }

            QaDriverSnapshot lastSnapshot = beginSnapshot;

            foreach (QaScenarioStepDefinition step in scenario.Steps)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return (QaScenarioRunOutcomeCode.Interrupted,
                        "Cancelled before executing step '" + step.Id + "'.", lastSnapshot);
                }

                QaScenarioStepOutcome stepOutcome = await ExecuteStepAsync(step, cancellationToken).ConfigureAwait(false);
                stepOutcomes.Add(stepOutcome);
                AppendStepEvidence(step, stepOutcome);

                if (stepOutcome.Snapshot != null)
                {
                    lastSnapshot = stepOutcome.Snapshot;
                }

                if (stepOutcome.WasCancelled)
                {
                    return (QaScenarioRunOutcomeCode.Interrupted,
                        "Step '" + step.Id + "' was cancelled: " + stepOutcome.Message, lastSnapshot);
                }

                if (!stepOutcome.IsSuccess)
                {
                    return (QaScenarioRunOutcomeCode.Failed,
                        "Step '" + step.Id + "' failed: " + stepOutcome.Message, lastSnapshot);
                }
            }

            return (QaScenarioRunOutcomeCode.Passed, "All steps passed.", lastSnapshot);
        }

        // -----------------------------------------------------------------------------------
        //  Step execution
        // -----------------------------------------------------------------------------------

        private async Task<QaScenarioStepOutcome> ExecuteStepAsync(
            QaScenarioStepDefinition step, CancellationToken cancellationToken)
        {
            if (step == null)
            {
                return QaScenarioStepOutcome.Failed("(null)", "Step must not be null.");
            }

            if (step.TimeoutMs <= 0
                || string.IsNullOrWhiteSpace(step.Command)
                || !QaScenarioSchema.CommandKindsByName.TryGetValue(step.Command, out QaScenarioCommandKind commandKind))
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Step is not executable (invalid command/timeout).");
            }

            TimeSpan timeout = TimeSpan.FromMilliseconds(step.TimeoutMs);

            switch (commandKind)
            {
                case QaScenarioCommandKind.InteractionPointer:
                    return await ExecutePointerAsync(step, timeout, cancellationToken).ConfigureAwait(false);
                case QaScenarioCommandKind.InteractionDrag:
                    return await ExecuteDragAsync(step, timeout, cancellationToken).ConfigureAwait(false);
                case QaScenarioCommandKind.InteractionKey:
                    return await ExecuteKeyAsync(step, timeout, cancellationToken).ConfigureAwait(false);
                case QaScenarioCommandKind.StateAssert:
                    return await ExecuteAssertAsync(step, timeout, cancellationToken).ConfigureAwait(false);
                default:
                    return QaScenarioStepOutcome.Failed(step.Id, "Command '" + step.Command + "' is not executable yet.");
            }
        }

        private async Task<QaScenarioStepOutcome> ExecutePointerAsync(
            QaScenarioStepDefinition step, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!QaTargetId.TryCreate(step.Target, out QaTargetId targetId, out string targetError))
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Invalid target: " + targetError);
            }

            return await RunInputAsync(
                step.Id, timeout, cancellationToken,
                (token) => inputDriver.ClickAsync(targetId, token)).ConfigureAwait(false);
        }

        private async Task<QaScenarioStepOutcome> ExecuteDragAsync(
            QaScenarioStepDefinition step, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!QaTargetId.TryCreate(step.Target, out QaTargetId sourceId, out string sourceError))
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Invalid target: " + sourceError);
            }

            if (!QaTargetId.TryCreate(step.DestinationTarget, out QaTargetId destinationId, out string destinationError))
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Invalid destinationTarget: " + destinationError);
            }

            return await RunInputAsync(
                step.Id, timeout, cancellationToken,
                (token) => inputDriver.DragAsync(sourceId, destinationId, token)).ConfigureAwait(false);
        }

        private async Task<QaScenarioStepOutcome> ExecuteKeyAsync(
            QaScenarioStepDefinition step, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (!QaTargetId.TryCreate(step.Target, out QaTargetId targetId, out string targetError))
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Invalid target: " + targetError);
            }

            string text = step.Text ?? string.Empty;
            return await RunInputAsync(
                step.Id, timeout, cancellationToken,
                (token) => inputDriver.KeyAsync(targetId, text, token)).ConfigureAwait(false);
        }

        private async Task<QaScenarioStepOutcome> RunInputAsync(
            string stepId,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<QaInputResult>> invokeDriver)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            QaInputResult result;
            try
            {
                result = await invokeDriver(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return cancellationToken.IsCancellationRequested
                    ? QaScenarioStepOutcome.Cancelled(stepId, "Interaction was cancelled.")
                    : QaScenarioStepOutcome.Failed(stepId, "Interaction timed out after " + timeout + ".");
            }
            catch (Exception ex)
            {
                return QaScenarioStepOutcome.Failed(stepId, "Input driver threw " + ex.GetType().Name + ".");
            }

            if (result.Code == QaInputResultCode.Cancelled)
            {
                return QaScenarioStepOutcome.Cancelled(stepId, result.Message);
            }

            return result.IsSuccess
                ? QaScenarioStepOutcome.Success(stepId, result.Message)
                : QaScenarioStepOutcome.Failed(stepId, result.Message);
        }

        private async Task<QaScenarioStepOutcome> ExecuteAssertAsync(
            QaScenarioStepDefinition step, TimeSpan timeout, CancellationToken cancellationToken)
        {
            QaAssertion assertion;
            try
            {
                assertion = step.Assertion?.ToAssertion();
            }
            catch (Exception ex)
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Invalid assertion: " + ex.Message);
            }

            if (assertion == null)
            {
                return QaScenarioStepOutcome.Failed(step.Id, "Step declares state.assert without an 'assertion' object.");
            }

            var waiter = new QaConditionWaiter(captureSnapshot, utcNowProvider);
            QaWaitResult waitResult = await waiter.WaitUntilAsync(assertion, timeout, baseline: null, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            switch (waitResult.Code)
            {
                case QaWaitResultCode.Passed:
                    return QaScenarioStepOutcome.Success(
                        step.Id, waitResult.LastAssertionResult?.Message, waitResult.FinalSnapshot);
                case QaWaitResultCode.Cancelled:
                    return QaScenarioStepOutcome.Cancelled(
                        step.Id, "Assertion wait was cancelled.", waitResult.FinalSnapshot);
                default:
                    return QaScenarioStepOutcome.Failed(
                        step.Id,
                        waitResult.LastAssertionResult?.Message ?? "Assertion timed out after " + timeout + ".",
                        waitResult.FinalSnapshot);
            }
        }

        // -----------------------------------------------------------------------------------
        //  Fail-safe helpers (never let a dependency exception escape RunAsync's cleanup path)
        // -----------------------------------------------------------------------------------

        private async Task<QaCommandResult> SafeExecuteDriverAsync(QaCommand command, CancellationToken cancellationToken)
        {
            try
            {
                return await driver.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return QaCommandResult.Create(
                    command?.Id, QaRunId.None, 0, QaResultCode.InternalError,
                    "QaScenarioRunner: driver threw " + ex.GetType().Name + ".");
            }
        }

        private QaDriverSnapshot SafeCaptureSnapshot()
        {
            try
            {
                return captureSnapshot();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaScenarioRunner] captureSnapshot threw: " + ex.GetType().Name);
                return null;
            }
        }

        private void SafeRestoreProfile(string scenarioId)
        {
            try
            {
                QaProfileOperationResult result = profileService.RestorePreviousProfile();
                if (!result.IsSuccess)
                {
                    AppendNoteSafely(scenarioId, "Failed to restore QA profile: " + result.Message);
                }
            }
            catch (Exception ex)
            {
                AppendNoteSafely(scenarioId, "Restoring QA profile threw " + ex.GetType().Name + ".");
            }
        }

        private void SafeReleaseLease(string scenarioId, QaLeaseId leaseId)
        {
            try
            {
                QaLeaseOperationResult result = leaseService.Release(leaseId);
                if (!result.IsSuccess)
                {
                    AppendNoteSafely(scenarioId, "Failed to release QA execution lease: " + result.Message);
                }
            }
            catch (Exception ex)
            {
                AppendNoteSafely(scenarioId, "Releasing QA execution lease threw " + ex.GetType().Name + ".");
            }
        }

        private QaEvidenceOperationResult SafeBeginEvidence(QaRunId runId, QaDriverSnapshot beginSnapshot)
        {
            try
            {
                return evidenceRecorder.BeginRun(runId.ToString(), beginSnapshot);
            }
            catch (Exception ex)
            {
                return QaEvidenceOperationResult.InternalError(
                    "Evidence BeginRun threw " + ex.GetType().Name + ".");
            }
        }

        private void SafeFinalizeEvidence(QaDriverSnapshot endSnapshot)
        {
            try
            {
                evidenceRecorder.Finalize(endSnapshot);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaScenarioRunner] Evidence Finalize threw: " + ex.GetType().Name);
            }
        }

        private void AppendNoteSafely(string commandId, string message)
        {
            try
            {
                evidenceRecorder.AppendEvent(QaEvidenceEvent.Create(QaEvidenceEventType.Note, commandId, message: message));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaScenarioRunner] Evidence AppendEvent threw: " + ex.GetType().Name);
            }
        }

        private void AppendStepEvidence(QaScenarioStepDefinition step, QaScenarioStepOutcome outcome)
        {
            if (outcome.WasCancelled)
            {
                AppendNoteSafely(step.Id, "Step '" + step.Id + "' cancelled: " + outcome.Message);
                return;
            }

            try
            {
                if (string.Equals(step.Command, QaScenarioSchema.CommandStateAssert, StringComparison.Ordinal))
                {
                    evidenceRecorder.AppendEvent(QaEvidenceEvent.ForAssertion(step.Id, outcome.IsSuccess, outcome.Message));
                }
                else
                {
                    evidenceRecorder.AppendEvent(QaEvidenceEvent.ForCommandResult(
                        step.Id, outcome.IsSuccess ? "Success" : "Failed", outcome.Message));
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaScenarioRunner] Evidence AppendEvent threw: " + ex.GetType().Name);
            }
        }
    }
}
#endif
