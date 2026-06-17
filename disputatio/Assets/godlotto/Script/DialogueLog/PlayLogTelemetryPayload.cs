using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

/// <summary>
/// 텔레메트리 전송 본문(JSON) 직렬화. 서버 <c>backend_ai/models/requests.py</c>의
/// <c>TelemetryIngestRequest</c> / <c>TelemetryEvent</c> 스키마와 1:1 대응.
/// 네트워크 비의존 순수 로직(테스트 가능).
/// </summary>
public static class PlayLogTelemetryPayload
{
    /// <summary>이벤트 배치를 <c>{"events":[...]}</c> JSON 문자열로 만든다.</summary>
    public static string BuildJson(IReadOnlyList<PlayLogEvent> events, bool includeMessageContent)
    {
        var dtoList = new List<EventDto>(events?.Count ?? 0);
        if (events != null)
        {
            for (int i = 0; i < events.Count; i++)
                dtoList.Add(EventDto.From(events[i], includeMessageContent));
        }

        return JsonConvert.SerializeObject(new IngestDto { events = dtoList });
    }

    [System.Serializable]
    sealed class IngestDto
    {
        public List<EventDto> events;
    }

    [System.Serializable]
    sealed class EventDto
    {
        [JsonProperty("session_id")] public string sessionId;
        [JsonProperty("anonymous_player_id")] public string anonymousPlayerId;
        [JsonProperty("scene_name")] public string sceneName;
        [JsonProperty("puzzle_id")] public string puzzleId;
        [JsonProperty("event_time")] public string eventTime;
        [JsonProperty("event_type")] public string eventType;
        [JsonProperty("user_message")] public string userMessage;
        [JsonProperty("bot_response")] public string botResponse;
        [JsonProperty("hint_level")] public string hintLevel;
        [JsonProperty("progress_state")] public string progressState;
        [JsonProperty("time_since_scene_start")] public float timeSinceSceneStart;
        [JsonProperty("attempt_count")] public int attemptCount;
        [JsonProperty("wrong_action_count")] public int wrongActionCount;
        [JsonProperty("repeated_question_count")] public int repeatedQuestionCount;
        [JsonProperty("solved")] public bool solved;

        public static EventDto From(PlayLogEvent evt, bool includeMessageContent)
        {
            return new EventDto
            {
                sessionId = evt.SessionId,
                anonymousPlayerId = evt.AnonymousPlayerId,
                sceneName = evt.SceneName,
                puzzleId = evt.PuzzleId,
                eventTime = evt.EventTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                eventType = evt.EventType,
                userMessage = includeMessageContent ? evt.UserMessage : string.Empty,
                botResponse = includeMessageContent ? evt.BotResponse : string.Empty,
                hintLevel = evt.HintLevel,
                progressState = evt.ProgressState,
                timeSinceSceneStart = evt.TimeSinceSceneStart,
                attemptCount = evt.AttemptCount,
                wrongActionCount = evt.WrongActionCount,
                repeatedQuestionCount = evt.RepeatedQuestionCount,
                solved = evt.Solved,
            };
        }
    }
}
