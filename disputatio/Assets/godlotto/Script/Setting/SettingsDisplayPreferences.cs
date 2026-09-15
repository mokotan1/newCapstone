using Fungus;
using UnityEngine;

/// <summary>
/// Locale and Cheshire answer scale for the shared settings shell.
/// Existing audio/video keys stay on <see cref="SettingPlayerPrefsKeys"/>.
/// </summary>
public static class SettingsDisplayPreferences
{
    public static int GetAnswerTextScale()
    {
        int stored = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.AnswerTextScale, 100);
        return SettingsWoodPanelSpec.NormalizeScalePercent(stored);
    }

    public static void SetAnswerTextScale(int percent)
    {
        int normalized = SettingsWoodPanelSpec.NormalizeScalePercent(percent);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.AnswerTextScale, normalized);
        PlayerPrefs.Save();
    }

    public static float ResolveAnswerFontSize()
    {
        return SettingsWoodPanelSpec.AnswerFontSizeForScale(GetAnswerTextScale());
    }

    public static string ResolveLocale()
    {
        return CheshireLocaleResolver.ResolveCurrentLocale();
    }

    public static void ApplyLocale(string locale)
    {
        string normalized = CheshireLocaleResolver.NormalizeLocale(locale);
        SetLanguage.mostRecentLanguage = normalized;

        Localization[] localizations = Object.FindObjectsByType<Localization>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < localizations.Length; i++)
        {
            if (localizations[i] != null)
                localizations[i].SetActiveLanguage(normalized, true);
        }
    }
}
