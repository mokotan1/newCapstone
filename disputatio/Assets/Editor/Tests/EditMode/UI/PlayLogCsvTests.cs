using System;
using NUnit.Framework;

public class PlayLogCsvTests
{
    [TearDown]
    public void TearDown()
    {
        PlayLogRecorder.ResetForTest();
        ChatHttpClient.ResetAnonymousUserIdForTest();
    }

    [Test]
    public void Columns_OrderMatchesAnalysisSpec()
    {
        Assert.AreEqual(15, PlayLogCsvColumns.Ordered.Length);
        Assert.AreEqual("session_id", PlayLogCsvColumns.Ordered[0]);
        Assert.AreEqual("anonymous_player_id", PlayLogCsvColumns.Ordered[1]);
        Assert.AreEqual("scene_name", PlayLogCsvColumns.Ordered[2]);
        Assert.AreEqual("puzzle_id", PlayLogCsvColumns.Ordered[3]);
        Assert.AreEqual("event_time", PlayLogCsvColumns.Ordered[4]);
        Assert.AreEqual("event_type", PlayLogCsvColumns.Ordered[5]);
        Assert.AreEqual("user_message", PlayLogCsvColumns.Ordered[6]);
        Assert.AreEqual("bot_response", PlayLogCsvColumns.Ordered[7]);
        Assert.AreEqual("hint_level", PlayLogCsvColumns.Ordered[8]);
        Assert.AreEqual("progress_state", PlayLogCsvColumns.Ordered[9]);
        Assert.AreEqual("time_since_scene_start", PlayLogCsvColumns.Ordered[10]);
        Assert.AreEqual("attempt_count", PlayLogCsvColumns.Ordered[11]);
        Assert.AreEqual("wrong_action_count", PlayLogCsvColumns.Ordered[12]);
        Assert.AreEqual("repeated_question_count", PlayLogCsvColumns.Ordered[13]);
        Assert.AreEqual("solved", PlayLogCsvColumns.Ordered[14]);
    }

    [Test]
    public void BuildHeaderLine_MatchesColumnOrder()
    {
        string header = PlayLogCsvLogic.BuildHeaderLine();

        Assert.AreEqual(string.Join(",", PlayLogCsvColumns.Ordered), header);
    }

    [Test]
    public void FormatEventRow_IncludesAllRequiredFields()
    {
        var timestamp = new DateTimeOffset(2026, 6, 16, 3, 4, 5, TimeSpan.Zero);
        var evt = new PlayLogEvent(
            sessionId: "sess-1",
            anonymousPlayerId: "anon-abc",
            sceneName: "Kitchen",
            puzzleId: "Kitchen",
            eventTime: timestamp,
            eventType: PlayLogCsvLogic.EventTypeCheshireUserMessage,
            userMessage: "물병은 어디?",
            botResponse: string.Empty,
            hintLevel: string.Empty,
            progressState: "room=GlobalChatbot;quest=tutorial;step=1",
            timeSinceSceneStart: 12.5f,
            attemptCount: 2,
            wrongActionCount: 1,
            repeatedQuestionCount: 1,
            solved: false);

        string row = PlayLogCsvLogic.FormatEventRow(evt, includeMessageContent: true);

        StringAssert.Contains("sess-1", row);
        StringAssert.Contains("anon-abc", row);
        StringAssert.Contains("Kitchen", row);
        StringAssert.Contains("cheshire_user_message", row);
        StringAssert.Contains("물병은 어디?", row);
        StringAssert.Contains("room=GlobalChatbot;quest=tutorial;step=1", row);
        StringAssert.Contains("12.5", row);
        StringAssert.Contains(",2,1,1,false", row);
    }

    [Test]
    public void FormatEventRow_OmitsMessageContent_WhenDisabled()
    {
        var evt = new PlayLogEvent(
            sessionId: "sess-1",
            anonymousPlayerId: "anon-abc",
            sceneName: "Kitchen",
            puzzleId: "Kitchen",
            eventTime: DateTimeOffset.UtcNow,
            eventType: PlayLogCsvLogic.EventTypeCheshireBotResponse,
            userMessage: string.Empty,
            botResponse: "비밀 응답",
            hintLevel: string.Empty,
            progressState: string.Empty,
            timeSinceSceneStart: 0f,
            attemptCount: 0,
            wrongActionCount: 0,
            repeatedQuestionCount: 0,
            solved: false);

        string row = PlayLogCsvLogic.FormatEventRow(evt, includeMessageContent: false);

        Assert.IsFalse(row.Contains("비밀 응답"));
    }

    [Test]
    public void Escape_QuotesCommasAndNewlines()
    {
        Assert.AreEqual("plain", PlayLogCsvLogic.Escape("plain"));
        Assert.AreEqual("\"a,b\"", PlayLogCsvLogic.Escape("a,b"));
        Assert.AreEqual("\"line\"\"two\"", PlayLogCsvLogic.Escape("line\"two"));
    }

    [Test]
    public void SessionContext_TracksAttemptsWrongActionsAndRepeatedQuestions()
    {
        var ctx = new PlayLogSessionContext("sess-test");

        Assert.AreEqual(1, ctx.IncrementAttempt("Kitchen"));
        Assert.AreEqual(2, ctx.IncrementAttempt("Kitchen"));
        Assert.AreEqual(1, ctx.IncrementWrongAction("Kitchen"));
        Assert.AreEqual(1, ctx.RegisterQuestion("Kitchen", "hello"));
        Assert.AreEqual(2, ctx.RegisterQuestion("Kitchen", "hello"));

        ctx.OnSceneEntered("Kitchen", 10f);
        Assert.AreEqual(5f, ctx.GetTimeSinceSceneStart("Kitchen", 15f));
    }

    [Test]
    public void Recorder_BuildsProgressState_WithRoomName()
    {
        string progress = PlayLogRecorder.BuildProgressStateSnapshot("GlobalChatbot");

        StringAssert.Contains("room=GlobalChatbot", progress);
    }

    [Test]
    public void Recorder_InitializesSessionWithoutWriting_WhenFileIoDisabled()
    {
        PlayLogRecorder.DisableFileIoForTests = true;
        PlayLogRecorder.RecordCheshireUserMessage("테스트 질문");

        Assert.IsFalse(string.IsNullOrEmpty(PlayLogRecorder.SessionId));
    }
}
