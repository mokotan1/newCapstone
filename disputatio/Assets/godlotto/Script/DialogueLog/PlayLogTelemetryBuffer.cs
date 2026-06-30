using System.Collections.Generic;

/// <summary>
/// 텔레메트리 전송 대기 이벤트의 메모리 버퍼(FIFO).
/// 상한 초과 시 가장 오래된 항목을 버리고, 전송 실패분은 <see cref="Requeue"/>로
/// 앞쪽에 되돌려 순서를 보존한다. 단일(메인) 스레드 사용 전제 — 락 없음.
/// </summary>
public sealed class PlayLogTelemetryBuffer
{
    public const int DefaultMaxBufferedEvents = 1000;

    readonly List<PlayLogEvent> pending;
    readonly int maxBufferedEvents;

    public PlayLogTelemetryBuffer(int maxBufferedEvents = DefaultMaxBufferedEvents)
    {
        this.maxBufferedEvents = maxBufferedEvents > 0 ? maxBufferedEvents : DefaultMaxBufferedEvents;
        pending = new List<PlayLogEvent>(this.maxBufferedEvents);
    }

    public int Count => pending.Count;

    /// <summary>이벤트를 큐 끝에 추가한다. 상한 초과 시 가장 오래된 항목을 버린다.</summary>
    public void Enqueue(PlayLogEvent evt)
    {
        pending.Add(evt);
        TrimToCapacity();
    }

    /// <summary>최대 <paramref name="maxBatch"/>개를 FIFO로 꺼내 제거한 뒤 반환한다.</summary>
    public List<PlayLogEvent> DrainBatch(int maxBatch)
    {
        int take = maxBatch < pending.Count ? maxBatch : pending.Count;
        if (take <= 0)
            return new List<PlayLogEvent>();

        var batch = pending.GetRange(0, take);
        pending.RemoveRange(0, take);
        return batch;
    }

    /// <summary>전송 실패분을 앞쪽에 되돌린다(다음 Drain에서 우선 재시도).</summary>
    public void Requeue(IReadOnlyList<PlayLogEvent> events)
    {
        if (events == null || events.Count == 0)
            return;

        pending.InsertRange(0, events);
        TrimToCapacity();
    }

    void TrimToCapacity()
    {
        int overflow = pending.Count - maxBufferedEvents;
        if (overflow > 0)
            pending.RemoveRange(0, overflow);
    }
}
