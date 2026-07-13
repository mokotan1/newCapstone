using UnityEngine;

/// <summary>
/// Read-only locale authority for Cheshire AI: Fungus language → canonical ko|ja|en.
/// Does not introduce a second settings store or rewrite already-displayed chat text.
/// </summary>
public static class CheshireLocaleResolver
{
    public const string Korean = "ko";
    public const string Japanese = "ja";
    public const string English = "en";

    public static string NormalizeLocale(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Korean;

        string s = raw.Trim();
        int sep = s.IndexOfAny(new[] { '-', '_' });
        if (sep > 0)
            s = s.Substring(0, sep);

        s = s.Trim().ToLowerInvariant();

        switch (s)
        {
            case "ko":
            case "kr":
            case "korean":
                return Korean;
            case "ja":
            case "jp":
            case "japanese":
                return Japanese;
            case "en":
            case "english":
                return English;
            default:
                return Korean;
        }
    }

    public static string ResolveCurrentLocale()
    {
        if (!string.IsNullOrWhiteSpace(Fungus.SetLanguage.mostRecentLanguage))
            return NormalizeLocale(Fungus.SetLanguage.mostRecentLanguage);

        Fungus.Localization loc =
            Object.FindFirstObjectByType<Fungus.Localization>(FindObjectsInactive.Include);
        if (loc != null && !string.IsNullOrWhiteSpace(loc.ActiveLanguage))
            return NormalizeLocale(loc.ActiveLanguage);

        return Korean;
    }
}
