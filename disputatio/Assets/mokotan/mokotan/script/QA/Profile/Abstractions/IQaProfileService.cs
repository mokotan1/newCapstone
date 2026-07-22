#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Core;

namespace Godlotto.QA.Profile
{
    /// <summary>
    /// <see cref="QaProfileService"/>가 QA 드라이버/개발자 패널에 노출하는 최소 계약.
    /// 목표는 단 하나: QA 자동화가 일반 플레이어의 진행 데이터(PlayerPrefs 진행 키 등)를
    /// 절대 영구적으로 변형하지 않도록 하는 것입니다(의존성 역전: 호출자는 구체 구현이 아닌
    /// 이 인터페이스에 의존합니다).
    /// </summary>
    public interface IQaProfileService
    {
        /// <summary>지금 이 인스턴스가 QA 프로필을 활성화한 상태인지 여부.</summary>
        bool IsQaProfileActive { get; }

        /// <summary>
        /// QA 실행(run)을 위한 격리 프로필을 시작합니다. 호출 시점의 일반 진행 PlayerPrefs 키
        /// 값을 스냅샷으로 저장(및 크래시 복구용으로 영속화)하여, 이후 <see cref="RestorePreviousProfile"/>
        /// 또는 <see cref="RecoverInterruptedSession"/>이 정확히 그 값으로 되돌릴 수 있게 합니다.
        /// 이미 프로필이 활성 상태이거나 해소되지 않은 이전 세션 마커가 있으면 거부됩니다.
        /// </summary>
        QaProfileOperationResult BeginQaProfile(QaRunId runId);

        /// <summary>
        /// 활성 QA 프로필의 진행 상태만 초기화합니다(오디오/비디오/언어 설정은 절대 건드리지
        /// 않음). QA 시나리오 사이에 깨끗한 상태로 재시작하기 위해 사용합니다. 활성 프로필이
        /// 없으면 거부됩니다.
        /// </summary>
        QaProfileOperationResult ResetGameplay();

        /// <summary>
        /// <see cref="BeginQaProfile"/> 시점에 캡처한 일반 진행 값을 그대로 복원하고 QA 프로필을
        /// 종료합니다. 스냅샷 이후 QA가 무엇을 했든, 완료 시점에는 일반 진행 PlayerPrefs 키가
        /// byte-for-byte 동일해야 합니다. 활성 프로필이 없으면 거부됩니다.
        /// </summary>
        QaProfileOperationResult RestorePreviousProfile();

        /// <summary>
        /// 이전 프로세스가 <see cref="RestorePreviousProfile"/>을 호출하지 못하고 비정상
        /// 종료된 경우(예: 크래시), 영속화된 세션 마커로부터 일반 진행 값을 복원하고 일반
        /// 프로필을 선택합니다. 씬 로딩 등 다른 초기화보다 먼저 호출되어야 합니다. 해소할
        /// 마커가 없으면 안전하게 아무 것도 하지 않습니다(예외 없음).
        /// </summary>
        QaProfileOperationResult RecoverInterruptedSession();
    }

    /// <summary><see cref="IQaProfileService"/> 각 연산이 반환할 수 있는 명시적 결과 코드.</summary>
    public enum QaProfileOperationCode
    {
        Success,
        InvalidRequest,

        /// <summary>이미 활성 QA 프로필이 있어 새로 시작할 수 없습니다.</summary>
        AlreadyActive,

        /// <summary>활성 QA 프로필이 없어 연산을 수행할 수 없습니다.</summary>
        NotActive,

        /// <summary>해소되지 않은 이전 세션 마커가 있어 <see cref="RecoverInterruptedSession"/>이 먼저 필요합니다.</summary>
        RecoveryRequired,

        /// <summary>중단된 세션을 성공적으로 복구했습니다.</summary>
        Recovered,

        /// <summary>복구할 대상이 없었습니다(정상 상태).</summary>
        NothingToRecover
    }

    /// <summary>
    /// <see cref="IQaProfileService"/> 연산 한 건의 불변 결과. 비밀값이나 원본 PlayerPrefs 값은
    /// 절대 담지 않고, 사람이 읽을 수 있는 사유만 포함합니다.
    /// </summary>
    public sealed class QaProfileOperationResult
    {
        public QaProfileOperationCode Code { get; }

        public string Message { get; }

        private QaProfileOperationResult(QaProfileOperationCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        /// <summary><see cref="QaProfileOperationCode.Success"/> 또는 <see cref="QaProfileOperationCode.Recovered"/>일 때 true.</summary>
        public bool IsSuccess
        {
            get { return Code == QaProfileOperationCode.Success || Code == QaProfileOperationCode.Recovered; }
        }

        public static QaProfileOperationResult Success(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.Success, message);
        }

        public static QaProfileOperationResult Invalid(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.InvalidRequest, message);
        }

        public static QaProfileOperationResult AlreadyActive(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.AlreadyActive, message);
        }

        public static QaProfileOperationResult NotActive(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.NotActive, message);
        }

        public static QaProfileOperationResult RecoveryRequired(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.RecoveryRequired, message);
        }

        public static QaProfileOperationResult Recovered(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.Recovered, message);
        }

        public static QaProfileOperationResult NothingToRecover(string message)
        {
            return new QaProfileOperationResult(QaProfileOperationCode.NothingToRecover, message);
        }
    }
}
#endif
