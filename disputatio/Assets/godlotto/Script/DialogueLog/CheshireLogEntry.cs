using System;

/// <summary>
/// 체셔(챗봇) 대화 로그 한 줄. CSV 내보내기·세션 누적용 데이터 모델.
/// UI 표시는 <see cref="DialogueLogEntry"/>로 변환해 사용한다.
/// </summary>
public readonly struct CheshireLogEntry
{
    /// <summary>기록 시각(UTC).</summary>
    public readonly DateTimeOffset Timestamp;

    /// <summary>기록 당시 활성 씬 이름.</summary>
    public readonly string SceneName;

    /// <summary>화자(예: 나, 체셔).</summary>
    public readonly string Speaker;

    /// <summary>질문 또는 응답 본문.</summary>
    public readonly string Text;

    /// <summary>세션 내 누적 순서(0부터). CSV·질문/답변 순서 복원용.</summary>
    public readonly int TurnIndex;

    public CheshireLogEntry(
        DateTimeOffset timestamp,
        string sceneName,
        string speaker,
        string text,
        int turnIndex)
    {
        Timestamp = timestamp;
        SceneName = sceneName ?? string.Empty;
        Speaker = speaker ?? string.Empty;
        Text = text ?? string.Empty;
        TurnIndex = turnIndex;
    }
}
