/// <summary>
/// Pure send/status rules for the settings Cheshire preview.
/// HTTP stays on <see cref="ChatHttpClient"/>; this type only maps LocalAiReadiness to UI keys.
/// </summary>
public static class SettingsCheshirePreviewGate
{
    public const string StatusDisabled = "LocalAiDisabled";
    public const string StatusConnecting = "AiSettingsConnecting";
    public const string StatusReady = "SettingsPreviewReady";
    public const string StatusNotReady = "LocalAiNotReady";

    public static string IdleStatusKey(bool playerDisabled, bool requiresLoopback, bool localReady)
    {
        if (playerDisabled)
            return StatusDisabled;
        if (requiresLoopback && !localReady)
            return StatusConnecting;
        return StatusReady;
    }

    public static string BlockedAskKey(bool playerDisabled, bool requiresLoopback, bool localReady)
    {
        if (LocalAiReadiness.CanSendChat(playerDisabled, requiresLoopback, localReady))
            return null;
        if (playerDisabled)
            return StatusDisabled;
        return StatusNotReady;
    }

    public static bool CanAsk(bool playerDisabled, bool requiresLoopback, bool localReady)
    {
        return LocalAiReadiness.CanSendChat(playerDisabled, requiresLoopback, localReady);
    }

    public static bool IsServerReachable(long statusCode)
    {
        return statusCode >= 200 && statusCode < 300;
    }

    public static bool ShouldApplyDefaultDevice(
        bool playerDisabled, bool requiresLoopback, bool alreadyApplied, bool serverReachable)
    {
        return !playerDisabled && requiresLoopback && !alreadyApplied && serverReachable;
    }

    public static bool ShouldKeepPolling(bool playerDisabled, bool requiresLoopback, bool localReady)
    {
        return !playerDisabled && requiresLoopback && !localReady;
    }

    public static bool PanelOwnsIndependentStatusPoll(bool isEmbedded)
    {
        return !isEmbedded;
    }
}
