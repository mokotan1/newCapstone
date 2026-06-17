using System;
using System.Globalization;
using System.Text;

/// <summary>
/// 플레이 CSV 헤더·행 직렬화. 파일 I/O 없이 단위 테스트 가능.
/// </summary>
public static class PlayLogCsvLogic
{
    public const string EventTypeSceneEnter = "scene_enter";
    public const string EventTypeCheshireUserMessage = "cheshire_user_message";
    public const string EventTypeCheshireBotResponse = "cheshire_bot_response";
    public const string EventTypeGiveHint = "give_hint";
    public const string EventTypeWrongAction = "wrong_action";
    public const string EventTypePuzzleSolved = "puzzle_solved";

    public static string BuildHeaderLine() =>
        string.Join(",", PlayLogCsvColumns.Ordered);

    public static string FormatEventRow(PlayLogEvent evt, bool includeMessageContent)
    {
        string userMessage = includeMessageContent ? evt.UserMessage : string.Empty;
        string botResponse = includeMessageContent ? evt.BotResponse : string.Empty;

        var values = new[]
        {
            evt.SessionId,
            evt.AnonymousPlayerId,
            evt.SceneName,
            evt.PuzzleId,
            FormatTimestamp(evt.EventTime),
            evt.EventType,
            userMessage,
            botResponse,
            evt.HintLevel,
            evt.ProgressState,
            FormatFloat(evt.TimeSinceSceneStart),
            evt.AttemptCount.ToString(CultureInfo.InvariantCulture),
            evt.WrongActionCount.ToString(CultureInfo.InvariantCulture),
            evt.RepeatedQuestionCount.ToString(CultureInfo.InvariantCulture),
            evt.Solved ? "true" : "false",
        };

        if (values.Length != PlayLogCsvColumns.Ordered.Length)
            throw new InvalidOperationException("PlayLog CSV column count mismatch.");

        var sb = new StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(Escape(values[i]));
        }

        return sb.ToString();
    }

    public static string Escape(string value)
    {
        string s = value ?? string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            return s;

        return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static string NormalizeQuestion(string question) =>
        (question ?? string.Empty).Trim().ToLowerInvariant();

    public static string ResolvePuzzleId(string sceneName) =>
        string.IsNullOrWhiteSpace(sceneName) ? string.Empty : sceneName;

    static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    static string FormatFloat(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
