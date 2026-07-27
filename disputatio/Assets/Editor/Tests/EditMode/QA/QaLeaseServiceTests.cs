using System;
using Godlotto.QA.Core;
using NUnit.Framework;

/// <summary>
/// <see cref="QaLeaseService"/>가 강제하는 단일 writer 보장(소유권 거부, 하트비트 멱등성,
/// 유효하지 않은 반납, 만료 후 명시적 복구 필요)을 검증합니다. 실제 파일 시스템/PlayerPrefs를
/// 오염시키지 않도록 <see cref="FakeLeaseRecoveryStore"/>를 주입합니다.
/// </summary>
[TestFixture]
public class QaLeaseServiceTests
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    private FakeClock clock;
    private FakeLeaseRecoveryStore store;
    private QaLeaseService service;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        store = new FakeLeaseRecoveryStore();
        service = new QaLeaseService(store, clock.UtcNow);
    }

    [TearDown]
    public void TearDown()
    {
        service?.Dispose();
        service = null;
    }

    // ---------------------------------------------------------------
    //  TryAcquire - basic acquisition & validation
    // ---------------------------------------------------------------

    [Test]
    public void TryAcquire_NoPriorLease_ReturnsAcquiredWithFreshLease()
    {
        QaRunId runId = QaRunId.NewId();

        QaLeaseAcquireResult result = service.TryAcquire("owner-a", runId, DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.Acquired, result.Code);
        Assert.IsNotNull(result.Lease);
        Assert.AreEqual("owner-a", result.Lease.OwnerId);
        Assert.AreEqual(runId, result.Lease.RunId);
        Assert.IsFalse(result.Lease.LeaseId.IsNone);
        Assert.IsNotNull(store.SavedMarker, "Acquiring must persist a recovery marker.");
    }

    [Test]
    public void TryAcquire_BlankOwnerId_ReturnsInvalidRequest()
    {
        QaLeaseAcquireResult result = service.TryAcquire("   ", QaRunId.NewId(), DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.InvalidRequest, result.Code);
    }

    [Test]
    public void TryAcquire_NoneRunId_ReturnsInvalidRequest()
    {
        QaLeaseAcquireResult result = service.TryAcquire("owner-a", QaRunId.None, DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.InvalidRequest, result.Code);
    }

    [Test]
    public void TryAcquire_NonPositiveTtl_ReturnsInvalidRequest()
    {
        QaLeaseAcquireResult result = service.TryAcquire("owner-a", QaRunId.NewId(), TimeSpan.Zero);

        Assert.AreEqual(QaLeaseAcquireResultCode.InvalidRequest, result.Code);
    }

    // ---------------------------------------------------------------
    //  TryAcquire - different-owner rejection
    // ---------------------------------------------------------------

    [Test]
    public void TryAcquire_DifferentOwnerWhileActiveLeaseValid_ReturnsDeniedActiveElsewhere()
    {
        QaLeaseAcquireResult first = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        Assert.IsTrue(first.IsAcquired);

        QaLeaseAcquireResult second = service.TryAcquire("owner-b", QaRunId.NewId(), DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.DeniedActiveElsewhere, second.Code);
        Assert.IsNull(second.Lease);
        Assert.IsNotNull(second.Blocker);
        Assert.AreEqual("owner-a", second.Blocker.OwnerId);
    }

    [Test]
    public void TryAcquire_SameOwnerWhileActiveLeaseValid_IsAlsoDenied_MustUseExistingLease()
    {
        // Even the original owner cannot acquire a second concurrent lease; it must reuse the
        // one it already holds (Heartbeat) instead of minting a new leaseId. This preserves the
        // "one active writer" guarantee without adding an ownerId/leaseId channel to QaCommand.
        QaLeaseAcquireResult first = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        Assert.IsTrue(first.IsAcquired);

        QaLeaseAcquireResult second = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.DeniedActiveElsewhere, second.Code);
    }

    // ---------------------------------------------------------------
    //  Heartbeat - same-owner idempotence
    // ---------------------------------------------------------------

    [Test]
    public void Heartbeat_CalledTwiceBySameOwner_ExtendsLeaseIdempotentlyWithoutError()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        QaLeaseId leaseId = acquired.Lease.LeaseId;

        clock.Advance(TimeSpan.FromSeconds(5));
        QaLeaseOperationResult firstHeartbeat = service.Heartbeat(leaseId);

        clock.Advance(TimeSpan.FromSeconds(5));
        QaLeaseOperationResult secondHeartbeat = service.Heartbeat(leaseId);

        Assert.IsTrue(firstHeartbeat.IsSuccess);
        Assert.IsTrue(secondHeartbeat.IsSuccess);
        Assert.AreEqual(leaseId, firstHeartbeat.Lease.LeaseId);
        Assert.AreEqual(leaseId, secondHeartbeat.Lease.LeaseId);
        Assert.Greater(secondHeartbeat.Lease.LastHeartbeatUtc, firstHeartbeat.Lease.LastHeartbeatUtc);
    }

    [Test]
    public void Heartbeat_UnknownLeaseId_ReturnsNotFound()
    {
        service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        QaLeaseOperationResult result = service.Heartbeat(QaLeaseId.NewId());

        Assert.AreEqual(QaLeaseOperationResultCode.NotFound, result.Code);
    }

    [Test]
    public void Heartbeat_OnExpiredLease_ReturnsExpiredWithoutExtendingIt()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        QaLeaseId leaseId = acquired.Lease.LeaseId;

        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        QaLeaseOperationResult result = service.Heartbeat(leaseId);

        Assert.AreEqual(QaLeaseOperationResultCode.Expired, result.Code);
    }

    // ---------------------------------------------------------------
    //  Release - invalid release must fail safely, never throw
    // ---------------------------------------------------------------

    [Test]
    public void Release_WithUnknownLeaseId_ReturnsNotFoundWithoutThrowing()
    {
        service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        QaLeaseOperationResult result = null;
        Assert.DoesNotThrow(() => result = service.Release(QaLeaseId.NewId()));

        Assert.AreEqual(QaLeaseOperationResultCode.NotFound, result.Code);
    }

    [Test]
    public void Release_WithNoneLeaseId_ReturnsInvalidRequestWithoutThrowing()
    {
        QaLeaseOperationResult result = null;
        Assert.DoesNotThrow(() => result = service.Release(QaLeaseId.None));

        Assert.AreEqual(QaLeaseOperationResultCode.InvalidRequest, result.Code);
    }

    [Test]
    public void Release_WithValidLeaseId_ClearsActiveLeaseAndPersistedMarker()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        QaLeaseOperationResult released = service.Release(acquired.Lease.LeaseId);

        Assert.IsTrue(released.IsSuccess);
        Assert.IsTrue(store.WasCleared, "Release must clear the persisted recovery marker.");

        // A fresh acquisition must now succeed, proving no stale state remains.
        QaLeaseAcquireResult reacquired = service.TryAcquire("owner-b", QaRunId.NewId(), DefaultTtl);
        Assert.IsTrue(reacquired.IsAcquired);
    }

    // ---------------------------------------------------------------
    //  Expiry requires explicit recovery - never silently continue a prior run
    // ---------------------------------------------------------------

    [Test]
    public void TryAcquire_AfterLeaseExpiresWithoutRelease_ReturnsRecoveryRequired()
    {
        QaRunId staleRunId = QaRunId.NewId();
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", staleRunId, DefaultTtl);
        Assert.IsTrue(acquired.IsAcquired);

        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        QaLeaseAcquireResult result = service.TryAcquire("owner-b", QaRunId.NewId(), DefaultTtl);

        Assert.AreEqual(QaLeaseAcquireResultCode.RecoveryRequired, result.Code);
        Assert.IsNull(result.Lease);
        Assert.IsNotNull(result.Blocker);
        Assert.AreEqual("owner-a", result.Blocker.OwnerId);
    }

    [Test]
    public void RecoverExpiredLease_ThenTryAcquire_SucceedsAndDoesNotResumePriorRun()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        QaLeaseId staleLeaseId = acquired.Lease.LeaseId;

        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        QaLeaseRecoveryResult recovery = service.RecoverExpiredLease(staleLeaseId, "operator");
        Assert.IsTrue(recovery.IsRecovered);
        Assert.AreEqual("owner-a", recovery.RecoveredLease.OwnerId);

        QaRunId freshRunId = QaRunId.NewId();
        QaLeaseAcquireResult reacquired = service.TryAcquire("owner-b", freshRunId, DefaultTtl);

        Assert.IsTrue(reacquired.IsAcquired);
        Assert.AreEqual(freshRunId, reacquired.Lease.RunId);
        Assert.AreNotEqual(staleLeaseId, reacquired.Lease.LeaseId);
    }

    [Test]
    public void RecoverExpiredLease_OnStillActiveLease_ReturnsInvalidRequest()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        QaLeaseRecoveryResult result = service.RecoverExpiredLease(acquired.Lease.LeaseId, "operator");

        Assert.AreEqual(QaLeaseRecoveryResultCode.InvalidRequest, result.Code);
    }

    [Test]
    public void RecoverExpiredLease_WithUnknownLeaseId_ReturnsNotFound()
    {
        service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        QaLeaseRecoveryResult result = service.RecoverExpiredLease(QaLeaseId.NewId(), "operator");

        Assert.AreEqual(QaLeaseRecoveryResultCode.NotFound, result.Code);
    }

    [Test]
    public void RecoverExpiredLease_BlankRecoveringOwnerId_ReturnsInvalidRequest()
    {
        QaLeaseAcquireResult acquired = service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);
        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        QaLeaseRecoveryResult result = service.RecoverExpiredLease(acquired.Lease.LeaseId, " ");

        Assert.AreEqual(QaLeaseRecoveryResultCode.InvalidRequest, result.Code);
    }

    // ---------------------------------------------------------------
    //  Restart / crash recovery - unresolved marker from a prior process instance
    // ---------------------------------------------------------------

    [Test]
    public void TryAcquire_WithUnresolvedMarkerFromPriorProcess_ReturnsRecoveryRequired()
    {
        QaRunId priorRunId = QaRunId.NewId();
        QaLeaseId priorLeaseId = QaLeaseId.NewId();
        store.SavedMarker = QaLeaseRecoveryMarker.Create(
            priorRunId.ToString(), "owner-crashed", priorLeaseId, clock.UtcNow());

        // Simulate a fresh process: a brand-new QaLeaseService instance backed by the same store.
        QaLeaseService restarted = new QaLeaseService(store, clock.UtcNow);
        try
        {
            QaLeaseAcquireResult result = restarted.TryAcquire("owner-new", QaRunId.NewId(), DefaultTtl);

            Assert.AreEqual(QaLeaseAcquireResultCode.RecoveryRequired, result.Code);
            Assert.AreEqual("owner-crashed", result.Blocker.OwnerId);
        }
        finally
        {
            restarted.Dispose();
        }
    }

    [Test]
    public void RecoverExpiredLease_OnUnresolvedMarkerFromPriorProcess_ClearsItAndAllowsAcquire()
    {
        QaRunId priorRunId = QaRunId.NewId();
        QaLeaseId priorLeaseId = QaLeaseId.NewId();
        store.SavedMarker = QaLeaseRecoveryMarker.Create(
            priorRunId.ToString(), "owner-crashed", priorLeaseId, clock.UtcNow());

        QaLeaseService restarted = new QaLeaseService(store, clock.UtcNow);
        try
        {
            QaLeaseRecoveryResult recovery = restarted.RecoverExpiredLease(priorLeaseId, "operator");
            Assert.IsTrue(recovery.IsRecovered);

            QaLeaseAcquireResult result = restarted.TryAcquire("owner-new", QaRunId.NewId(), DefaultTtl);
            Assert.IsTrue(result.IsAcquired);
        }
        finally
        {
            restarted.Dispose();
        }
    }

    // ---------------------------------------------------------------
    //  IQaLeaseGate - authorization surface consumed by QaDriverCore
    // ---------------------------------------------------------------

    [Test]
    public void TryAuthorizeMutation_NoActiveLease_ReturnsFalse()
    {
        bool authorized = service.TryAuthorizeMutation(QaRunId.NewId(), out string reason);

        Assert.IsFalse(authorized);
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void TryAuthorizeMutation_ActiveLeaseForMatchingRun_ReturnsTrue()
    {
        QaRunId runId = QaRunId.NewId();
        service.TryAcquire("owner-a", runId, DefaultTtl);

        bool authorized = service.TryAuthorizeMutation(runId, out string reason);

        Assert.IsTrue(authorized);
        Assert.IsNull(reason);
    }

    [Test]
    public void TryAuthorizeMutation_ActiveLeaseForDifferentRun_ReturnsFalse()
    {
        service.TryAcquire("owner-a", QaRunId.NewId(), DefaultTtl);

        bool authorized = service.TryAuthorizeMutation(QaRunId.NewId(), out string reason);

        Assert.IsFalse(authorized);
        Assert.IsNotEmpty(reason);
    }

    [Test]
    public void TryAuthorizeMutation_ExpiredLease_ReturnsFalseWithoutRecovering()
    {
        QaRunId runId = QaRunId.NewId();
        service.TryAcquire("owner-a", runId, DefaultTtl);
        clock.Advance(DefaultTtl + TimeSpan.FromSeconds(1));

        bool authorized = service.TryAuthorizeMutation(runId, out string reason);

        Assert.IsFalse(authorized);
        Assert.IsNotEmpty(reason);
    }

    // ---------------------------------------------------------------
    //  Test doubles
    // ---------------------------------------------------------------

    private sealed class FakeClock
    {
        private DateTime now;

        public FakeClock(DateTime initialUtc)
        {
            now = initialUtc;
        }

        public DateTime UtcNow()
        {
            return now;
        }

        public void Advance(TimeSpan by)
        {
            now = now + by;
        }
    }

    /// <summary>
    /// 인메모리 페이크 저장소. 실제 파일 시스템/PlayerPrefs를 건드리지 않고
    /// <see cref="QaLeaseService"/>의 영속화 계약을 검증할 수 있게 합니다.
    /// </summary>
    private sealed class FakeLeaseRecoveryStore : IQaLeaseRecoveryStore
    {
        public QaLeaseRecoveryMarker SavedMarker { get; set; }

        public bool WasCleared { get; private set; }

        public QaLeaseRecoveryMarker Load()
        {
            return SavedMarker;
        }

        public void Save(QaLeaseRecoveryMarker marker)
        {
            SavedMarker = marker;
            WasCleared = false;
        }

        public void Clear()
        {
            SavedMarker = null;
            WasCleared = true;
        }
    }
}
