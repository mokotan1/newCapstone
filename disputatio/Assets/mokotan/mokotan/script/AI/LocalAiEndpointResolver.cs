using System;
using Newtonsoft.Json.Linq;

/// <summary>
/// Maps a Supervisor session file to chat/stream/grade/status URLs.
/// Remote Inspector/Resources URLs never win over an owned loopback session.
/// </summary>
public static class LocalAiEndpointResolver
{
    public const string ProtocolVersion = "1";
    public const string LoopbackChatUrl = "http://127.0.0.1:8000/chat";

    public static string ResolveChatUrl(string inspectorOrConfiguredUrl, LocalAiSessionSnapshot session)
    {
        if (session != null && session.IsUsable)
            return session.ChatUrl;

        if (LocalAiReadiness.RequiresLoopbackRuntime(inspectorOrConfiguredUrl))
            return inspectorOrConfiguredUrl;

        return LoopbackChatUrl;
    }

    public static string ResolveStreamUrl(string chatUrl)
    {
        if (string.IsNullOrWhiteSpace(chatUrl))
            return "";
        if (chatUrl.EndsWith("/chat/stream", StringComparison.OrdinalIgnoreCase))
            return chatUrl;
        if (chatUrl.EndsWith("/chat", StringComparison.OrdinalIgnoreCase))
            return chatUrl + "/stream";
        return LocalAiReadiness.ResolveRootUrl(chatUrl) + "chat/stream";
    }

    public static string ResolveGradeUrl(string chatUrl)
    {
        return LocalAiReadiness.ResolveRootUrl(chatUrl) + "tutor/grade";
    }

    public static string ResolveStatusUrl(string chatUrl)
    {
        return LocalAiReadiness.ResolveRootUrl(chatUrl);
    }

    public static bool ShouldAutoStart(bool isBatchMode, bool autoStartEnabled, bool manualStopLatched)
    {
        if (isBatchMode)
            return false;
        if (!autoStartEnabled)
            return false;
        if (manualStopLatched)
            return false;
        return true;
    }
}

public sealed class LocalAiSessionSnapshot
{
    public LocalAiSessionSnapshot(
        string protocolVersion,
        string sessionId,
        string instanceId,
        int parentPid,
        string parentStart,
        string projectId,
        string baseUrl,
        string token)
    {
        ProtocolVersion = protocolVersion ?? "";
        SessionId = sessionId ?? "";
        InstanceId = instanceId ?? "";
        ParentPid = parentPid;
        ParentStart = parentStart ?? "";
        ProjectId = projectId ?? "";
        BaseUrl = baseUrl ?? "";
        Token = token ?? "";
    }

    public string ProtocolVersion { get; }
    public string SessionId { get; }
    public string InstanceId { get; }
    public int ParentPid { get; }
    public string ParentStart { get; }
    public string ProjectId { get; }
    public string BaseUrl { get; }
    public string Token { get; }

    public string ChatUrl => string.IsNullOrEmpty(BaseUrl)
        ? ""
        : BaseUrl.TrimEnd('/') + "/chat";

    public bool IsUsable
    {
        get
        {
            if (ProtocolVersion != LocalAiEndpointResolver.ProtocolVersion)
                return false;
            if (string.IsNullOrEmpty(SessionId) || string.IsNullOrEmpty(InstanceId) || string.IsNullOrEmpty(BaseUrl))
                return false;
            if (InstanceId == "pending")
                return false;
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri uri))
                return false;
            if (uri.Port <= 0)
                return false;
            return LocalAiReadiness.RequiresLoopbackRuntime(ChatUrl);
        }
    }

    public static bool CanReconnect(LocalAiSessionSnapshot claimed, LocalAiSessionSnapshot live)
    {
        if (claimed == null || live == null)
            return false;
        return claimed.ProtocolVersion == live.ProtocolVersion
            && claimed.SessionId == live.SessionId
            && claimed.InstanceId == live.InstanceId
            && claimed.ParentPid == live.ParentPid
            && claimed.ParentStart == live.ParentStart
            && claimed.ProjectId == live.ProjectId;
    }

    public static bool TryParse(string json, string token, out LocalAiSessionSnapshot snapshot)
    {
        snapshot = null;
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

        if (root["protocol_version"]?.Type != JTokenType.String
            || root["session_id"]?.Type != JTokenType.String
            || root["instance_id"]?.Type != JTokenType.String
            || root["base_url"]?.Type != JTokenType.String)
            return false;

        snapshot = new LocalAiSessionSnapshot(
            root.Value<string>("protocol_version"),
            root.Value<string>("session_id"),
            root.Value<string>("instance_id"),
            root.Value<int?>("parent_pid") ?? 0,
            root.Value<string>("parent_start") ?? "",
            root.Value<string>("project_id") ?? "",
            root.Value<string>("base_url"),
            token ?? "");
        return true;
    }
}
