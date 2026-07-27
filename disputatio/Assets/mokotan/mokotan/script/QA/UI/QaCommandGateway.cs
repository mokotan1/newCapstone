#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenarios;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.Gateway
{
    /// <summary><see cref="QaCommandGateway"/> 연산 한 건이 보고할 수 있는 명시적 결과 코드.</summary>
    public enum QaGatewayOperationCode
    {
        /// <summary>
        /// 요청이 실제로 실행되었습니다. <c>RunScenarioAsync</c>의 경우 시나리오 자체가
        /// 통과했는지는 별도의 <see cref="QaGatewayRunResult.Outcome"/>을 확인해야 합니다 —
        /// "요청을 실행했다"와 "시나리오가 통과했다"는 서로 다른 질문입니다.
        /// </summary>
        Success,
        InvalidRequest,
        NotFound,
        AlreadyRunning,

        /// <summary>대상 연산(취소/증거 첨부 등)이 요구하는 활성 상태(run 등)가 지금 없습니다.</summary>
        NotActive,
        InternalError
    }

    /// <summary>qa_status가 보고하는 게이트웨이 상태의 불변 스냅샷.</summary>
    public sealed class QaGatewayStatusSnapshot
    {
        public bool IsQaProfileActive { get; }
        public bool IsScenarioRunning { get; }
        public string ActiveScenarioId { get; }
        public string ActiveRunId { get; }
        public string EvidenceRunDirectoryPath { get; }
        public IReadOnlyCollection<string> RegisteredSceneNames { get; }

        private QaGatewayStatusSnapshot(
            bool isQaProfileActive,
            bool isScenarioRunning,
            string activeScenarioId,
            string activeRunId,
            string evidenceRunDirectoryPath,
            IReadOnlyCollection<string> registeredSceneNames)
        {
            IsQaProfileActive = isQaProfileActive;
            IsScenarioRunning = isScenarioRunning;
            ActiveScenarioId = activeScenarioId ?? string.Empty;
            ActiveRunId = activeRunId ?? string.Empty;
            EvidenceRunDirectoryPath = evidenceRunDirectoryPath ?? string.Empty;
            RegisteredSceneNames = registeredSceneNames ?? Array.Empty<string>();
        }

        public static QaGatewayStatusSnapshot Create(
            bool isQaProfileActive,
            bool isScenarioRunning,
            string activeScenarioId,
            string activeRunId,
            string evidenceRunDirectoryPath,
            IReadOnlyCollection<string> registeredSceneNames)
        {
            return new QaGatewayStatusSnapshot(
                isQaProfileActive, isScenarioRunning, activeScenarioId, activeRunId,
                evidenceRunDirectoryPath, registeredSceneNames);
        }
    }

    /// <summary>qa_list가 보고하는 시나리오 한 건의 불변 요약(검증 통과/실패 모두 포함).</summary>
    public sealed class QaGatewayScenarioSummary
    {
        public string ScenarioId { get; }
        public string Scene { get; }
        public string Preset { get; }
        public int StepCount { get; }
        public bool IsValid { get; }
        public IReadOnlyList<string> ValidationErrors { get; }

        private QaGatewayScenarioSummary(
            string scenarioId,
            string scene,
            string preset,
            int stepCount,
            bool isValid,
            IReadOnlyList<string> validationErrors)
        {
            ScenarioId = scenarioId ?? string.Empty;
            Scene = scene ?? string.Empty;
            Preset = preset ?? string.Empty;
            StepCount = stepCount;
            IsValid = isValid;
            ValidationErrors = validationErrors ?? Array.Empty<string>();
        }

        public static QaGatewayScenarioSummary ForValid(QaScenarioDefinition scenario)
        {
            return new QaGatewayScenarioSummary(
                scenario.Id, scenario.Scene, scenario.Preset,
                scenario.Steps != null ? scenario.Steps.Count : 0, true, Array.Empty<string>());
        }

        public static QaGatewayScenarioSummary ForInvalid(string sourceName, IReadOnlyList<string> errors)
        {
            return new QaGatewayScenarioSummary(sourceName, string.Empty, string.Empty, 0, false, errors);
        }
    }

    /// <summary>
    /// <see cref="QaCommandGateway.RunScenarioAsync"/> 호출 한 건의 불변 결과. "요청을
    /// 실행했는가"(<see cref="Code"/>)와 "시나리오가 통과했는가"(<see cref="Outcome"/>)를 분리
    /// 합니다 — 요청 실행 자체는 성공했어도 시나리오는 실패/취소로 끝날 수 있기 때문입니다.
    /// </summary>
    public sealed class QaGatewayRunResult
    {
        public QaGatewayOperationCode Code { get; }
        public string Message { get; }

        /// <summary><see cref="Code"/>가 <see cref="QaGatewayOperationCode.Success"/>일 때만 값이 있습니다.</summary>
        public QaScenarioRunOutcome Outcome { get; }

        private QaGatewayRunResult(QaGatewayOperationCode code, string message, QaScenarioRunOutcome outcome)
        {
            Code = code;
            Message = message ?? string.Empty;
            Outcome = outcome;
        }

        /// <summary>요청 실행 자체가 성공했는지 여부(시나리오 pass/fail과는 무관).</summary>
        public bool IsSuccess
        {
            get { return Code == QaGatewayOperationCode.Success; }
        }

        public static QaGatewayRunResult Completed(QaScenarioRunOutcome outcome)
        {
            return new QaGatewayRunResult(QaGatewayOperationCode.Success, outcome?.Message, outcome);
        }

        public static QaGatewayRunResult Failure(QaGatewayOperationCode code, string message)
        {
            return new QaGatewayRunResult(code, message, null);
        }
    }

    /// <summary>
    /// <see cref="QaCommandGateway.CancelActiveRun"/>/<see cref="QaCommandGateway.CaptureEvidence"/>/
    /// <see cref="QaCommandGateway.Recover"/> 호출 한 건의 불변 결과.
    /// </summary>
    public sealed class QaGatewayOperationResult
    {
        public QaGatewayOperationCode Code { get; }
        public string Message { get; }

        private QaGatewayOperationResult(QaGatewayOperationCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess
        {
            get { return Code == QaGatewayOperationCode.Success; }
        }

        public static QaGatewayOperationResult Success(string message)
        {
            return new QaGatewayOperationResult(QaGatewayOperationCode.Success, message);
        }

        public static QaGatewayOperationResult Failure(QaGatewayOperationCode code, string message)
        {
            return new QaGatewayOperationResult(code, message);
        }
    }

    /// <summary>
    /// Unity CLI 게이트웨이(<c>qa_status</c>/<c>qa_list</c>/<c>qa_run</c>/<c>qa_cancel</c>/
    /// <c>qa_capture</c>/<c>qa_recover</c>)와 사람이 조작하는 개발자 패널(<c>QaDeveloperPanel</c>)이
    /// 공유하는 단 하나의 QA 오케스트레이션 경계(Task 10). 두 어댑터 모두:
    ///
    /// 1) 동일한 <c>Build*Command</c> 정적 메서드로 동일한 <see cref="QaCommand"/> DTO를 구성하고
    ///    (계약 테스트가 이 지점에서 CLI 인자 파싱과 패널 필드 수집이 동일한 명령을 만드는지
    ///    검증합니다),
    /// 2) 동일한 인스턴스 메서드(<see cref="GetStatus"/>/<see cref="ListScenarios"/>/
    ///    <see cref="RunScenarioAsync"/>/<see cref="CancelActiveRun"/>/<see cref="CaptureEvidence"/>/
    ///    <see cref="Recover"/>)로 실제 QA 서비스(<see cref="QaScenarioRunner"/>,
    ///    <see cref="QaLeaseService"/>, <see cref="IQaProfileService"/>,
    ///    <see cref="IQaEvidenceRecorder"/>)를 호출합니다.
    ///
    /// <see cref="QaScenarioRunner"/>가 이미 스텝 실행 중 자신만의 리스를 직접 획득하므로
    /// (Task 9), 이 게이트웨이는 <see cref="QaDriverCore.ExecuteAsync"/>를 <c>session.*</c>
    /// 생애주기 이외의 용도로 호출하지 않습니다 — <c>scenario.run</c>/<c>scenario.cancel</c>/
    /// <c>scenario.status</c> <see cref="QaCommandType"/> 값은 아직 드라이버 디스패치에 연결되지
    /// 않은 예약값이므로(향후 태스크), 이 클래스가 직접 소유하는 별도 실행 경로로 처리합니다.
    /// </summary>
    public sealed class QaCommandGateway : IDisposable
    {
        private const string DefaultScenarioResourcesPath = "QA/Scenarios";
        private const string DefaultOwnerId = "qa-command-gateway";
        private const string TimeoutMsParameterKey = "timeoutMs";

        private readonly QaDriverCore driver;
        private readonly QaSceneRegistry sceneRegistry;
        private readonly IQaProfileService profileService;
        private readonly QaLeaseService leaseService;
        private readonly IQaInputDriver inputDriver;
        private readonly IQaEvidenceRecorder evidenceRecorder;
        private readonly QaScenarioValidator scenarioValidator;
        private readonly QaScenarioRunner scenarioRunner;
        private readonly Func<string> evidenceRunDirectoryProvider;
        private readonly Func<IReadOnlyList<(string Name, string Json)>> scenarioSourceProvider;

        private readonly object sync = new object();
        private CancellationTokenSource activeRunCts;
        private Task<QaScenarioRunOutcome> activeRunTask;
        private string activeScenarioId = string.Empty;
        private string activeRunId = string.Empty;

        /// <param name="evidenceRecorder">
        /// append-only evidence 기록기(필수). 호출자가 Editor(<c>EditorQaEvidenceRecorder</c>) 또는
        /// development player(<c>DevelopmentQaEvidenceRecorder</c>) 저장 전략을 선택합니다(DIP).
        /// </param>
        /// <param name="evidenceRunDirectoryProvider">
        /// <see cref="GetStatus"/>가 보고할 현재 run의 절대 디렉터리 경로를 조회하는 콜백.
        /// <see cref="IQaEvidenceRecorder"/> 인터페이스 자체는 이 경로를 노출하지 않으므로(구현체별로
        /// 다름), 이 게이트웨이가 강제로 특정 구현 타입에 캐스팅하지 않도록 호출자가 주입합니다.
        /// 생략하면 <see cref="QaGatewayStatusSnapshot.EvidenceRunDirectoryPath"/>는 항상 빈 문자열입니다.
        /// </param>
        /// <param name="scenarioSourceProvider">
        /// (이름, 원본 JSON) 쌍의 목록을 반환하는 콜백. 생략하면
        /// <c>Resources.LoadAll&lt;TextAsset&gt;("QA/Scenarios")</c>를 사용합니다(테스트에서는
        /// Unity Resources를 건드리지 않는 인메모리 목록을 주입할 수 있습니다).
        /// </param>
        /// <param name="captureScreenshotPng">
        /// <c>evidence.capture</c> 스텝과 <see cref="QaScenarioRunner"/>의 Finalize 직전 안전망이
        /// 사용할 PNG 캡처 콜백(Task: manifest PASS 근본 원인 수정 — <c>qa_run</c>은 동기적으로
        /// 끝나므로 실행 종료 후 별도 <c>qa_capture</c> 호출로는 늦습니다). Editor 호출자는
        /// <c>QaEditorCommandGatewayInstaller</c>에서 실제 Game/Scene view 캡처를 주입합니다.
        /// 생략하면(<c>null</c>) <c>evidence.capture</c> 스텝은 가짜 evidence를 만들지 않고
        /// 명시적으로 실패합니다.
        /// </param>
        /// <param name="inputDriver">
        /// Optional override for the scenario runner input driver. When omitted, uses API mode.
        /// Pass a RealInput (<see cref="QaEventSystemInputDriver"/>) driver when pointer steps
        /// must exercise EventSystem (design §6.2); callers may use
        /// <see cref="SceneAdapters.DeveloperQaServiceFactory.TryCreateRealInputDriver"/>.
        /// </param>
        /// <param name="realInputDriver">
        /// Optional RealInput driver retained for diagnostics/status; when <paramref name="inputDriver"/>
        /// is null and this is non-null, the runner uses RealInput instead of API.
        /// </param>
        public QaCommandGateway(
            IQaEvidenceRecorder evidenceRecorder,
            Func<string> evidenceRunDirectoryProvider = null,
            Func<IReadOnlyList<(string Name, string Json)>> scenarioSourceProvider = null,
            QaSceneRegistry sceneRegistry = null,
            IQaProfileService profileService = null,
            QaLeaseService leaseService = null,
            Func<byte[]> captureScreenshotPng = null,
            IQaInputDriver inputDriver = null,
            IQaInputDriver realInputDriver = null)
        {
            this.evidenceRecorder = evidenceRecorder ?? throw new ArgumentNullException(nameof(evidenceRecorder));
            this.evidenceRunDirectoryProvider = evidenceRunDirectoryProvider;
            this.scenarioSourceProvider = scenarioSourceProvider ?? LoadScenarioResourcesFromUnity;
            this.sceneRegistry = sceneRegistry ?? new QaSceneRegistry();
            this.profileService = profileService ?? CreateFallbackProfileService();
            this.leaseService = leaseService ?? new QaLeaseService(QaFileLeaseRecoveryStore.CreateDefault());
            this.driver = new QaDriverCore(leaseGate: this.leaseService);
            this.scenarioValidator = new QaScenarioValidator(this.sceneRegistry);
            this.inputDriver = inputDriver
                ?? realInputDriver
                ?? new QaApiInputDriver(ResolveInteractable);
            this.scenarioRunner = new QaScenarioRunner(
                this.driver,
                this.sceneRegistry,
                this.profileService,
                this.leaseService,
                this.inputDriver,
                this.evidenceRecorder,
                captureSnapshot: () => new QaStateProbe().Capture(),
                ownerId: DefaultOwnerId,
                captureScreenshotPng: captureScreenshotPng);
        }

        // -----------------------------------------------------------------------------------
        //  Shared command builders (Task 10 §Step 1 contract boundary)
        //
        //  Both QaUnityCliTools (Editor-only JObject parsing) and QaDeveloperPanel (OnGUI field
        //  collection) must call these same static methods with the same inputs to produce
        //  byte-for-byte identical QaCommand DTOs. Neither adapter is allowed to construct a
        //  QaCommand directly.
        // -----------------------------------------------------------------------------------

        public static QaCommand BuildStatusCommand(string commandId)
        {
            return QaCommand.Create(commandId, QaCommandType.ScenarioStatus);
        }

        public static QaCommand BuildRunCommand(string commandId, string scenarioId, TimeSpan overallTimeout)
        {
            long timeoutMs = overallTimeout > TimeSpan.Zero ? (long)overallTimeout.TotalMilliseconds : 0L;
            var parameters = new Dictionary<string, string>
            {
                [TimeoutMsParameterKey] = timeoutMs.ToString(CultureInfo.InvariantCulture)
            };

            return QaCommand.Create(commandId, QaCommandType.ScenarioRun, scenarioId, parameters);
        }

        public static QaCommand BuildCancelCommand(string commandId, string scenarioId)
        {
            return QaCommand.Create(commandId, QaCommandType.ScenarioCancel, scenarioId);
        }

        public static QaCommand BuildCaptureCommand(string commandId, string scenarioId)
        {
            return QaCommand.Create(commandId, QaCommandType.EvidenceCapture, scenarioId);
        }

        // -----------------------------------------------------------------------------------
        //  qa_status
        // -----------------------------------------------------------------------------------

        public QaGatewayStatusSnapshot GetStatus()
        {
            lock (sync)
            {
                bool isRunning = activeRunTask != null && !activeRunTask.IsCompleted;

                return QaGatewayStatusSnapshot.Create(
                    profileService.IsQaProfileActive,
                    isRunning,
                    isRunning ? activeScenarioId : string.Empty,
                    isRunning ? activeRunId : string.Empty,
                    SafeGetEvidenceRunDirectoryPath(),
                    sceneRegistry.RegisteredSceneNames);
            }
        }

        // -----------------------------------------------------------------------------------
        //  qa_list (catalog operation; deliberately outside the QaCommand DTO taxonomy — see
        //  class remarks and Task 10 design notes: there is no "scenario.list" QaCommandType).
        // -----------------------------------------------------------------------------------

        public IReadOnlyList<QaGatewayScenarioSummary> ListScenarios()
        {
            IReadOnlyList<(string Name, string Json)> sources = SafeLoadScenarioSources();
            var summaries = new List<QaGatewayScenarioSummary>(sources.Count);

            foreach ((string name, string json) in sources)
            {
                QaScenarioValidationResult validation = scenarioValidator.Validate(json);
                summaries.Add(validation.IsValid
                    ? QaGatewayScenarioSummary.ForValid(validation.Scenario)
                    : QaGatewayScenarioSummary.ForInvalid(name, validation.Errors));
            }

            return summaries;
        }

        // -----------------------------------------------------------------------------------
        //  qa_run
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// 검증된 시나리오를 실행합니다. 리스/프로필 획득은 전적으로 <see cref="QaScenarioRunner"/>가
        /// 소유하므로(Task 9), 유효한 리스 없이는 실제 mutation이 절대 일어나지 않습니다 — 이
        /// 게이트웨이는 그 위에 "동시에 하나의 run만" 규칙과 선택적 전체 타임아웃만 추가합니다.
        /// </summary>
        /// <param name="overallTimeout">
        /// 전체 run의 최대 시간. <see cref="TimeSpan.Zero"/> 이하이면 개별 스텝의
        /// <c>timeoutMs</c>만 적용되고 전체 타임아웃은 걸리지 않습니다.
        /// </param>
        public async Task<QaGatewayRunResult> RunScenarioAsync(
            string scenarioId, TimeSpan overallTimeout, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return QaGatewayRunResult.Failure(
                    QaGatewayOperationCode.InvalidRequest, "scenarioId must not be blank.");
            }

            QaScenarioDefinition scenario = TryFindValidatedScenario(scenarioId, out string notFoundReason);
            if (scenario == null)
            {
                return QaGatewayRunResult.Failure(QaGatewayOperationCode.NotFound, notFoundReason);
            }

            Task<QaScenarioRunOutcome> runTask;
            lock (sync)
            {
                if (activeRunTask != null && !activeRunTask.IsCompleted)
                {
                    return QaGatewayRunResult.Failure(
                        QaGatewayOperationCode.AlreadyRunning,
                        "A QA scenario run ('" + activeScenarioId + "') is already active. Cancel it first.");
                }

                activeRunCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (overallTimeout > TimeSpan.Zero)
                {
                    activeRunCts.CancelAfter(overallTimeout);
                }

                activeScenarioId = scenarioId;
                activeRunId = string.Empty;
                runTask = scenarioRunner.RunAsync(scenario, activeRunCts.Token);
                activeRunTask = runTask;
            }

            try
            {
                QaScenarioRunOutcome outcome = await runTask.ConfigureAwait(false);

                lock (sync)
                {
                    activeRunId = outcome.RunId.ToString();
                }

                return QaGatewayRunResult.Completed(outcome);
            }
            catch (Exception ex)
            {
                return QaGatewayRunResult.Failure(
                    QaGatewayOperationCode.InternalError, "QaScenarioRunner threw " + ex.GetType().Name + ".");
            }
            finally
            {
                lock (sync)
                {
                    activeRunCts?.Dispose();
                    activeRunCts = null;
                    activeScenarioId = string.Empty;
                }
            }
        }

        // -----------------------------------------------------------------------------------
        //  qa_cancel
        // -----------------------------------------------------------------------------------

        public QaGatewayOperationResult CancelActiveRun()
        {
            CancellationTokenSource ctsToCancel;
            string scenarioId;

            lock (sync)
            {
                if (activeRunTask == null || activeRunTask.IsCompleted || activeRunCts == null)
                {
                    return QaGatewayOperationResult.Failure(
                        QaGatewayOperationCode.NotActive, "No active QA scenario run to cancel.");
                }

                ctsToCancel = activeRunCts;
                scenarioId = activeScenarioId;
            }

            try
            {
                ctsToCancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run finished (and disposed its token source) between the check above and
                // this call; that race means there is simply nothing left to cancel.
                return QaGatewayOperationResult.Failure(
                    QaGatewayOperationCode.NotActive, "No active QA scenario run to cancel.");
            }

            return QaGatewayOperationResult.Success(
                "Cancellation requested for QA scenario run '" + scenarioId + "'.");
        }

        // -----------------------------------------------------------------------------------
        //  qa_capture
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// 스크린샷 바이트를 현재 활성 run의 evidence에 첨부합니다. 실제 화면 캡처 방법(Editor
        /// SceneView/GameView 카메라, 런타임 <c>ScreenCapture</c> 등)은 호출자마다 다르므로, 이
        /// 게이트웨이는 이미 인코딩된 PNG 바이트만 받습니다(SRP).
        /// </summary>
        public QaGatewayOperationResult CaptureEvidence(string commandId, byte[] pngBytes, string fileNameHint = null)
        {
            bool hasActiveRun;
            lock (sync)
            {
                hasActiveRun = activeRunTask != null && !activeRunTask.IsCompleted;
            }

            if (!hasActiveRun)
            {
                return QaGatewayOperationResult.Failure(
                    QaGatewayOperationCode.NotActive,
                    "Evidence capture requires an active QA scenario run; call qa_run first.");
            }

            if (pngBytes == null || pngBytes.Length == 0)
            {
                return QaGatewayOperationResult.Failure(
                    QaGatewayOperationCode.InvalidRequest, "pngBytes must not be null or empty.");
            }

            QaEvidenceOperationResult result;
            try
            {
                result = evidenceRecorder.AttachScreenshot(commandId, pngBytes, fileNameHint);
            }
            catch (Exception ex)
            {
                return QaGatewayOperationResult.Failure(
                    QaGatewayOperationCode.InternalError, "Evidence recorder threw " + ex.GetType().Name + ".");
            }

            return result.IsSuccess
                ? QaGatewayOperationResult.Success(result.Message)
                : QaGatewayOperationResult.Failure(QaGatewayOperationCode.InternalError, result.Message);
        }

        // -----------------------------------------------------------------------------------
        //  qa_recover (service-level operation; deliberately outside the QaCommand DTO taxonomy —
        //  recovering a lease/profile is not one of the fixed QaCommandType values).
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// 이전 프로세스가 남긴 미해소 QA 프로필/리스 마커를 해소합니다. 두 서비스 모두
        /// 독립적으로 복구를 보고하므로(하나가 실패해도 나머지는 계속 진행), 결과 메시지에
        /// 각각의 결과가 모두 담깁니다.
        /// </summary>
        public QaGatewayOperationResult Recover(string recoveringOwnerId)
        {
            string effectiveOwnerId = string.IsNullOrWhiteSpace(recoveringOwnerId) ? DefaultOwnerId : recoveringOwnerId;
            var notes = new List<string>(2);

            try
            {
                QaProfileOperationResult profileResult = profileService.RecoverInterruptedSession();
                notes.Add("Profile: " + profileResult.Message);
            }
            catch (Exception ex)
            {
                notes.Add("Profile recovery threw " + ex.GetType().Name + ".");
            }

            notes.Add("Lease: " + RecoverLeaseBlocker(effectiveOwnerId));

            return QaGatewayOperationResult.Success(string.Join(" | ", notes));
        }

        /// <summary>
        /// <see cref="QaLeaseService"/>는 "지금 회수해야 할 블로커가 있는가"를 직접 조회하는 API를
        /// 노출하지 않으므로, 짧은 <see cref="QaLeaseService.TryAcquire"/> 프로브로 관찰합니다.
        /// 프로브가 성공하면(블로커 없음) 즉시 반납하고, 만료/미해소 블로커가 있으면
        /// <see cref="QaLeaseService.RecoverExpiredLease"/>로 명시적으로 정리합니다. 다른 프로세스가
        /// 아직 유효한 리스를 쥐고 있으면(진짜 실행 중) 그 사실만 보고하고 아무것도 강제하지 않습니다.
        /// </summary>
        private string RecoverLeaseBlocker(string effectiveOwnerId)
        {
            try
            {
                QaLeaseAcquireResult probe = leaseService.TryAcquire(
                    effectiveOwnerId, QaRunId.NewId(), TimeSpan.FromSeconds(1));

                if (probe.IsAcquired)
                {
                    leaseService.Release(probe.Lease.LeaseId);
                    return "no blocker found.";
                }

                if (probe.Code == QaLeaseAcquireResultCode.RecoveryRequired && probe.Blocker != null)
                {
                    QaLeaseRecoveryResult recovery =
                        leaseService.RecoverExpiredLease(probe.Blocker.LeaseId, effectiveOwnerId);
                    return recovery.Message;
                }

                return probe.Message;
            }
            catch (Exception ex)
            {
                return "recovery threw " + ex.GetType().Name + ".";
            }
        }

        // -----------------------------------------------------------------------------------
        //  Internal helpers
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// <see cref="QaProfileService"/>(진짜 PlayerPrefs 격리 구현)는 <c>Assembly-CSharp</c>에
        /// 존재하며 그 안에서 <c>PlayDataPrefsCleaner</c>(오디오/비디오 설정 카탈로그)에
        /// 의존하므로, 이 custom asmdef(<c>Godlotto.QA.UI</c>)에서는 직접 생성할 수 없습니다
        /// (custom asmdef는 default assembly를 참조할 수 없음 — 순환 참조가 되기 때문).
        /// 그래서 실제 <see cref="IQaProfileService"/>는 항상 호출자가 주입해야 합니다:
        /// Editor에서는 <c>QaEditorCommandGatewayInstaller</c>가, standalone development
        /// player에서는 <c>DeveloperModeController</c>(Assembly-CSharp, 그 타입에 접근 가능)가
        /// 각각 진짜 <c>QaProfileService</c>를 생성해 주입합니다.
        ///
        /// 이 메서드는 두 설치 경로 중 어느 쪽도 실행되지 않은 마지막 안전망입니다 — 조용히
        /// PlayerPrefs를 격리한 척(성공을 거짓 보고)하지 않고, 모든 mutation 연산을 명시적으로
        /// 거부합니다(Fail-Safe: 진짜 격리가 보장되지 않으면 어떤 QA 실행도 플레이어의 진행
        /// 데이터를 건드리게 두지 않습니다).
        /// </summary>
        private static IQaProfileService CreateFallbackProfileService()
        {
            Debug.LogWarning(
                "[QaCommandGateway] No IQaProfileService was injected and no real default is " +
                "reachable from this assembly; falling back to a safe no-op stub that refuses " +
                "every mutating operation. Install a real factory via QaCommandGatewayHost.InstallFactory " +
                "(see QaEditorCommandGatewayInstaller / DeveloperModeController) before running QA scenarios.");
            return new QaFallbackNullProfileService();
        }

        /// <summary>
        /// <see cref="IQaProfileService"/>의 안전한 널 오브젝트. 진짜 PlayerPrefs 스냅샷/복원을
        /// 절대 수행하지 않으며, 그 사실을 숨기지 않고 모든 mutation을 명시적으로 거부합니다.
        /// </summary>
        private sealed class QaFallbackNullProfileService : IQaProfileService
        {
            public bool IsQaProfileActive
            {
                get { return false; }
            }

            public QaProfileOperationResult BeginQaProfile(QaRunId runId)
            {
                return QaProfileOperationResult.Invalid(
                    "No real IQaProfileService is configured; refusing to begin an unisolated QA profile.");
            }

            public QaProfileOperationResult ResetGameplay()
            {
                return QaProfileOperationResult.NotActive(
                    "No real IQaProfileService is configured; there is no active QA profile to reset.");
            }

            public QaProfileOperationResult RestorePreviousProfile()
            {
                return QaProfileOperationResult.NotActive(
                    "No real IQaProfileService is configured; there is no active QA profile to restore.");
            }

            public QaProfileOperationResult RecoverInterruptedSession()
            {
                return QaProfileOperationResult.NothingToRecover(
                    "No real IQaProfileService is configured; nothing was ever begun to recover.");
            }
        }

        private IQaApiInteractable ResolveInteractable(QaTargetId targetId)
        {
            return sceneRegistry.TryResolveTarget(targetId, out QaResolvedTarget resolved)
                ? resolved.Adapter as IQaApiInteractable
                : null;
        }

        private QaScenarioDefinition TryFindValidatedScenario(string scenarioId, out string reason)
        {
            foreach ((string name, string json) in SafeLoadScenarioSources())
            {
                QaScenarioValidationResult validation = scenarioValidator.Validate(json);
                if (validation.IsValid && string.Equals(validation.Scenario.Id, scenarioId, StringComparison.Ordinal))
                {
                    reason = null;
                    return validation.Scenario;
                }
            }

            reason = "No valid QA scenario with id '" + scenarioId + "' was found.";
            return null;
        }

        private IReadOnlyList<(string Name, string Json)> SafeLoadScenarioSources()
        {
            try
            {
                return scenarioSourceProvider() ?? Array.Empty<(string Name, string Json)>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaCommandGateway] scenarioSourceProvider threw: " + ex.GetType().Name);
                return Array.Empty<(string Name, string Json)>();
            }
        }

        private string SafeGetEvidenceRunDirectoryPath()
        {
            try
            {
                return evidenceRunDirectoryProvider != null ? evidenceRunDirectoryProvider() : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaCommandGateway] evidenceRunDirectoryProvider threw: " + ex.GetType().Name);
                return string.Empty;
            }
        }

        private static IReadOnlyList<(string Name, string Json)> LoadScenarioResourcesFromUnity()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(DefaultScenarioResourcesPath);
            if (assets == null || assets.Length == 0)
            {
                return Array.Empty<(string Name, string Json)>();
            }

            var result = new List<(string Name, string Json)>(assets.Length);
            foreach (TextAsset asset in assets)
            {
                if (asset != null)
                {
                    result.Add((asset.name, asset.text));
                }
            }

            return result;
        }

        public void Dispose()
        {
            CancelActiveRun();
            leaseService.Dispose();
            driver.Dispose();
        }
    }
}
#endif
