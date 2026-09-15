using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Decides whether Cheshire chat may send when the desktop build talks to loopback FastAPI.
/// Remote/cloud URLs skip the local-runtime gate. Player can disable dialogue AI without
/// deleting the installed Gemma files.
/// </summary>
public static class LocalAiReadiness
{
    public const string DisabledPrefsKey = "LocalAi.ChatDisabled";

    public static bool IsChatDisabled()
    {
        return PlayerPrefs.GetInt(DisabledPrefsKey, 0) != 0;
    }

    public static void SetChatDisabled(bool disabled)
    {
        PlayerPrefs.SetInt(DisabledPrefsKey, disabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool RequiresLoopbackRuntime(string chatUrl)
    {
        if (string.IsNullOrWhiteSpace(chatUrl))
            return false;

        if (!Uri.TryCreate(chatUrl, UriKind.Absolute, out Uri uri))
            return false;

        return uri.Host == "127.0.0.1" || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveRootUrl(string chatUrl)
    {
        if (string.IsNullOrWhiteSpace(chatUrl))
            return "";

        string trimmed = chatUrl.Trim();
        if (trimmed.EndsWith("/chat/stream", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - "/chat/stream".Length);
        else if (trimmed.EndsWith("/chat", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(0, trimmed.Length - "/chat".Length);

        return trimmed.TrimEnd('/') + "/";
    }

    public static bool CanSendChat(bool playerDisabled, bool requiresLoopback, bool localReady)
    {
        if (playerDisabled)
            return false;
        if (!requiresLoopback)
            return true;
        return localReady;
    }

    public static bool IsLocalModelReady(string json, long statusCode, bool requireLocalRuntime)
    {
        if (statusCode < 200 || statusCode >= 300)
            return false;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch
        {
            return false;
        }

        JToken runtime = root["local_runtime"];
        if (requireLocalRuntime)
        {
            if (runtime == null || runtime.Type == JTokenType.Null)
                return false;
            bool? modelAvailable = runtime.Value<bool?>("model_available");
            return modelAvailable == true;
        }

        return true;
    }
}
