using System;
using System.IO;
using Newtonsoft.Json.Linq;

/// <summary>
/// Loopback local-AI control contract. Unity never starts or kills inference
/// processes. Default requested device is GPU (CUDA sidecar after Gate 0 miss).
/// </summary>
public readonly struct LocalAiRuntimeStatus
{
    public LocalAiRuntimeStatus(
        string requestedMode,
        string effectiveBackend,
        string state,
        bool inferenceReady,
        string fallbackReason,
        string gpuName = "",
        float? gpuUtilization = null,
        float? gpuMemoryUsedMiB = null,
        float? gpuMemoryTotalMiB = null)
    {
        RequestedMode = requestedMode ?? "";
        EffectiveBackend = effectiveBackend ?? "";
        State = state ?? "";
        InferenceReady = inferenceReady;
        FallbackReason = fallbackReason ?? "";
        GpuName = gpuName ?? "";
        GpuUtilization = gpuUtilization;
        GpuMemoryUsedMiB = gpuMemoryUsedMiB;
        GpuMemoryTotalMiB = gpuMemoryTotalMiB;
    }

    public string RequestedMode { get; }
    public string EffectiveBackend { get; }
    public string State { get; }
    public bool InferenceReady { get; }
    public string FallbackReason { get; }
    public string GpuName { get; }
    public float? GpuUtilization { get; }
    public float? GpuMemoryUsedMiB { get; }
    public float? GpuMemoryTotalMiB { get; }
    public bool CanApply => State == "ready" || State == "failed" || State == "stopped";
}

public static class LocalAiControlApi
{
    public const string DefaultRequestedMode = "gpu";
    public const string DefaultGpuOffload = "medium";

    public static bool ShouldControlLocalRuntime(string chatUrl)
    {
        return LocalAiReadiness.RequiresLoopbackRuntime(chatUrl);
    }

    public static string StatusUrl(string chatUrl)
    {
        return LocalAiReadiness.ResolveRootUrl(chatUrl) + "local-ai/status";
    }

    public static string SettingsUrl(string chatUrl)
    {
        return LocalAiReadiness.ResolveRootUrl(chatUrl) + "local-ai/settings";
    }

    public static string BuildSettingsBody(string mode = null, string gpuOffload = null)
    {
        string resolvedMode = string.IsNullOrWhiteSpace(mode) ? DefaultRequestedMode : mode.Trim();
        string resolvedOffload = string.IsNullOrWhiteSpace(gpuOffload)
            ? DefaultGpuOffload
            : gpuOffload.Trim();
        if (resolvedMode != "auto" && resolvedMode != "cpu" && resolvedMode != "gpu")
            throw new ArgumentException("Unsupported local AI mode", nameof(mode));
        if (resolvedOffload != "low" && resolvedOffload != "medium" && resolvedOffload != "high")
            throw new ArgumentException("Unsupported GPU offload", nameof(gpuOffload));
        return new JObject { ["mode"] = resolvedMode, ["gpu_offload"] = resolvedOffload }.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static string DefaultControlTokenPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Disputatio", "local-ai", "control.token");
    }

    public static bool TryReadControlToken(string fileText, out string token)
    {
        token = (fileText ?? "").Trim();
        return token.Length > 0;
    }

    public static bool TryParseStatus(string json, out LocalAiRuntimeStatus status)
    {
        status = default;
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

        try
        {
        if (root["state"]?.Type != JTokenType.String || root["requested_mode"]?.Type != JTokenType.String)
            return false;
        JObject gpu = root["gpu"] as JObject;
        status = new LocalAiRuntimeStatus(
            root.Value<string>("requested_mode") ?? "",
            root.Value<string>("effective_backend") ?? "",
            root.Value<string>("state") ?? "",
            root.Value<bool?>("inference_ready") == true,
            root.Value<string>("fallback_reason") ?? "",
            gpu?.Value<string>("name"), gpu?.Value<float?>("utilization_percent"),
            gpu?.Value<float?>("memory_used_mib"), gpu?.Value<float?>("memory_total_mib"));
        return true;
        }
        catch
        {
            return false;
        }
    }
}
