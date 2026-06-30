/// <summary>
/// 플레이·챗봇 CSV 로그 컬럼 순서. 분석 파이프라인·헤더 생성 시 이 순서를 따른다.
/// 상세 설명: <c>disputatio/docs/play-log-csv-columns.md</c>
/// </summary>
public static class PlayLogCsvColumns
{
    public const string SessionId = "session_id";
    public const string AnonymousPlayerId = "anonymous_player_id";
    public const string SceneName = "scene_name";
    public const string PuzzleId = "puzzle_id";
    public const string EventTime = "event_time";
    public const string EventType = "event_type";
    public const string UserMessage = "user_message";
    public const string BotResponse = "bot_response";
    public const string HintLevel = "hint_level";
    public const string ProgressState = "progress_state";
    public const string TimeSinceSceneStart = "time_since_scene_start";
    public const string AttemptCount = "attempt_count";
    public const string WrongActionCount = "wrong_action_count";
    public const string RepeatedQuestionCount = "repeated_question_count";
    public const string Solved = "solved";

    /// <summary>헤더·데이터 행에 사용하는 고정 컬럼 순서.</summary>
    public static readonly string[] Ordered =
    {
        SessionId,
        AnonymousPlayerId,
        SceneName,
        PuzzleId,
        EventTime,
        EventType,
        UserMessage,
        BotResponse,
        HintLevel,
        ProgressState,
        TimeSinceSceneStart,
        AttemptCount,
        WrongActionCount,
        RepeatedQuestionCount,
        Solved,
    };
}
