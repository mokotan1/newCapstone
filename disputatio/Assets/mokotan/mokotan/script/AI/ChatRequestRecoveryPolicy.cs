using UnityEngine.Networking;

/// <summary>
/// Classifies whether a chat HTTP attempt should be retried once.
/// Unity often reports request timeouts as <see cref="UnityWebRequest.Result.ConnectionError"/>.
/// </summary>
public static class ChatRequestRecoveryPolicy
{
    /// <summary>
    /// Returns true for a single automatic retry on connection errors/timeouts
    /// and HTTP 408 / 429 / 5xx. <paramref name="attempt"/> is 0-based.
    /// </summary>
    public static bool ShouldRetry(UnityWebRequest.Result result, long responseCode, int attempt)
    {
        if (attempt >= 1)
            return false;
        if (result == UnityWebRequest.Result.ConnectionError)
            return true;
        return responseCode == 408 || responseCode == 429 || responseCode >= 500;
    }
}
