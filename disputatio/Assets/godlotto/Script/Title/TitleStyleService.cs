using UnityEngine;

/// <summary>
/// Loads title-style payloads for the blood-drip title renderer.
/// Networking is deferred; local mock JSON under <see cref="MockResourcePath"/> is used first.
/// </summary>
public static class TitleStyleService
{
    public const string MockResourcePath = "TitleStyle/MockTitleStyle";
    public const string MockKoreanResourcePath = "TitleStyle/MockTitleStyleKo";

    static TitleStylePayload cachedMockPayload;
    static TitleStylePayload cachedKoreanMockPayload;

    public static TitleStylePayload LoadMockPayload()
    {
        if (cachedMockPayload != null)
            return cachedMockPayload;

        var asset = Resources.Load<TextAsset>(MockResourcePath);
        if (asset == null)
        {
            GameLog.LogWarning(
                $"[TitleStyleService] Resources/{MockResourcePath} not found — using runtime defaults.");
            cachedMockPayload = TitleStylePayload.CreateDefault();
            return cachedMockPayload;
        }

        cachedMockPayload = TitleStylePayload.FromJson(asset.text);
        return cachedMockPayload;
    }

    public static bool TryLoadMockPayload(out TitleStylePayload payload)
    {
        payload = LoadMockPayload();
        return payload != null;
    }

    public static TitleStylePayload CreateDefaultPayload() => TitleStylePayload.CreateDefault();

    public static TitleStylePayload LoadKoreanMockPayload()
    {
        if (cachedKoreanMockPayload != null)
            return cachedKoreanMockPayload;

        var asset = Resources.Load<TextAsset>(MockKoreanResourcePath);
        if (asset == null)
        {
            GameLog.LogWarning(
                $"[TitleStyleService] Resources/{MockKoreanResourcePath} not found — using Korean defaults.");
            cachedKoreanMockPayload = TitleStylePayload.FromJson(@"{
                ""text"": ""피의 논쟁"",
                ""language"": ""ko"",
                ""fontKey"": ""nanum""
            }");
            return cachedKoreanMockPayload;
        }

        cachedKoreanMockPayload = TitleStylePayload.FromJson(asset.text);
        return cachedKoreanMockPayload;
    }

    internal static void ResetCacheForTests()
    {
        cachedMockPayload = null;
        cachedKoreanMockPayload = null;
    }
}
