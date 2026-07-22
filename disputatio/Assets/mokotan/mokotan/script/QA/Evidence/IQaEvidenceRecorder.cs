#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Evidence
{
    /// <summary><see cref="QaEvidenceOperationResult"/>가 나타낼 수 있는 명시적 결과 코드.</summary>
    public enum QaEvidenceOperationCode
    {
        Success,
        InvalidRequest,

        /// <summary>활성 run이 없어 연산을 수행할 수 없습니다(<see cref="IQaEvidenceRecorder.BeginRun"/> 먼저 필요).</summary>
        NotActive,

        /// <summary>이미 활성 run이 있어 새로 시작할 수 없습니다.</summary>
        AlreadyActive,

        /// <summary>run이 이미 <see cref="IQaEvidenceRecorder.Finalize"/>로 마감되어 더 이상 변경할 수 없습니다.</summary>
        AlreadyFinalized,

        /// <summary>파일 시스템 등 내부 오류. 자세한 사유는 <see cref="QaEvidenceOperationResult.Message"/>.</summary>
        InternalError
    }

    /// <summary>
    /// <see cref="IQaEvidenceRecorder"/> 연산 한 건의 불변 결과. 비밀값이나 원본 evidence
    /// 페이로드는 절대 담지 않고, 사람이 읽을 수 있는 사유만 포함합니다.
    /// </summary>
    public sealed class QaEvidenceOperationResult
    {
        public QaEvidenceOperationCode Code { get; }

        public string Message { get; }

        private QaEvidenceOperationResult(QaEvidenceOperationCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess
        {
            get { return Code == QaEvidenceOperationCode.Success; }
        }

        public static QaEvidenceOperationResult Success(string message = null)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.Success, message);
        }

        public static QaEvidenceOperationResult Invalid(string message)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.InvalidRequest, message);
        }

        public static QaEvidenceOperationResult NotActive(string message)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.NotActive, message);
        }

        public static QaEvidenceOperationResult AlreadyActive(string message)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.AlreadyActive, message);
        }

        public static QaEvidenceOperationResult AlreadyFinalized(string message)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.AlreadyFinalized, message);
        }

        public static QaEvidenceOperationResult InternalError(string message)
        {
            return new QaEvidenceOperationResult(QaEvidenceOperationCode.InternalError, message);
        }
    }

    /// <summary><see cref="IQaEvidenceRecorder.Finalize"/> 호출 한 건의 불변 결과.</summary>
    public sealed class QaEvidenceFinalizeResult
    {
        public QaEvidenceOperationResult Operation { get; }

        /// <summary>성공 시 작성된 불변 manifest. 실패 시 <c>null</c>.</summary>
        public QaRunManifest Manifest { get; }

        /// <summary>성공 시 이 run이 기록된 절대 디렉터리 경로. 실패 시 <c>null</c>.</summary>
        public string RunDirectoryPath { get; }

        private QaEvidenceFinalizeResult(QaEvidenceOperationResult operation, QaRunManifest manifest, string runDirectoryPath)
        {
            Operation = operation;
            Manifest = manifest;
            RunDirectoryPath = runDirectoryPath;
        }

        public bool IsSuccess
        {
            get { return Operation != null && Operation.IsSuccess; }
        }

        public static QaEvidenceFinalizeResult Success(QaRunManifest manifest, string runDirectoryPath)
        {
            return new QaEvidenceFinalizeResult(
                QaEvidenceOperationResult.Success("QA run finalized."), manifest, runDirectoryPath);
        }

        public static QaEvidenceFinalizeResult Failure(QaEvidenceOperationResult operation)
        {
            return new QaEvidenceFinalizeResult(operation, null, null);
        }
    }

    /// <summary>
    /// Unity CLI QA 게이트웨이와 개발자 패널이 공유하는, 하나의 QA run에 대한 append-only
    /// evidence 기록 계약. 구현체는 <c>events.jsonl</c>(append-only), 스크린샷, Console 로그를
    /// 누적하고, <see cref="Finalize"/> 호출 시 단 한 번만 불변 <c>manifest.json</c>/<c>report.md</c>를
    /// 씁니다. <see cref="Finalize"/> 이후에는 어떤 append 연산도 거부되어야 합니다(불변 보장).
    /// </summary>
    public interface IQaEvidenceRecorder
    {
        /// <summary>
        /// 새 run의 evidence 디렉터리를 만들고 append 로그를 엽니다. <paramref name="runId"/>가
        /// 경로 구분자(<c>/</c>, <c>\</c>)나 <c>..</c> 등을 포함하면 디렉터리를 전혀 만들지 않고
        /// <see cref="QaEvidenceOperationCode.InvalidRequest"/>를 반환합니다.
        /// </summary>
        QaEvidenceOperationResult BeginRun(string runId, QaDriverSnapshot startSnapshot = null);

        /// <summary>
        /// 하나의 evidence 사건을 append합니다. 기존 줄은 절대 다시 쓰지 않습니다(append-only).
        /// 활성 run이 없거나 이미 finalize된 run에는 거부됩니다.
        /// </summary>
        QaEvidenceOperationResult AppendEvent(QaEvidenceEvent evidenceEvent);

        /// <summary>
        /// 스크린샷 바이트를 run의 <c>screenshots/</c> 아래에 쓰고, 대응하는
        /// <see cref="QaEvidenceEventType.ScreenshotAttached"/> 사건을 append합니다.
        /// </summary>
        QaEvidenceOperationResult AttachScreenshot(string commandId, byte[] pngBytes, string fileNameHint = null);

        /// <summary>
        /// Console 로그 텍스트를 run의 <c>console.log</c>에 append하고, 대응하는
        /// <see cref="QaEvidenceEventType.ConsoleRecorded"/> 사건을 append합니다. 민감 필드는
        /// 기록 전 치환됩니다.
        /// </summary>
        QaEvidenceOperationResult RecordConsole(string logText);

        /// <summary>
        /// run을 마감합니다: 지금까지 누적된 이벤트로부터 증거 기반 verdict를 산출하고,
        /// 단 한 번만 불변 <c>manifest.json</c>/<c>report.md</c>를 씁니다. 두 번째 호출은
        /// <see cref="QaEvidenceOperationCode.AlreadyFinalized"/>로 거부됩니다.
        /// </summary>
        QaEvidenceFinalizeResult Finalize(QaDriverSnapshot endSnapshot = null);
    }
}
#endif
