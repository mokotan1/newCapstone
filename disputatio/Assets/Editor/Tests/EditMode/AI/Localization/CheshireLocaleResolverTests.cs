using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class CheshireLocaleResolverTests
{
    [TestCase("KO", CheshireLocaleResolver.Korean)]
    [TestCase("ko-KR", CheshireLocaleResolver.Korean)]
    [TestCase("Korean", CheshireLocaleResolver.Korean)]
    [TestCase("JA", CheshireLocaleResolver.Japanese)]
    [TestCase("JP", CheshireLocaleResolver.Japanese)]
    [TestCase("ja-JP", CheshireLocaleResolver.Japanese)]
    [TestCase("Japanese", CheshireLocaleResolver.Japanese)]
    [TestCase("EN", CheshireLocaleResolver.English)]
    [TestCase("en-US", CheshireLocaleResolver.English)]
    [TestCase("English", CheshireLocaleResolver.English)]
    public void NormalizeLocale_MapsAliases(string raw, string expected)
    {
        Assert.AreEqual(expected, CheshireLocaleResolver.NormalizeLocale(raw));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("fr")]
    [TestCase("zh-CN")]
    public void NormalizeLocale_UnsupportedOrEmpty_FallsBackToKo(string raw)
    {
        Assert.AreEqual(CheshireLocaleResolver.Korean, CheshireLocaleResolver.NormalizeLocale(raw));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void ResolveCurrentLocale_EmptyMostRecent_NoUsableLocalization_FallsBackToKo(
        string mostRecent)
    {
        string previousMostRecent = Fungus.SetLanguage.mostRecentLanguage;
        List<(Fungus.Localization loc, string active)> restored = SnapshotAndClearActiveLanguages();
        try
        {
            Fungus.SetLanguage.mostRecentLanguage = mostRecent;
            Assert.AreEqual(
                CheshireLocaleResolver.Korean,
                CheshireLocaleResolver.ResolveCurrentLocale());
        }
        finally
        {
            RestoreActiveLanguages(restored);
            Fungus.SetLanguage.mostRecentLanguage = previousMostRecent;
        }
    }

    [Test]
    public void ResolveCurrentLocale_UsesActiveLanguageWhenMostRecentCleared()
    {
        string previousMostRecent = Fungus.SetLanguage.mostRecentLanguage;
        List<(Fungus.Localization loc, string active)> restored = SnapshotAndClearActiveLanguages();
        GameObject host = null;
        try
        {
            Fungus.SetLanguage.mostRecentLanguage = "";
            Fungus.Localization target =
                Object.FindFirstObjectByType<Fungus.Localization>(FindObjectsInactive.Include);
            if (target == null)
            {
                host = new GameObject("CheshireLocaleResolverTests_Localization");
                target = host.AddComponent<Fungus.Localization>();
            }

            SetLocalizationActiveLanguage(target, "ja");
            Assert.AreEqual(
                CheshireLocaleResolver.Japanese,
                CheshireLocaleResolver.ResolveCurrentLocale());
        }
        finally
        {
            if (host != null)
                Object.DestroyImmediate(host);
            RestoreActiveLanguages(restored);
            Fungus.SetLanguage.mostRecentLanguage = previousMostRecent;
        }
    }

    [Test]
    public void ResolveCurrentLocale_PrefersMostRecentLanguageOverActive()
    {
        string previousMostRecent = Fungus.SetLanguage.mostRecentLanguage;
        List<(Fungus.Localization loc, string active)> restored = SnapshotAndClearActiveLanguages();
        GameObject host = null;
        try
        {
            Fungus.Localization target =
                Object.FindFirstObjectByType<Fungus.Localization>(FindObjectsInactive.Include);
            if (target == null)
            {
                host = new GameObject("CheshireLocaleResolverTests_Localization");
                target = host.AddComponent<Fungus.Localization>();
            }

            SetLocalizationActiveLanguage(target, "ja");
            Fungus.SetLanguage.mostRecentLanguage = "en-US";
            Assert.AreEqual(
                CheshireLocaleResolver.English,
                CheshireLocaleResolver.ResolveCurrentLocale());
        }
        finally
        {
            if (host != null)
                Object.DestroyImmediate(host);
            RestoreActiveLanguages(restored);
            Fungus.SetLanguage.mostRecentLanguage = previousMostRecent;
        }
    }

    static List<(Fungus.Localization loc, string active)> SnapshotAndClearActiveLanguages()
    {
        var restored = new List<(Fungus.Localization loc, string active)>();
        Fungus.Localization[] locs = Object.FindObjectsByType<Fungus.Localization>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < locs.Length; i++)
        {
            Fungus.Localization loc = locs[i];
            restored.Add((loc, loc.ActiveLanguage));
            SetLocalizationActiveLanguage(loc, "");
        }

        return restored;
    }

    static void RestoreActiveLanguages(List<(Fungus.Localization loc, string active)> restored)
    {
        for (int i = 0; i < restored.Count; i++)
        {
            (Fungus.Localization loc, string active) = restored[i];
            if (loc != null)
                SetLocalizationActiveLanguage(loc, active);
        }
    }

    /// <summary>
    /// EditMode cannot call <see cref="Fungus.Localization.SetActiveLanguage"/> (Play Mode only
    /// and requires a CSV). Set the serialized field directly.
    /// </summary>
    static void SetLocalizationActiveLanguage(Fungus.Localization localization, string language)
    {
        var so = new SerializedObject(localization);
        SerializedProperty prop = so.FindProperty("activeLanguage");
        Assert.IsNotNull(prop, "Fungus.Localization.activeLanguage serialize field missing");
        prop.stringValue = language ?? string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
