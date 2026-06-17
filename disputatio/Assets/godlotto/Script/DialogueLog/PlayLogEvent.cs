using System;

/// <summary>
/// CSV 한 행에 대응하는 플레이·챗봇 이벤트.
/// </summary>
public readonly struct PlayLogEvent
{
    public readonly string SessionId;
    public readonly string AnonymousPlayerId;
    public readonly string SceneName;
    public readonly string PuzzleId;
    public readonly DateTimeOffset EventTime;
    public readonly string EventType;
    public readonly string UserMessage;
    public readonly string BotResponse;
    public readonly string HintLevel;
    public readonly string ProgressState;
    public readonly float TimeSinceSceneStart;
    public readonly int AttemptCount;
    public readonly int WrongActionCount;
    public readonly int RepeatedQuestionCount;
    public readonly bool Solved;

    public PlayLogEvent(
        string sessionId,
        string anonymousPlayerId,
        string sceneName,
        string puzzleId,
        DateTimeOffset eventTime,
        string eventType,
        string userMessage,
        string botResponse,
        string hintLevel,
        string progressState,
        float timeSinceSceneStart,
        int attemptCount,
        int wrongActionCount,
        int repeatedQuestionCount,
        bool solved)
    {
        SessionId = sessionId ?? string.Empty;
        AnonymousPlayerId = anonymousPlayerId ?? string.Empty;
        SceneName = sceneName ?? string.Empty;
        PuzzleId = puzzleId ?? string.Empty;
        EventTime = eventTime;
        EventType = eventType ?? string.Empty;
        UserMessage = userMessage ?? string.Empty;
        BotResponse = botResponse ?? string.Empty;
        HintLevel = hintLevel ?? string.Empty;
        ProgressState = progressState ?? string.Empty;
        TimeSinceSceneStart = timeSinceSceneStart;
        AttemptCount = attemptCount;
        WrongActionCount = wrongActionCount;
        RepeatedQuestionCount = repeatedQuestionCount;
        Solved = solved;
    }
}
