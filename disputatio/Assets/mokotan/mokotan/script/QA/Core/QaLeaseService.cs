#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// <see cref="QaLeaseService"/>가 <see cref="QaDriverCore"/>에 노출하는 최소 계약.
    /// mutation 명령을 실행하기 전, 현재 run에 대한 유효한(만료되지 않은) 활성 리스가
    /// 있는지만 확인합니다. 소유자 식별이나 리스 발급 자체는 이 인터페이스의 책임이 아닙니다
    /// (의존성 역전: <see cref="QaDriverCore"/>는 구체 구현이 아닌 이 인터페이스에 의존합니다).
    /// </summary>
    public interface IQaLeaseGate
    {
        /// <summary>
        /// <paramref name="runId"/>에 대한 mutation 명령이 지금 허용되는지 확인합니다.
        /// 거부되면 <paramref name="denialReason"/>에 사람이 읽을 수 있는 이유가 채워집니다.
        /// </summary>
        bool TryAuthorizeMutation(QaRunId runId, out string denialReason);
    }

    /// <summary>
    /// <see cref="QaLeaseRecoveryMarker"/>를 영속화하는 추상 저장소. 테스트에서는 실제
    /// 파일 시스템이나 PlayerPrefs를 오염시키지 않도록 인메모리 구현을 주입할 수 있습니다.
    /// </summary>
    public interface IQaLeaseRecoveryStore
    {
        /// <summary>이전에 저장된 마커를 불러옵니다. 없거나 읽기에 실패하면 <c>null</c>.</summary>
        QaLeaseRecoveryMarker Load();

        /// <summary>마커를 저장(갱신)합니다. 비밀값·명령 페이로드는 절대 포함하지 않습니다.</summary>
        void Save(QaLeaseRecoveryMarker marker);

        /// <summary>저장된 마커를 제거합니다(정상 Release 또는 명시적 복구 완료 시).</summary>
        void Clear();
    }

    /// <summary>영속화를 하지 않는 널 오브젝트. 복구 마커 기능이 필요 없는 호출자를 위한 기본값.</summary>
    public sealed class QaNullLeaseRecoveryStore : IQaLeaseRecoveryStore
    {
        public static readonly QaNullLeaseRecoveryStore Instance = new QaNullLeaseRecoveryStore();

        private QaNullLeaseRecoveryStore()
        {
        }

        public QaLeaseRecoveryMarker Load()
        {
            return null;
        }

        public void Save(QaLeaseRecoveryMarker marker)
        {
        }

        public void Clear()
        {
        }
    }

    /// <summary>
    /// <c>Application.persistentDataPath</c> 아래 QA 프로필 영역에 마커를 저장하는 기본 구현.
    /// 파일 하나에 <c>key=value</c> 라인 4개만 기록합니다(runId/ownerId/leaseId/lastHeartbeatUtc).
    /// 읽기/쓰기 실패는 절대 밖으로 던지지 않고, 안전하게 "마커 없음"으로 취급합니다
    /// (Fail-Safe: 손상된 파일이 있어도 QA 드라이버 전체가 죽지 않도록 함).
    /// </summary>
    public sealed class QaFileLeaseRecoveryStore : IQaLeaseRecoveryStore
    {
        private const string RunIdKey = "runId";
        private const string OwnerIdKey = "ownerId";
        private const string LeaseIdKey = "leaseId";
        private const string LastHeartbeatUtcKey = "lastHeartbeatUtc";

        private readonly string filePath;

        public QaFileLeaseRecoveryStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path must not be blank.", nameof(filePath));
            }

            this.filePath = filePath;
        }

        /// <summary>
        /// <c>Application.persistentDataPath</c> 기준 기본 경로(<c>QA/lease-recovery.marker</c>)를 사용하는
        /// 인스턴스를 생성합니다. <c>IQaProfileService</c>가 도입되면 그 경로로 교체될 예정입니다.
        /// </summary>
        public static QaFileLeaseRecoveryStore CreateDefault()
        {
            string basePath = UnityEngine.Application.persistentDataPath;
            string path = Path.Combine(basePath, "QA", "lease-recovery.marker");
            return new QaFileLeaseRecoveryStore(path);
        }

        public QaLeaseRecoveryMarker Load()
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                string runId = null;
                string ownerId = null;
                string leaseIdText = null;
                string lastHeartbeatText = null;

                foreach (string line in File.ReadAllLines(filePath))
                {
                    int separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex);
                    string value = line.Substring(separatorIndex + 1);

                    if (key == RunIdKey) runId = value;
                    else if (key == OwnerIdKey) ownerId = value;
                    else if (key == LeaseIdKey) leaseIdText = value;
                    else if (key == LastHeartbeatUtcKey) lastHeartbeatText = value;
                }

                if (string.IsNullOrWhiteSpace(runId)
                    || string.IsNullOrWhiteSpace(ownerId)
                    || !QaLeaseId.TryParse(leaseIdText, out QaLeaseId leaseId)
                    || !DateTime.TryParse(
                        lastHeartbeatText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal,
                        out DateTime lastHeartbeatUtc))
                {
                    UnityEngine.Debug.LogWarning(
                        "[QaLeaseService] Recovery marker at '" + filePath + "' is malformed; ignoring it.");
                    return null;
                }

                return QaLeaseRecoveryMarker.Create(runId, ownerId, leaseId, lastHeartbeatUtc);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[QaLeaseService] Failed to read recovery marker: " + ex.GetType().Name);
                return null;
            }
        }

        public void Save(QaLeaseRecoveryMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string content =
                    RunIdKey + "=" + marker.RunId + Environment.NewLine +
                    OwnerIdKey + "=" + marker.OwnerId + Environment.NewLine +
                    LeaseIdKey + "=" + marker.LeaseId + Environment.NewLine +
                    LastHeartbeatUtcKey + "=" +
                    marker.LastHeartbeatUtc.ToString("o", CultureInfo.InvariantCulture) + Environment.NewLine;

                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[QaLeaseService] Failed to persist recovery marker: " + ex.GetType().Name);
            }
        }

        public void Clear()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[QaLeaseService] Failed to clear recovery marker: " + ex.GetType().Name);
            }
        }
    }

    /// <summary>
    /// 단일 활성 writer를 강제하는 QA 실행 리스 서비스. 한 번에 하나의 <see cref="QaExecutionLease"/>만
    /// 메모리에 보관하며, 소유자·리스 ID·마지막 하트비트를 비밀 없는 마커로 영속화하여 프로세스가
    /// 비정상 종료되거나 하트비트가 끊겨도 "만료된 리스를 조용히 이어서 실행"하는 일이 없도록 합니다.
    /// 만료·미회수 마커는 반드시 <see cref="RecoverExpiredLease"/>를 통해 명시적으로 해소해야만
    /// 새 리스를 발급할 수 있습니다.
    /// </summary>
    public sealed class QaLeaseService : IQaLeaseGate, IDisposable
    {
        private readonly object sync = new object();
        private readonly IQaLeaseRecoveryStore recoveryStore;
        private readonly Func<DateTime> utcNowProvider;

        private QaExecutionLease activeLease;
        private QaLeaseRecoveryMarker unresolvedMarker;
        private bool disposed;

        public QaLeaseService(IQaLeaseRecoveryStore recoveryStore = null, Func<DateTime> utcNowProvider = null)
        {
            this.recoveryStore = recoveryStore ?? QaNullLeaseRecoveryStore.Instance;
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);

            // 생성 시점에 미해소 마커가 있으면(이전 프로세스가 정상적으로 Release하지 못한 경우),
            // 이 인스턴스의 메모리 상태는 비어 있더라도 절대 조용히 새 run을 허용하지 않습니다.
            unresolvedMarker = SafeLoadMarker();
        }

        /// <summary>새 리스 발급을 시도합니다. 활성 리스가 있거나 미해소 마커가 있으면 거부됩니다.</summary>
        public QaLeaseAcquireResult TryAcquire(string ownerId, QaRunId runId, TimeSpan ttl)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return QaLeaseAcquireResult.Invalid("ownerId must not be blank.");
            }

            if (runId.IsNone)
            {
                return QaLeaseAcquireResult.Invalid("runId must not be QaRunId.None.");
            }

            if (ttl <= TimeSpan.Zero)
            {
                return QaLeaseAcquireResult.Invalid("ttl must be a positive duration.");
            }

            lock (sync)
            {
                ThrowIfDisposed();

                if (unresolvedMarker != null)
                {
                    return QaLeaseAcquireResult.RecoveryRequired(unresolvedMarker,
                        "An unresolved QA execution lease from a prior process must be recovered first.");
                }

                DateTime now = utcNowProvider();

                if (activeLease != null)
                {
                    if (!activeLease.IsExpired(now))
                    {
                        return QaLeaseAcquireResult.Denied(activeLease.ToRecoveryMarker(),
                            "Another QA execution lease is already active.");
                    }

                    // 만료됐지만 아직 명시적으로 회수되지 않았습니다. 조용히 이어받지 않고
                    // 복구를 요구합니다.
                    return QaLeaseAcquireResult.RecoveryRequired(activeLease.ToRecoveryMarker(),
                        "The active QA execution lease expired and must be recovered before a new one can be acquired.");
                }

                QaExecutionLease lease = QaExecutionLease.Acquire(ownerId, runId, ttl, now);
                activeLease = lease;
                recoveryStore.Save(lease.ToRecoveryMarker());
                return QaLeaseAcquireResult.Acquired(lease);
            }
        }

        /// <summary>
        /// 리스를 갱신합니다. 동일 소유자가 여러 번 호출해도 안전(idempotent)합니다.
        /// 리스가 이미 만료된 경우 갱신하지 않고 <see cref="QaLeaseOperationResultCode.Expired"/>를 반환합니다.
        /// </summary>
        public QaLeaseOperationResult Heartbeat(QaLeaseId leaseId)
        {
            if (leaseId.IsNone)
            {
                return QaLeaseOperationResult.Invalid("leaseId must not be QaLeaseId.None.");
            }

            lock (sync)
            {
                ThrowIfDisposed();

                if (activeLease == null || activeLease.LeaseId != leaseId)
                {
                    return QaLeaseOperationResult.NotFound("No active QA execution lease with the given id.");
                }

                DateTime now = utcNowProvider();
                if (activeLease.IsExpired(now))
                {
                    return QaLeaseOperationResult.Expired(activeLease,
                        "The QA execution lease already expired; call RecoverExpiredLease before reacquiring.");
                }

                activeLease = activeLease.WithHeartbeat(now);
                recoveryStore.Save(activeLease.ToRecoveryMarker());
                return QaLeaseOperationResult.Success(activeLease, "Heartbeat recorded.");
            }
        }

        /// <summary>리스를 정상적으로 반납합니다. 알 수 없는 리스 ID는 안전하게 실패합니다(예외 없음).</summary>
        public QaLeaseOperationResult Release(QaLeaseId leaseId)
        {
            if (leaseId.IsNone)
            {
                return QaLeaseOperationResult.Invalid("leaseId must not be QaLeaseId.None.");
            }

            lock (sync)
            {
                ThrowIfDisposed();

                if (activeLease == null || activeLease.LeaseId != leaseId)
                {
                    return QaLeaseOperationResult.NotFound("No active QA execution lease with the given id.");
                }

                QaExecutionLease released = activeLease;
                activeLease = null;
                recoveryStore.Clear();
                return QaLeaseOperationResult.Success(released, "QA execution lease released.");
            }
        }

        /// <summary>
        /// 만료되었거나 이전 프로세스가 남긴 미해소 마커를 명시적으로 정리합니다. 이 호출 없이는
        /// <see cref="TryAcquire"/>가 절대 이전 run을 조용히 이어받지 않습니다.
        /// </summary>
        public QaLeaseRecoveryResult RecoverExpiredLease(QaLeaseId leaseId, string recoveringOwnerId)
        {
            if (leaseId.IsNone)
            {
                return QaLeaseRecoveryResult.Invalid("leaseId must not be QaLeaseId.None.");
            }

            if (string.IsNullOrWhiteSpace(recoveringOwnerId))
            {
                return QaLeaseRecoveryResult.Invalid("recoveringOwnerId must not be blank.");
            }

            lock (sync)
            {
                ThrowIfDisposed();

                if (unresolvedMarker != null && unresolvedMarker.LeaseId == leaseId)
                {
                    QaLeaseRecoveryMarker recovered = unresolvedMarker;
                    unresolvedMarker = null;
                    activeLease = null;
                    recoveryStore.Clear();
                    return QaLeaseRecoveryResult.Recovered(recovered,
                        "Unresolved QA execution lease from a prior process recovered by '" + recoveringOwnerId + "'.");
                }

                if (activeLease != null && activeLease.LeaseId == leaseId)
                {
                    DateTime now = utcNowProvider();
                    if (!activeLease.IsExpired(now))
                    {
                        return QaLeaseRecoveryResult.Invalid(
                            "The QA execution lease is still active; call Release instead of RecoverExpiredLease.");
                    }

                    QaLeaseRecoveryMarker recovered = activeLease.ToRecoveryMarker();
                    activeLease = null;
                    recoveryStore.Clear();
                    return QaLeaseRecoveryResult.Recovered(recovered,
                        "Expired QA execution lease recovered by '" + recoveringOwnerId + "'.");
                }

                return QaLeaseRecoveryResult.NotFound("No expired or unresolved QA execution lease with the given id.");
            }
        }

        /// <inheritdoc />
        public bool TryAuthorizeMutation(QaRunId runId, out string denialReason)
        {
            lock (sync)
            {
                if (disposed)
                {
                    denialReason = "QA lease service has been disposed.";
                    return false;
                }

                if (unresolvedMarker != null)
                {
                    denialReason = "An unresolved QA execution lease requires recovery before mutations can resume.";
                    return false;
                }

                if (activeLease == null)
                {
                    denialReason = "No active QA execution lease.";
                    return false;
                }

                if (activeLease.RunId != runId)
                {
                    denialReason = "The active QA execution lease belongs to a different run.";
                    return false;
                }

                if (activeLease.IsExpired(utcNowProvider()))
                {
                    denialReason = "The QA execution lease expired and requires recovery.";
                    return false;
                }

                denialReason = null;
                return true;
            }
        }

        private QaLeaseRecoveryMarker SafeLoadMarker()
        {
            try
            {
                return recoveryStore.Load();
            }
            catch (Exception)
            {
                // Fail-safe: a broken store must never crash service construction. Treat as
                // "recovery required is unknown" by conservatively assuming none was found;
                // the store implementation is responsible for its own internal logging.
                return null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(QaLeaseService));
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                disposed = true;
            }
        }
    }
}
#endif
