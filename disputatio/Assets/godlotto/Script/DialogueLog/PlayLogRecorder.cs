using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 챗봇·플레이 이벤트를 CSV로 누적 저장한다.
/// 런타임 샘플 경로: <c>{Application.persistentDataPath}/PlayLogs/play_log_{session_id}.csv</c>
/// </summary>
public static class PlayLogRecorder
{
    const string SessionIdPrefsKey = "PlayLogRecorder.SessionId";

    static bool initialized;
    static PlayLogSessionContext session;
    static string csvFilePath;
    static bool headerWritten;
    static PlayLogTelemetryBuffer telemetryBuffer;

    internal static bool DisableFileIoForTests { get; set; }

    /// <summary>서버 전송 대기 중인 이벤트 수(업로더가 폴링).</summary>
    public static int TelemetryPendingCount => telemetryBuffer?.Count ?? 0;

    /// <summary>PlayLogSettings에서 서버 업로드가 켜져 있는지.</summary>
    public static bool TelemetryUploadEnabled => PlayLogSettings.GetOrCreate().EnableTelemetryUpload;

    internal static List<PlayLogEvent> DrainTelemetryBatch(int maxBatch) =>
        telemetryBuffer?.DrainBatch(maxBatch) ?? new List<PlayLogEvent>();

    internal static void RequeueTelemetry(IReadOnlyList<PlayLogEvent> events) =>
        telemetryBuffer?.Requeue(events);

    public static string SessionId => EnsureSession().SessionId;

    public static string CsvFilePath
    {
        get
        {
            EnsureInitialized();
            return csvFilePath ?? string.Empty;
        }
    }

    public static void ResetForTest()
    {
        initialized = false;
        session = null;
        csvFilePath = null;
        headerWritten = false;
        telemetryBuffer = null;
        DisableFileIoForTests = false;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlayerPrefs.DeleteKey(SessionIdPrefsKey);
        PlayerPrefs.Save();
        PlayLogSettings.ResetCacheForTest();
    }

    public static void RecordSceneEnter(string sceneName = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(sceneName);
        session.OnSceneEntered(scene, Time.unscaledTime);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypeSceneEnter,
            scene,
            userMessage: string.Empty,
            botResponse: string.Empty,
            hintLevel: string.Empty,
            incrementAttempt: false,
            normalizedQuestion: null));
    }

    public static void RecordCheshireUserMessage(string message, string progressState = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(null);
        string normalized = PlayLogCsvLogic.NormalizeQuestion(message);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypeCheshireUserMessage,
            scene,
            userMessage: message,
            botResponse: string.Empty,
            hintLevel: string.Empty,
            incrementAttempt: true,
            normalizedQuestion: normalized,
            progressState: progressState));
    }

    public static void RecordCheshireBotResponse(string response, string progressState = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(null);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypeCheshireBotResponse,
            scene,
            userMessage: string.Empty,
            botResponse: response,
            hintLevel: string.Empty,
            incrementAttempt: false,
            normalizedQuestion: null,
            progressState: progressState));
    }

    public static void RecordGiveHint(string hintLevel, string progressState = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(null);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypeGiveHint,
            scene,
            userMessage: string.Empty,
            botResponse: string.Empty,
            hintLevel: hintLevel,
            incrementAttempt: false,
            normalizedQuestion: null,
            progressState: progressState));
    }

    public static void RecordWrongAction(string progressState = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(null);
        session.IncrementWrongAction(scene);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypeWrongAction,
            scene,
            userMessage: string.Empty,
            botResponse: string.Empty,
            hintLevel: string.Empty,
            incrementAttempt: false,
            normalizedQuestion: null,
            progressState: progressState));
    }

    public static void RecordPuzzleSolved(string sceneName = null, string progressState = null)
    {
        EnsureInitialized();
        string scene = ResolveSceneName(sceneName);

        AppendEvent(BuildEvent(
            PlayLogCsvLogic.EventTypePuzzleSolved,
            scene,
            userMessage: string.Empty,
            botResponse: string.Empty,
            hintLevel: string.Empty,
            incrementAttempt: false,
            normalizedQuestion: null,
            progressState: progressState,
            forceSolved: true));
    }

    public static string BuildProgressStateSnapshot(string roomName = null)
    {
        var parts = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(roomName))
            parts.Append("room=").Append(roomName);

        QuestTrackerHudController questHud = QuestTrackerHudController.Instance;
        QuestTrackerState tracker = questHud != null ? questHud.TrackerState : null;
        if (tracker != null && !string.IsNullOrWhiteSpace(tracker.CurrentQuestId))
        {
            if (parts.Length > 0)
                parts.Append(';');
            parts.Append("quest=").Append(tracker.CurrentQuestId);
            parts.Append(";step=").Append(tracker.CurrentStepIndex);
            if (tracker.IsQuestCleared)
                parts.Append(";cleared=1");
        }

        return parts.ToString();
    }

    static PlayLogSessionContext EnsureSession()
    {
        if (session != null)
            return session;

        string existing = PlayerPrefs.GetString(SessionIdPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(existing))
        {
            existing = Guid.NewGuid().ToString("D");
            PlayerPrefs.SetString(SessionIdPrefsKey, existing);
            PlayerPrefs.Save();
        }

        session = new PlayLogSessionContext(existing);
        return session;
    }

    static void EnsureInitialized()
    {
        if (initialized)
            return;

        EnsureSession();
        SceneManager.sceneLoaded += HandleSceneLoaded;

        PlayLogSettings settings = PlayLogSettings.GetOrCreate();
        string fileName = settings.LogFileNamePattern.Replace("{session_id}", session.SessionId);
        string directory = Path.Combine(Application.persistentDataPath, settings.LogDirectoryName);
        Directory.CreateDirectory(directory);
        csvFilePath = Path.Combine(directory, fileName);

        telemetryBuffer = new PlayLogTelemetryBuffer(settings.TelemetryMaxBufferedEvents);

        initialized = true;
        RecordSceneEnter(SceneManager.GetActiveScene().name);
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!initialized || !scene.IsValid())
            return;

        RecordSceneEnter(scene.name);

        if (PuzzleSolvedStateProvider.IsSolved(scene.name))
            RecordPuzzleSolved(scene.name);
    }

    static PlayLogEvent BuildEvent(
        string eventType,
        string sceneName,
        string userMessage,
        string botResponse,
        string hintLevel,
        bool incrementAttempt,
        string normalizedQuestion,
        string progressState = null,
        bool forceSolved = false)
    {
        int attemptCount = incrementAttempt
            ? session.IncrementAttempt(sceneName)
            : session.GetAttemptCount(sceneName);

        int repeatedQuestionCount = 0;
        if (!string.IsNullOrEmpty(normalizedQuestion))
            repeatedQuestionCount = session.RegisterQuestion(sceneName, normalizedQuestion);

        return new PlayLogEvent(
            sessionId: session.SessionId,
            anonymousPlayerId: ChatHttpClient.ResolveChatClientUserId(),
            sceneName: sceneName,
            puzzleId: PlayLogCsvLogic.ResolvePuzzleId(sceneName),
            eventTime: DateTimeOffset.UtcNow,
            eventType: eventType,
            userMessage: userMessage,
            botResponse: botResponse,
            hintLevel: hintLevel,
            progressState: progressState ?? BuildProgressStateSnapshot(),
            timeSinceSceneStart: session.GetTimeSinceSceneStart(sceneName, Time.unscaledTime),
            attemptCount: attemptCount,
            wrongActionCount: session.GetWrongActionCount(sceneName),
            repeatedQuestionCount: repeatedQuestionCount,
            solved: forceSolved || PuzzleSolvedStateProvider.IsSolved(sceneName));
    }

    static void AppendEvent(PlayLogEvent evt)
    {
        PlayLogSettings settings = PlayLogSettings.GetOrCreate();
        if (DisableFileIoForTests)
            return;

        if (settings.EnableTelemetryUpload)
            telemetryBuffer?.Enqueue(evt);

        if (!settings.EnableCsvLogging || string.IsNullOrEmpty(csvFilePath))
            return;

        try
        {
            bool writeHeader = !headerWritten && !File.Exists(csvFilePath);
            using var stream = new FileStream(csvFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            if (writeHeader)
            {
                writer.WriteLine(PlayLogCsvLogic.BuildHeaderLine());
                headerWritten = true;
            }

            writer.WriteLine(PlayLogCsvLogic.FormatEventRow(evt, settings.IncludeMessageContent));
        }
        catch (Exception ex)
        {
            GameLog.LogWarning("[PlayLogRecorder] CSV append failed: " + ex.Message);
        }
    }

    static string ResolveSceneName(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
            return sceneName;

        return SceneManager.GetActiveScene().name ?? string.Empty;
    }
}
