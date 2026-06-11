using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Normalized backend title-style payload for the blood-drip title renderer.
/// Contract: <c>docs/blood-drip-title-final.html</c> → <c>#cursorUnityPortSpec</c> → <c>backendContract.fields</c>.
/// </summary>
[Serializable]
public sealed class TitleStylePayload
{
    public const string DefaultText = "DISPUTATIO";
    public const string DefaultLanguage = "en";
    public const string DefaultFontKey = "cinzel";
    public const string DefaultColorHex = "#c11414";
    public const string DefaultDarkColorHex = "#4a0101";
    public const string DefaultBrightColorHex = "#ff2a2a";
    public const float DefaultDripIntensity = 0.75f;
    public const bool DefaultPoolEnabled = true;

    [Serializable]
    public class JsonDto
    {
        public string text;
        public string language;
        public string fontKey;
        public string color;
        public string darkColor;
        public string brightColor;
        public float dripIntensity;
        public bool poolEnabled;
        public int seed;
    }

    public string Text { get; private set; } = DefaultText;
    public string Language { get; private set; } = DefaultLanguage;
    public string FontKey { get; private set; } = DefaultFontKey;

    public string ColorHex { get; private set; } = DefaultColorHex;
    public string DarkColorHex { get; private set; } = DefaultDarkColorHex;
    public string BrightColorHex { get; private set; } = DefaultBrightColorHex;

    public Color Color { get; private set; }
    public Color DarkColor { get; private set; }
    public Color BrightColor { get; private set; }

    public float DripIntensity { get; private set; } = DefaultDripIntensity;
    public bool PoolEnabled { get; private set; } = DefaultPoolEnabled;
    public bool HasSeed { get; private set; }
    public int Seed { get; private set; }

    public static TitleStylePayload CreateDefault() => Normalize(new JsonDto(), new JObject());

    public static TitleStylePayload FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            GameLog.LogWarning("[TitleStylePayload] Empty JSON — using defaults.");
            return CreateDefault();
        }

        try
        {
            var root = JObject.Parse(json);
            var dto = root.ToObject<JsonDto>() ?? new JsonDto();
            return Normalize(dto, root);
        }
        catch (Exception ex)
        {
            GameLog.LogWarning($"[TitleStylePayload] Failed to parse JSON: {ex.Message}");
            return CreateDefault();
        }
    }

    internal static TitleStylePayload Normalize(JsonDto dto, JObject source)
    {
        var payload = new TitleStylePayload();

        payload.Text = CoalesceRequiredString(dto.text, DefaultText, nameof(dto.text));
        payload.Language = CoalesceRequiredString(dto.language, DefaultLanguage, nameof(dto.language));
        payload.FontKey = CoalesceRequiredString(dto.fontKey, DefaultFontKey, nameof(dto.fontKey));

        bool hasColor = HasJsonKey(source, nameof(dto.color));
        bool hasDarkColor = HasJsonKey(source, nameof(dto.darkColor));
        bool hasBrightColor = HasJsonKey(source, nameof(dto.brightColor));
        bool hasDripIntensity = HasJsonKey(source, nameof(dto.dripIntensity));
        bool hasPoolEnabled = HasJsonKey(source, nameof(dto.poolEnabled));
        bool hasSeed = HasJsonKey(source, nameof(dto.seed));

        payload.ColorHex = ResolveHex(dto.color, DefaultColorHex, hasColor);
        payload.DarkColorHex = ResolveHex(dto.darkColor, DefaultDarkColorHex, hasDarkColor);
        payload.BrightColorHex = ResolveHex(dto.brightColor, DefaultBrightColorHex, hasBrightColor);

        payload.Color = ParseHexColor(payload.ColorHex, ParseHexColor(DefaultColorHex, Color.red));
        payload.DarkColor = ParseHexColor(payload.DarkColorHex, ParseHexColor(DefaultDarkColorHex, Color.black));
        payload.BrightColor = ParseHexColor(payload.BrightColorHex, ParseHexColor(DefaultBrightColorHex, Color.red));

        payload.DripIntensity = hasDripIntensity
            ? Clamp01(dto.dripIntensity)
            : DefaultDripIntensity;

        payload.PoolEnabled = hasPoolEnabled ? dto.poolEnabled : DefaultPoolEnabled;
        payload.HasSeed = hasSeed;
        payload.Seed = hasSeed ? dto.seed : 0;

        return payload;
    }

    public static float ClampDripIntensity(float value) => Clamp01(value);

    static string CoalesceRequiredString(string value, string fallback, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return value.Trim();

        if (!string.IsNullOrWhiteSpace(fallback))
            GameLog.LogWarning($"[TitleStylePayload] Missing required field '{fieldName}' — using '{fallback}'.");

        return fallback;
    }

    static string ResolveHex(string value, string fallback, bool wasProvided)
    {
        if (!wasProvided || string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Trim();
    }

    static bool HasJsonKey(JObject source, string key)
    {
        return source != null && source.TryGetValue(key, out _);
    }

    static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return DefaultDripIntensity;

        return Mathf.Clamp01(value);
    }

    public static Color ParseHexColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        string normalized = hex.Trim();
        if (!normalized.StartsWith("#", StringComparison.Ordinal))
            normalized = "#" + normalized;

        return ColorUtility.TryParseHtmlString(normalized, out Color parsed) ? parsed : fallback;
    }
}
