using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Maps backend <c>fontKey</c> values and language codes to <see cref="TMP_FontAsset"/> references.
/// Unknown keys fall back by language, then global fallback, then TMP default — rendering never throws.
/// </summary>
[CreateAssetMenu(fileName = "TitleFontRegistry", menuName = "Title/Title Font Registry")]
public class TitleFontRegistry : ScriptableObject
{
    public const string ResourcePath = "TitleFontRegistry";

    public const string LanguageKorean = "ko";
    public const string LanguageEnglish = "en";

    [Serializable]
    public class FontEntry
    {
        [Tooltip("Stable backend fontKey (e.g. cinzel, nanum). Case-insensitive lookup.")]
        [SerializeField] string fontKey;

        [Tooltip("TMP font asset for this fontKey.")]
        [SerializeField] TMP_FontAsset fontAsset;

        public string FontKey => fontKey;

        public TMP_FontAsset FontAsset => fontAsset;
    }

    [Serializable]
    public class LanguageFallbackEntry
    {
        [Tooltip("Language code (e.g. ko, en). Region suffixes such as ko-KR are normalized.")]
        [SerializeField] string languageCode;

        [Tooltip("TMP font used when fontKey is missing/unknown for this language.")]
        [SerializeField] TMP_FontAsset fontAsset;

        public string LanguageCode => languageCode;

        public TMP_FontAsset FontAsset => fontAsset;
    }

    [Header("Font key mappings")]
    [SerializeField] List<FontEntry> fontEntries = new List<FontEntry>();

    [Header("Language fallbacks")]
    [SerializeField] List<LanguageFallbackEntry> languageFallbacks = new List<LanguageFallbackEntry>();

    [Header("Last resort")]
    [Tooltip("Used when fontKey and language fallback both fail.")]
    [SerializeField] TMP_FontAsset globalFallback;

    public IReadOnlyList<FontEntry> FontEntries => fontEntries;

    public IReadOnlyList<LanguageFallbackEntry> LanguageFallbacks => languageFallbacks;

    public TMP_FontAsset GlobalFallback => globalFallback;

    /// <summary>
    /// Resolves a TMP font for the given backend fontKey and language without throwing.
    /// </summary>
    public TMP_FontAsset Resolve(string fontKey, string language)
    {
        if (TryResolve(fontKey, language, out TMP_FontAsset font))
            return font;

        return GetSafeFallbackFont();
    }

    /// <summary>
    /// Attempts to resolve a font. Returns false only when no asset could be found at all.
    /// </summary>
    public bool TryResolve(string fontKey, string language, out TMP_FontAsset fontAsset)
    {
        if (TryGetByFontKey(fontKey, out fontAsset))
            return true;

        fontAsset = GetLanguageFallback(language);
        if (fontAsset != null)
            return true;

        fontAsset = globalFallback;
        if (fontAsset != null)
            return true;

        fontAsset = GetTmpDefaultFontAsset();
        return fontAsset != null;
    }

    /// <summary>
    /// Looks up an explicit fontKey mapping. Does not apply language or global fallbacks.
    /// </summary>
    public bool TryGetByFontKey(string fontKey, out TMP_FontAsset fontAsset)
    {
        fontAsset = null;
        if (string.IsNullOrWhiteSpace(fontKey))
            return false;

        string normalizedKey = NormalizeKey(fontKey);
        for (int i = 0; i < fontEntries.Count; i++)
        {
            FontEntry entry = fontEntries[i];
            if (entry == null || entry.FontAsset == null)
                continue;

            if (!string.Equals(NormalizeKey(entry.FontKey), normalizedKey, StringComparison.Ordinal))
                continue;

            fontAsset = entry.FontAsset;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the configured language fallback, or null when none is assigned.
    /// </summary>
    public TMP_FontAsset GetLanguageFallback(string language)
    {
        string normalizedLanguage = NormalizeLanguage(language);
        if (string.IsNullOrEmpty(normalizedLanguage))
            return null;

        for (int i = 0; i < languageFallbacks.Count; i++)
        {
            LanguageFallbackEntry entry = languageFallbacks[i];
            if (entry == null || entry.FontAsset == null)
                continue;

            if (!string.Equals(NormalizeLanguage(entry.LanguageCode), normalizedLanguage, StringComparison.Ordinal))
                continue;

            return entry.FontAsset;
        }

        return null;
    }

    /// <summary>
    /// Best-effort font for unknown keys — never throws.
    /// </summary>
    public TMP_FontAsset GetSafeFallbackFont()
    {
        if (TryResolve(null, null, out TMP_FontAsset font))
            return font;

        GameLog.LogWarning("[TitleFontRegistry] No TMP font could be resolved; assign language/global fallbacks.");
        return null;
    }

    static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes language tags such as <c>ko-KR</c> or <c>EN_us</c> to a two-letter base code when possible.
    /// </summary>
    public static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return string.Empty;

        string trimmed = language.Trim().ToLowerInvariant();
        int separatorIndex = trimmed.IndexOf('-');
        if (separatorIndex < 0)
            separatorIndex = trimmed.IndexOf('_');

        if (separatorIndex > 0)
            trimmed = trimmed.Substring(0, separatorIndex);

        return trimmed;
    }

    static TMP_FontAsset GetTmpDefaultFontAsset()
    {
        return TMP_Settings.defaultFontAsset;
    }

    private static TitleFontRegistry _cached;

    internal static void ResetCacheForTest()
    {
        _cached = null;
    }

    public static TitleFontRegistry GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<TitleFontRegistry>(ResourcePath);
        return _cached;
    }
}
