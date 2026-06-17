using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

public class PlayLogTelemetryTests
{
    static PlayLogEvent SampleEvent(
        string sessionId = "sess-1",
        string eventType = "cheshire_user_message",
        string userMessage = "where is the key?",
        string botResponse = "")
    {
        return new PlayLogEvent(
            sessionId: sessionId,
            anonymousPlayerId: "anon-1",
            sceneName: "BedRoom",
            puzzleId: "BedRoom",
            eventTime: new DateTimeOffset(2026, 6, 16, 7, 0, 0, TimeSpan.Zero),
            eventType: eventType,
            userMessage: userMessage,
            botResponse: botResponse,
            hintLevel: "subtle",
            progressState: "room=Global",
            timeSinceSceneStart: 12.5f,
            attemptCount: 3,
            wrongActionCount: 1,
            repeatedQuestionCount: 0,
            solved: false);
    }

    // ---- PlayLogTelemetryUrl ----

    [Test]
    public void Url_ReplacesChatSuffixWithTelemetry()
    {
        Assert.AreEqual(
            "http://host:8000/telemetry",
            PlayLogTelemetryUrl.Resolve("http://host:8000/chat", null));
    }

    [Test]
    public void Url_HandlesTrailingSlash()
    {
        Assert.AreEqual(
            "http://host:8000/telemetry",
            PlayLogTelemetryUrl.Resolve("http://host:8000/chat/", ""));
    }

    [Test]
    public void Url_AppendsTelemetryWhenNoChatSuffix()
    {
        Assert.AreEqual(
            "http://host:8000/telemetry",
            PlayLogTelemetryUrl.Resolve("http://host:8000", null));
    }

    [Test]
    public void Url_OverrideWins()
    {
        Assert.AreEqual(
            "https://custom/t",
            PlayLogTelemetryUrl.Resolve("http://host:8000/chat", "https://custom/t"));
    }

    [Test]
    public void Url_EmptyChatUrlReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, PlayLogTelemetryUrl.Resolve("", null));
    }

    // ---- PlayLogTelemetryPayload ----

    [Test]
    public void Payload_WrapsEventsArrayWithSnakeCaseKeys()
    {
        string json = PlayLogTelemetryPayload.BuildJson(
            new List<PlayLogEvent> { SampleEvent() },
            includeMessageContent: true);

        JObject root = JObject.Parse(json);
        JArray events = (JArray)root["events"];
        Assert.AreEqual(1, events.Count);

        JObject e = (JObject)events[0];
        Assert.AreEqual("sess-1", (string)e["session_id"]);
        Assert.AreEqual("cheshire_user_message", (string)e["event_type"]);
        Assert.AreEqual("where is the key?", (string)e["user_message"]);
        Assert.AreEqual(3, (int)e["attempt_count"]);
        Assert.AreEqual(false, (bool)e["solved"]);
    }

    [Test]
    public void Payload_OmitsMessageContent_WhenDisabled()
    {
        string json = PlayLogTelemetryPayload.BuildJson(
            new List<PlayLogEvent> { SampleEvent(userMessage: "secret", botResponse: "secret-bot") },
            includeMessageContent: false);

        JObject e = (JObject)((JArray)JObject.Parse(json)["events"])[0];
        Assert.AreEqual(string.Empty, (string)e["user_message"]);
        Assert.AreEqual(string.Empty, (string)e["bot_response"]);
        // 비-본문 컬럼은 유지된다.
        Assert.AreEqual("sess-1", (string)e["session_id"]);
    }

    [Test]
    public void Payload_SerializesMultipleEventsInOrder()
    {
        string json = PlayLogTelemetryPayload.BuildJson(
            new List<PlayLogEvent> { SampleEvent("s1"), SampleEvent("s2") },
            includeMessageContent: true);

        JArray events = (JArray)JObject.Parse(json)["events"];
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("s1", (string)((JObject)events[0])["session_id"]);
        Assert.AreEqual("s2", (string)((JObject)events[1])["session_id"]);
    }

    // ---- PlayLogTelemetryBuffer ----

    [Test]
    public void Buffer_EnqueueIncrementsCount()
    {
        var buffer = new PlayLogTelemetryBuffer(maxBufferedEvents: 10);
        buffer.Enqueue(SampleEvent());
        Assert.AreEqual(1, buffer.Count);
    }

    [Test]
    public void Buffer_DrainBatchReturnsFifoUpToMax()
    {
        var buffer = new PlayLogTelemetryBuffer(maxBufferedEvents: 10);
        buffer.Enqueue(SampleEvent("a"));
        buffer.Enqueue(SampleEvent("b"));
        buffer.Enqueue(SampleEvent("c"));

        List<PlayLogEvent> batch = buffer.DrainBatch(2);

        Assert.AreEqual(2, batch.Count);
        Assert.AreEqual("a", batch[0].SessionId);
        Assert.AreEqual("b", batch[1].SessionId);
        Assert.AreEqual(1, buffer.Count);
    }

    [Test]
    public void Buffer_DrainBatchOnEmptyReturnsEmpty()
    {
        var buffer = new PlayLogTelemetryBuffer(maxBufferedEvents: 10);
        Assert.AreEqual(0, buffer.DrainBatch(5).Count);
    }

    [Test]
    public void Buffer_DropsOldestWhenOverCapacity()
    {
        var buffer = new PlayLogTelemetryBuffer(maxBufferedEvents: 2);
        buffer.Enqueue(SampleEvent("a"));
        buffer.Enqueue(SampleEvent("b"));
        buffer.Enqueue(SampleEvent("c")); // "a" should be dropped

        Assert.AreEqual(2, buffer.Count);
        List<PlayLogEvent> batch = buffer.DrainBatch(2);
        Assert.AreEqual("b", batch[0].SessionId);
        Assert.AreEqual("c", batch[1].SessionId);
    }

    [Test]
    public void Buffer_RequeuePutsEventsBackToFront()
    {
        var buffer = new PlayLogTelemetryBuffer(maxBufferedEvents: 10);
        buffer.Enqueue(SampleEvent("new"));
        var failed = new List<PlayLogEvent> { SampleEvent("retry") };

        buffer.Requeue(failed);

        Assert.AreEqual(2, buffer.Count);
        List<PlayLogEvent> batch = buffer.DrainBatch(2);
        Assert.AreEqual("retry", batch[0].SessionId);
        Assert.AreEqual("new", batch[1].SessionId);
    }
}
