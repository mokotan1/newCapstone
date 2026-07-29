#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// 하나의 QA 실행 리스(lease)를 식별하는 불변 값 타입. <see cref="QaRunId"/>와 동일한 패턴을
    /// 따르며, 기본값 <see cref="None"/>은 활성 리스가 없음을 나타냅니다.
    /// </summary>
    public readonly struct QaLeaseId : IEquatable<QaLeaseId>
    {
        private readonly Guid value;

        private QaLeaseId(Guid value)
        {
            this.value = value;
        }

        /// <summary>활성 리스가 없을 때 사용하는 값.</summary>
        public static readonly QaLeaseId None = default;

        public bool IsNone
        {
            get { return value == Guid.Empty; }
        }

        public static QaLeaseId NewId()
        {
            return new QaLeaseId(Guid.NewGuid());
        }

        /// <summary>영속 마커 등 외부 문자열 표현으로부터 안전하게 복원합니다.</summary>
        public static bool TryParse(string text, out QaLeaseId leaseId)
        {
            if (!string.IsNullOrWhiteSpace(text) && Guid.TryParse(text, out Guid parsed))
            {
                leaseId = new QaLeaseId(parsed);
                return true;
            }

            leaseId = None;
            return false;
        }

        public bool Equals(QaLeaseId other)
        {
            return value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is QaLeaseId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return IsNone ? "none" : value.ToString("N");
        }

        public static bool operator ==(QaLeaseId left, QaLeaseId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QaLeaseId left, QaLeaseId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 디스크에 영속화되는 복구 마커의 불변 스냅샷. 절대 비밀값이나 명령 페이로드를 담지
    /// 않으며, 오직 <c>runId</c>·<c>ownerId</c>·<c>leaseId</c>·마지막 하트비트 시각만 보관합니다.
    /// 진행 중이던 리스가 만료되었거나 이전 프로세스가 비정상 종료된 경우, 이 마커가 남아
    /// 있으면 <see cref="QaLeaseService"/>는 명시적 복구 없이는 새 리스 발급을 거부합니다.
    /// </summary>
    public sealed class QaLeaseRecoveryMarker
    {
        /// <summary>마커가 가리키는 run의 문자열 식별자(<see cref="QaRunId.ToString"/> 표현).</summary>
        public string RunId { get; }

        public string OwnerId { get; }

        public QaLeaseId LeaseId { get; }

        public DateTime LastHeartbeatUtc { get; }

        private QaLeaseRecoveryMarker(string runId, string ownerId, QaLeaseId leaseId, DateTime lastHeartbeatUtc)
        {
            RunId = runId;
            OwnerId = ownerId;
            LeaseId = leaseId;
            LastHeartbeatUtc = lastHeartbeatUtc;
        }

        public static QaLeaseRecoveryMarker Create(
            string runId,
            string ownerId,
            QaLeaseId leaseId,
            DateTime lastHeartbeatUtc)
        {
            return new QaLeaseRecoveryMarker(runId, ownerId, leaseId, lastHeartbeatUtc);
        }
    }

    /// <summary>
    /// 하나의 활성 QA 실행 리스에 대한 불변 스냅샷. 상태가 바뀔 때마다(하트비트 등)
    /// 새 인스턴스를 생성하여 공유 가변 상태로 인한 예측 불가능성을 차단합니다.
    /// </summary>
    public sealed class QaExecutionLease
    {
        public QaLeaseId LeaseId { get; }

        public QaRunId RunId { get; }

        public string OwnerId { get; }

        public TimeSpan Ttl { get; }

        public DateTime AcquiredAtUtc { get; }

        public DateTime LastHeartbeatUtc { get; }

        private QaExecutionLease(
            QaLeaseId leaseId,
            QaRunId runId,
            string ownerId,
            TimeSpan ttl,
            DateTime acquiredAtUtc,
            DateTime lastHeartbeatUtc)
        {
            LeaseId = leaseId;
            RunId = runId;
            OwnerId = ownerId;
            Ttl = ttl;
            AcquiredAtUtc = acquiredAtUtc;
            LastHeartbeatUtc = lastHeartbeatUtc;
        }

        public static QaExecutionLease Acquire(string ownerId, QaRunId runId, TimeSpan ttl, DateTime nowUtc)
        {
            return new QaExecutionLease(QaLeaseId.NewId(), runId, ownerId, ttl, nowUtc, nowUtc);
        }

        /// <summary>하트비트를 반영한 새 스냅샷을 반환합니다. 원본 인스턴스는 변경되지 않습니다.</summary>
        public QaExecutionLease WithHeartbeat(DateTime nowUtc)
        {
            return new QaExecutionLease(LeaseId, RunId, OwnerId, Ttl, AcquiredAtUtc, nowUtc);
        }

        /// <summary>마지막 하트비트 이후 TTL이 지났는지 여부.</summary>
        public bool IsExpired(DateTime nowUtc)
        {
            return nowUtc > LastHeartbeatUtc + Ttl;
        }

        /// <summary>영속화·복구 결과 보고용으로 비밀 정보 없는 마커로 변환합니다.</summary>
        public QaLeaseRecoveryMarker ToRecoveryMarker()
        {
            return QaLeaseRecoveryMarker.Create(RunId.ToString(), OwnerId, LeaseId, LastHeartbeatUtc);
        }
    }

    /// <summary><see cref="QaLeaseService.TryAcquire"/>가 반환할 수 있는 결과 코드.</summary>
    public enum QaLeaseAcquireResultCode
    {
        /// <summary>새 리스가 발급되었습니다.</summary>
        Acquired,

        /// <summary>다른 소유자(또는 만료되지 않은 리스)가 이미 활성 상태라 거부되었습니다.</summary>
        DeniedActiveElsewhere,

        /// <summary>만료되었거나 이전 프로세스가 남긴 리스가 있어 명시적 복구가 필요합니다.</summary>
        RecoveryRequired,

        /// <summary>요청 파라미터가 유효하지 않습니다.</summary>
        InvalidRequest
    }

    /// <summary><see cref="QaLeaseService.Heartbeat"/>/<see cref="QaLeaseService.Release"/> 결과 코드.</summary>
    public enum QaLeaseOperationResultCode
    {
        Success,

        /// <summary>주어진 리스 ID에 해당하는 활성 리스가 없습니다.</summary>
        NotFound,

        /// <summary>리스는 존재하지만 TTL이 지나 만료되어, 복구 없이는 갱신할 수 없습니다.</summary>
        Expired,

        InvalidRequest
    }

    /// <summary><see cref="QaLeaseService.RecoverExpiredLease"/> 결과 코드.</summary>
    public enum QaLeaseRecoveryResultCode
    {
        Recovered,

        /// <summary>복구가 필요한 리스/마커가 없습니다(이미 복구되었거나 존재하지 않음).</summary>
        NotFound,

        InvalidRequest
    }

    /// <summary><see cref="QaLeaseService.TryAcquire"/>의 불변 결과.</summary>
    public sealed class QaLeaseAcquireResult
    {
        public QaLeaseAcquireResultCode Code { get; }

        /// <summary><see cref="QaLeaseAcquireResultCode.Acquired"/>일 때만 값이 존재합니다.</summary>
        public QaExecutionLease Lease { get; }

        /// <summary>거부/복구 필요 상태를 유발한 기존 리스(또는 영속 마커)에 대한 비밀 없는 설명.</summary>
        public QaLeaseRecoveryMarker Blocker { get; }

        public string Message { get; }

        private QaLeaseAcquireResult(
            QaLeaseAcquireResultCode code,
            QaExecutionLease lease,
            QaLeaseRecoveryMarker blocker,
            string message)
        {
            Code = code;
            Lease = lease;
            Blocker = blocker;
            Message = message ?? string.Empty;
        }

        public bool IsAcquired
        {
            get { return Code == QaLeaseAcquireResultCode.Acquired; }
        }

        public static QaLeaseAcquireResult Acquired(QaExecutionLease lease)
        {
            return new QaLeaseAcquireResult(QaLeaseAcquireResultCode.Acquired, lease, null,
                "QA execution lease acquired.");
        }

        public static QaLeaseAcquireResult Denied(QaLeaseRecoveryMarker blocker, string message)
        {
            return new QaLeaseAcquireResult(QaLeaseAcquireResultCode.DeniedActiveElsewhere, null, blocker, message);
        }

        public static QaLeaseAcquireResult RecoveryRequired(QaLeaseRecoveryMarker blocker, string message)
        {
            return new QaLeaseAcquireResult(QaLeaseAcquireResultCode.RecoveryRequired, null, blocker, message);
        }

        public static QaLeaseAcquireResult Invalid(string message)
        {
            return new QaLeaseAcquireResult(QaLeaseAcquireResultCode.InvalidRequest, null, null, message);
        }
    }

    /// <summary><see cref="QaLeaseService.Heartbeat"/>/<see cref="QaLeaseService.Release"/>의 불변 결과.</summary>
    public sealed class QaLeaseOperationResult
    {
        public QaLeaseOperationResultCode Code { get; }

        public QaExecutionLease Lease { get; }

        public string Message { get; }

        private QaLeaseOperationResult(QaLeaseOperationResultCode code, QaExecutionLease lease, string message)
        {
            Code = code;
            Lease = lease;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess
        {
            get { return Code == QaLeaseOperationResultCode.Success; }
        }

        public static QaLeaseOperationResult Success(QaExecutionLease lease, string message)
        {
            return new QaLeaseOperationResult(QaLeaseOperationResultCode.Success, lease, message);
        }

        public static QaLeaseOperationResult NotFound(string message)
        {
            return new QaLeaseOperationResult(QaLeaseOperationResultCode.NotFound, null, message);
        }

        public static QaLeaseOperationResult Expired(QaExecutionLease lease, string message)
        {
            return new QaLeaseOperationResult(QaLeaseOperationResultCode.Expired, lease, message);
        }

        public static QaLeaseOperationResult Invalid(string message)
        {
            return new QaLeaseOperationResult(QaLeaseOperationResultCode.InvalidRequest, null, message);
        }
    }

    /// <summary><see cref="QaLeaseService.RecoverExpiredLease"/>의 불변 결과.</summary>
    public sealed class QaLeaseRecoveryResult
    {
        public QaLeaseRecoveryResultCode Code { get; }

        /// <summary>복구되어 정리된 기존 리스에 대한 비밀 없는 설명(성공 시).</summary>
        public QaLeaseRecoveryMarker RecoveredLease { get; }

        public string Message { get; }

        private QaLeaseRecoveryResult(
            QaLeaseRecoveryResultCode code,
            QaLeaseRecoveryMarker recoveredLease,
            string message)
        {
            Code = code;
            RecoveredLease = recoveredLease;
            Message = message ?? string.Empty;
        }

        public bool IsRecovered
        {
            get { return Code == QaLeaseRecoveryResultCode.Recovered; }
        }

        public static QaLeaseRecoveryResult Recovered(QaLeaseRecoveryMarker recoveredLease, string message)
        {
            return new QaLeaseRecoveryResult(QaLeaseRecoveryResultCode.Recovered, recoveredLease, message);
        }

        public static QaLeaseRecoveryResult NotFound(string message)
        {
            return new QaLeaseRecoveryResult(QaLeaseRecoveryResultCode.NotFound, null, message);
        }

        public static QaLeaseRecoveryResult Invalid(string message)
        {
            return new QaLeaseRecoveryResult(QaLeaseRecoveryResultCode.InvalidRequest, null, message);
        }
    }
}
#endif
