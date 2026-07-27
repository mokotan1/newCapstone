#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Godlotto.QA.Core
{
    /// <summary>
    /// 하나의 QA 실행(run)을 식별하는 불변 값 타입. 기본값 <see cref="None"/>은
    /// 실행 중인 run이 없음을 나타냅니다.
    /// </summary>
    public readonly struct QaRunId : IEquatable<QaRunId>
    {
        private readonly Guid value;

        private QaRunId(Guid value)
        {
            this.value = value;
        }

        /// <summary>활성 run이 없을 때 사용하는 값.</summary>
        public static readonly QaRunId None = default;

        public bool IsNone
        {
            get { return value == Guid.Empty; }
        }

        public static QaRunId NewId()
        {
            return new QaRunId(Guid.NewGuid());
        }

        /// <summary>영속 마커·명령 파라미터 등 외부 문자열 표현으로부터 안전하게 복원합니다.</summary>
        public static bool TryParse(string text, out QaRunId runId)
        {
            if (!string.IsNullOrWhiteSpace(text) && Guid.TryParse(text, out Guid parsed))
            {
                runId = new QaRunId(parsed);
                return true;
            }

            runId = None;
            return false;
        }

        public bool Equals(QaRunId other)
        {
            return value.Equals(other.value);
        }

        public override bool Equals(object obj)
        {
            return obj is QaRunId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return IsNone ? "none" : value.ToString("N");
        }

        public static bool operator ==(QaRunId left, QaRunId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QaRunId left, QaRunId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>QA run의 생애주기 단계.</summary>
    public enum QaRunPhase
    {
        Idle,
        Active,
        Ended,
        Aborted
    }

    /// <summary>
    /// <see cref="QaDriverCore"/>가 소유하는 단일 run의 불변 스냅샷.
    /// 상태 전이마다 새 인스턴스를 생성하여 예측 불가능한 공유 상태 변경을 방지합니다.
    /// </summary>
    public sealed class QaRunState
    {
        public QaRunId RunId { get; }
        public QaRunPhase Phase { get; }
        public string BeganByCommandId { get; }
        public DateTime StartedAtUtc { get; }
        public DateTime? EndedAtUtc { get; }

        private QaRunState(
            QaRunId runId,
            QaRunPhase phase,
            string beganByCommandId,
            DateTime startedAtUtc,
            DateTime? endedAtUtc)
        {
            RunId = runId;
            Phase = phase;
            BeganByCommandId = beganByCommandId;
            StartedAtUtc = startedAtUtc;
            EndedAtUtc = endedAtUtc;
        }

        public bool IsActive
        {
            get { return Phase == QaRunPhase.Active; }
        }

        /// <summary>활성 run이 없는 초기 상태.</summary>
        public static QaRunState Idle
        {
            get { return new QaRunState(QaRunId.None, QaRunPhase.Idle, null, default, null); }
        }

        public static QaRunState Begin(string commandId, DateTime startedAtUtc)
        {
            return new QaRunState(QaRunId.NewId(), QaRunPhase.Active, commandId, startedAtUtc, null);
        }

        public QaRunState WithEnded(DateTime endedAtUtc)
        {
            return new QaRunState(RunId, QaRunPhase.Ended, BeganByCommandId, StartedAtUtc, endedAtUtc);
        }

        public QaRunState WithAborted(DateTime endedAtUtc)
        {
            return new QaRunState(RunId, QaRunPhase.Aborted, BeganByCommandId, StartedAtUtc, endedAtUtc);
        }
    }
}
#endif
