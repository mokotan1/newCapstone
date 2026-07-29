#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// 헤드리스 QA 드라이버. 한 번에 하나의 run만 소유하며, 모든 명령을 검증하고
    /// 하나의 async 호환 게이트로 직렬화하여 실행합니다. 씬별 게임플레이 로직은
    /// 포함하지 않으며(<c>IQaSceneRegistry</c> 등은 이후 태스크에서 연결됩니다),
    /// 지금은 <c>session.*</c> 명령만 처리하고 나머지는 <see cref="QaResultCode.UnsupportedCommand"/>를
    /// 반환합니다.
    /// </summary>
    public sealed class QaDriverCore : IQaDriver, IDisposable
    {
        /// <summary>
        /// 유효한(만료되지 않은) 활성 <c>QaExecutionLease</c>를 요구하는 mutation 명령 종류.
        /// 씬 전환/프로필 변경/입력 주입/시나리오 실행처럼 실제 Unity 실행 상태를 바꾸는
        /// 명령만 포함합니다. <see cref="QaCommandType.StateRead"/> 등 읽기 전용 명령과
        /// session.* 생애주기 명령은 리스가 없어도 항상 허용됩니다.
        /// </summary>
        private static readonly HashSet<QaCommandType> MutationCommandTypesRequiringLease = new HashSet<QaCommandType>
        {
            QaCommandType.ProfileReset,
            QaCommandType.ProfileApplyPreset,
            QaCommandType.SceneLoad,
            QaCommandType.SceneWaitReady,
            QaCommandType.InteractionApi,
            QaCommandType.InteractionPointer,
            QaCommandType.InteractionDrag,
            QaCommandType.InteractionKey,
            QaCommandType.ScenarioRun,
            QaCommandType.ScenarioCancel,
            QaCommandType.ScenarioStatus
        };

        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly Func<DateTime> utcNowProvider;
        private readonly IQaLeaseGate leaseGate;

        private long sequenceNumber;
        private QaRunState runState = QaRunState.Idle;
        private bool disposed;

        /// <summary>
        /// 명령이 완료될 때마다(성공·거부·취소·내부 오류 모두 포함) 발생합니다.
        /// 이후 태스크에서 <c>IQaEvidenceRecorder</c>가 이 이벤트를 구독하여 이벤트를 기록합니다.
        /// </summary>
        public event Action<QaCommandResult> CommandCompleted;

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// 테스트 전용 결함 주입 훅. 지정한 함수가 예외를 반환하면 명령 디스패치 중
        /// 그 예외가 발생한 것처럼 처리되어 <see cref="QaResultCode.InternalError"/> 경로를 검증할 수 있습니다.
        /// </summary>
        internal Func<QaCommand, Exception> FaultInjectorForTests { get; set; }
#endif

        /// <param name="utcNowProvider">테스트용 시각 주입 훅. 생략하면 <see cref="DateTime.UtcNow"/> 사용.</param>
        /// <param name="leaseGate">
        /// mutation 명령 실행 전 활성 리스를 확인할 게이트. <c>null</c>을 넘기면(기본값)
        /// 리스 검증을 완전히 생략합니다 — Task 2에서 만들어진 기존 호출자와의 하위 호환을
        /// 위한 선택적(opt-in) 의존성 주입입니다(DIP: 구체 <c>QaLeaseService</c>가 아닌
        /// <see cref="IQaLeaseGate"/> 인터페이스에 의존).
        /// </param>
        public QaDriverCore(Func<DateTime> utcNowProvider = null, IQaLeaseGate leaseGate = null)
        {
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
            this.leaseGate = leaseGate;
        }

        /// <summary>현재 run의 불변 스냅샷.</summary>
        public QaRunState CurrentRun
        {
            get { return runState; }
        }

        public async Task<QaCommandResult> ExecuteAsync(QaCommand command, CancellationToken cancellationToken)
        {
            long assignedSequenceNumber = Interlocked.Increment(ref sequenceNumber);

            if (command == null)
            {
                return Complete(BuildResult(null, assignedSequenceNumber, QaResultCode.InvalidCommand,
                    "Command must not be null."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(BuildResult(command, assignedSequenceNumber, QaResultCode.Cancelled,
                    "Command was cancelled before execution."));
            }

            try
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Complete(BuildResult(command, assignedSequenceNumber, QaResultCode.Cancelled,
                    "Command was cancelled while waiting for the execution gate."));
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Complete(BuildResult(command, assignedSequenceNumber, QaResultCode.Cancelled,
                        "Command was cancelled before execution."));
                }

                if (string.IsNullOrWhiteSpace(command.Id))
                {
                    return Complete(BuildResult(command, assignedSequenceNumber, QaResultCode.InvalidCommand,
                        "Command Id must not be blank."));
                }

                QaCommandResult result;
                try
                {
#if UNITY_INCLUDE_TESTS
                    Exception injectedFault = FaultInjectorForTests != null
                        ? FaultInjectorForTests.Invoke(command)
                        : null;
                    if (injectedFault != null)
                    {
                        throw injectedFault;
                    }
#endif
                    result = Dispatch(command, assignedSequenceNumber);
                }
                catch (Exception ex)
                {
                    result = BuildResult(command, assignedSequenceNumber, QaResultCode.InternalError,
                        SanitizeExceptionMessage(ex));
                }

                return Complete(result);
            }
            finally
            {
                gate.Release();
            }
        }

        private QaCommandResult Dispatch(QaCommand command, long forSequenceNumber)
        {
            if (leaseGate != null && MutationCommandTypesRequiringLease.Contains(command.Type))
            {
                if (!leaseGate.TryAuthorizeMutation(runState.RunId, out string denialReason))
                {
                    return BuildResult(command, forSequenceNumber, QaResultCode.LeaseRequired,
                        denialReason ?? "A valid QA execution lease is required for this command.");
                }
            }

            switch (command.Type)
            {
                case QaCommandType.SessionBegin:
                    return HandleSessionBegin(command, forSequenceNumber);
                case QaCommandType.SessionEnd:
                    return HandleSessionEnd(command, forSequenceNumber);
                case QaCommandType.SessionAbort:
                    return HandleSessionAbort(command, forSequenceNumber);
                default:
                    return BuildResult(command, forSequenceNumber, QaResultCode.UnsupportedCommand,
                        "Command type '" + command.Type + "' is not supported yet.");
            }
        }

        private QaCommandResult HandleSessionBegin(QaCommand command, long forSequenceNumber)
        {
            if (runState.IsActive)
            {
                return BuildResult(command, forSequenceNumber, QaResultCode.RunAlreadyActive,
                    "A QA run is already active. End or abort it before beginning a new one.");
            }

            runState = QaRunState.Begin(command.Id, utcNowProvider());
            return BuildResult(command, forSequenceNumber, QaResultCode.Success, "QA run started.");
        }

        private QaCommandResult HandleSessionEnd(QaCommand command, long forSequenceNumber)
        {
            if (!runState.IsActive)
            {
                return BuildResult(command, forSequenceNumber, QaResultCode.NoActiveRun,
                    "No active QA run to end.");
            }

            runState = runState.WithEnded(utcNowProvider());
            return BuildResult(command, forSequenceNumber, QaResultCode.Success, "QA run ended.");
        }

        private QaCommandResult HandleSessionAbort(QaCommand command, long forSequenceNumber)
        {
            if (!runState.IsActive)
            {
                return BuildResult(command, forSequenceNumber, QaResultCode.NoActiveRun,
                    "No active QA run to abort.");
            }

            runState = runState.WithAborted(utcNowProvider());
            return BuildResult(command, forSequenceNumber, QaResultCode.Success, "QA run aborted.");
        }

        private QaCommandResult BuildResult(
            QaCommand command,
            long forSequenceNumber,
            QaResultCode code,
            string message)
        {
            string commandId = command != null ? command.Id : null;
            return QaCommandResult.Create(commandId, runState.RunId, forSequenceNumber, code, message);
        }

        private QaCommandResult Complete(QaCommandResult result)
        {
            CommandCompleted?.Invoke(result);
            return result;
        }

        private static string SanitizeExceptionMessage(Exception exception)
        {
            return "Internal QA driver error (" + exception.GetType().Name + "). See server logs for details.";
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            gate.Dispose();
        }
    }
}
#endif
