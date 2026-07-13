using System;
using UnityEngine;

/// <summary>
/// Loads Cheshire system/room prompts from Resources under
/// <c>CheshirePrompts/{locale}/{key}</c>, falling back to Korean when missing.
/// </summary>
public static class CheshirePromptCatalog
{
    public const string ResourceRoot = "CheshirePrompts";

    public static Func<string, TextAsset> ResourceLoader { get; set; } =
        path => Resources.Load<TextAsset>(path);

    public static string BuildResourcePath(string locale, string promptKey)
    {
        return $"{ResourceRoot}/{locale}/{promptKey}";
    }

    public static string Load(string promptKey, string locale)
    {
        if (string.IsNullOrWhiteSpace(promptKey))
            return string.Empty;

        string canonical = CheshireLocaleResolver.NormalizeLocale(locale);
        string primary = TryLoadText(BuildResourcePath(canonical, promptKey));
        if (!string.IsNullOrEmpty(primary))
            return primary;

        if (canonical != CheshireLocaleResolver.Korean)
        {
            string fallback = TryLoadText(BuildResourcePath(CheshireLocaleResolver.Korean, promptKey));
            if (!string.IsNullOrEmpty(fallback))
            {
                GameLog.LogWarning(
                    $"[CheshirePromptCatalog] missing '{promptKey}' for locale '{canonical}', using ko");
                return fallback;
            }
        }

        GameLog.LogWarning(
            $"[CheshirePromptCatalog] missing prompt key '{promptKey}' locale '{canonical}' (and ko)");
        return string.Empty;
    }

    static string TryLoadText(string path)
    {
        TextAsset asset = ResourceLoader != null ? ResourceLoader(path) : null;
        return asset != null ? asset.text : null;
    }
}
