#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Core
{
    /// <summary>
    /// <see cref="QaCommandResult"/>가 나타낼 수 있는 명시적 결과 코드.
    /// 새 의미가 필요하면 이 열거형에 값을 추가하고, 문자열 매직 코드는 사용하지 않습니다.
    /// </summary>
    public enum QaResultCode
    {
        Success,
        InvalidCommand,
        UnsupportedCommand,
        RunAlreadyActive,
        NoActiveRun,
        Cancelled,
        InternalError,

        /// <summary>
        /// Scene/profile/input/scenario 등 mutation 명령이 유효한(만료되지 않은) 활성
        /// <c>QaExecutionLease</c> 없이 실행되어 거부되었습니다. 자세한 사유는
        /// <see cref="QaCommandResult.Message"/>를 참고하세요.
        /// </summary>
        LeaseRequired
    }

    /// <summary>
    /// <see cref="IQaDriver.ExecuteAsync"/> 호출 한 건의 불변 결과.
    /// 실행 순서를 재구성할 수 있도록 드라이버 전체에서 단조 증가하는
    /// <see cref="SequenceNumber"/>를 항상 포함합니다.
    /// </summary>
    public sealed class QaCommandResult
    {
        public string CommandId { get; }
        public QaRunId RunId { get; }
        public long SequenceNumber { get; }
        public QaResultCode Code { get; }
        public string Message { get; }

        private QaCommandResult(
            string commandId,
            QaRunId runId,
            long sequenceNumber,
            QaResultCode code,
            string message)
        {
            CommandId = commandId;
            RunId = runId;
            SequenceNumber = sequenceNumber;
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess
        {
            get { return Code == QaResultCode.Success; }
        }

        public static QaCommandResult Create(
            string commandId,
            QaRunId runId,
            long sequenceNumber,
            QaResultCode code,
            string message = null)
        {
            return new QaCommandResult(commandId, runId, sequenceNumber, code, message);
        }
    }
}
#endif
