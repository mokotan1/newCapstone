using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 세션·씬별 카운터와 씬 진입 시각. CSV 행 생성 시 스냅샷을 제공한다.
/// </summary>
public sealed class PlayLogSessionContext
{
    readonly Dictionary<string, float> sceneEnteredAtUnscaled = new Dictionary<string, float>(StringComparer.Ordinal);
    readonly Dictionary<string, int> attemptCountByScene = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<string, int> wrongActionCountByScene = new Dictionary<string, int>(StringComparer.Ordinal);
    readonly Dictionary<string, Dictionary<string, int>> questionCountByScene =
        new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

    public string SessionId { get; }

    public PlayLogSessionContext(string sessionId)
    {
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? Guid.NewGuid().ToString("D") : sessionId;
    }

    public void OnSceneEntered(string sceneName, float unscaledTime)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        sceneEnteredAtUnscaled[sceneName] = unscaledTime;
    }

    public float GetTimeSinceSceneStart(string sceneName, float unscaledTime)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0f;

        if (!sceneEnteredAtUnscaled.TryGetValue(sceneName, out float enteredAt))
            return 0f;

        return Mathf.Max(0f, unscaledTime - enteredAt);
    }

    public int IncrementAttempt(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0;

        attemptCountByScene.TryGetValue(sceneName, out int count);
        count += 1;
        attemptCountByScene[sceneName] = count;
        return count;
    }

    public int IncrementWrongAction(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0;

        wrongActionCountByScene.TryGetValue(sceneName, out int count);
        count += 1;
        wrongActionCountByScene[sceneName] = count;
        return count;
    }

    public int RegisterQuestion(string sceneName, string normalizedQuestion)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(normalizedQuestion))
            return 0;

        if (!questionCountByScene.TryGetValue(sceneName, out Dictionary<string, int> byQuestion))
        {
            byQuestion = new Dictionary<string, int>(StringComparer.Ordinal);
            questionCountByScene[sceneName] = byQuestion;
        }

        byQuestion.TryGetValue(normalizedQuestion, out int count);
        count += 1;
        byQuestion[normalizedQuestion] = count;
        return count;
    }

    public int GetAttemptCount(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0;

        return attemptCountByScene.TryGetValue(sceneName, out int count) ? count : 0;
    }

    public int GetWrongActionCount(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return 0;

        return wrongActionCountByScene.TryGetValue(sceneName, out int count) ? count : 0;
    }
}
