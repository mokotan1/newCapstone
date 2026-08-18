using System;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CheshirePromptCatalogTests
{
    Func<string, TextAsset> _previousLoader;

    [SetUp]
    public void SetUp()
    {
        _previousLoader = CheshirePromptCatalog.ResourceLoader;
    }

    [TearDown]
    public void TearDown()
    {
        CheshirePromptCatalog.ResourceLoader = _previousLoader;
    }

    [Test]
    public void BuildResourcePath_UsesRootLocaleAndKey()
    {
        Assert.AreEqual(
            $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.English}/BaseSystem",
            CheshirePromptCatalog.BuildResourcePath(CheshireLocaleResolver.English, "BaseSystem"));
    }

    [Test]
    public void Load_RequestedLocalePresent_ReturnsThatText()
    {
        CheshirePromptCatalog.ResourceLoader = path =>
        {
            if (path == $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.English}/BaseSystem")
                return new TextAsset("english-base");
            if (path == $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.Korean}/BaseSystem")
                return new TextAsset("korean-base");
            return null;
        };

        Assert.AreEqual(
            "english-base",
            CheshirePromptCatalog.Load("BaseSystem", CheshireLocaleResolver.English));
    }

    [Test]
    public void Load_MissingLocale_FallsBackToKorean()
    {
        CheshirePromptCatalog.ResourceLoader = path =>
        {
            if (path == $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.Korean}/BaseSystem")
                return new TextAsset("korean-base");
            return null;
        };

        Assert.AreEqual(
            "korean-base",
            CheshirePromptCatalog.Load("BaseSystem", CheshireLocaleResolver.Japanese));
    }

    [Test]
    public void Load_MissingBoth_ReturnsEmptyAndDoesNotThrow()
    {
        CheshirePromptCatalog.ResourceLoader = _ => null;

        Assert.DoesNotThrow(() =>
        {
            string text = CheshirePromptCatalog.Load(
                "MissingKeyThatDoesNotExist_XYZ",
                CheshireLocaleResolver.English);
            Assert.AreEqual(string.Empty, text);
        });
    }

    [Test]
    public void Load_EmptyOrWhitespacePromptKey_ReturnsEmpty()
    {
        CheshirePromptCatalog.ResourceLoader = _ => new TextAsset("should-not-load");

        Assert.AreEqual(
            string.Empty,
            CheshirePromptCatalog.Load(null, CheshireLocaleResolver.Korean));
        Assert.AreEqual(
            string.Empty,
            CheshirePromptCatalog.Load("", CheshireLocaleResolver.Korean));
        Assert.AreEqual(
            string.Empty,
            CheshirePromptCatalog.Load("   ", CheshireLocaleResolver.English));
    }

    [Test]
    public void Load_NormalizesLocaleAliasInResourcePath()
    {
        string capturedPath = null;
        CheshirePromptCatalog.ResourceLoader = path =>
        {
            capturedPath = path;
            return new TextAsset("english-base");
        };

        string text = CheshirePromptCatalog.Load("BaseSystem", "en-US");

        Assert.AreEqual("english-base", text);
        Assert.AreEqual(
            $"{CheshirePromptCatalog.ResourceRoot}/{CheshireLocaleResolver.English}/BaseSystem",
            capturedPath);
    }

    static readonly string[] StablePromptKeys =
    {
        "BaseSystem",
        "ChesterVoiceCommon",
        "introPrompt",
        "KitchenPrompt",
        "MainBedroomPrompt",
        "SonRoomPrompt",
        "StudyRoomPrompt",
        "TutorRoomPrompt",
        "WifeRoomPrompt",
        "ParrotPrompt",
    };

    static readonly string[] CatalogLocales =
    {
        CheshireLocaleResolver.Korean,
        CheshireLocaleResolver.Japanese,
        CheshireLocaleResolver.English,
    };

    [Test]
    public void Load_KoreanStableKeys_ResolveViaDefaultResourcesLoader()
    {
        // Restore production loader so real Resources/CheshirePrompts/ko assets are exercised.
        CheshirePromptCatalog.ResourceLoader = path => Resources.Load<TextAsset>(path);

        foreach (string key in StablePromptKeys)
        {
            string text = CheshirePromptCatalog.Load(key, CheshireLocaleResolver.Korean);
            Assert.IsFalse(
                string.IsNullOrEmpty(text),
                $"Expected non-empty Korean catalog text for key '{key}'");
        }
    }

    [Test]
    public void Load_AllLocales_StableKeys_NonEmptyViaDefaultResourcesLoader()
    {
        CheshirePromptCatalog.ResourceLoader = path => Resources.Load<TextAsset>(path);

        foreach (string locale in CatalogLocales)
        {
            foreach (string key in StablePromptKeys)
            {
                string text = CheshirePromptCatalog.Load(key, locale);
                Assert.IsFalse(
                    string.IsNullOrEmpty(text),
                    $"Expected non-empty catalog text for locale '{locale}' key '{key}'");
            }
        }
    }

    [Test]
    public void Resources_HintPolicyAndFragmentKeys_PresentForAllLocales()
    {
        CheshirePromptCatalog.ResourceLoader = path => Resources.Load<TextAsset>(path);

        string[] optionalKeys =
        {
            "HintPolicy_Novice",
            "HintPolicy_Intermediate",
            "HintPolicy_Expert",
            "Fragment_KitchenGiveFoodPost",
            "Fragment_KitchenGiveFoodSecret",
            "Fragment_StudyAlreadySolved",
        };

        foreach (string locale in CatalogLocales)
        {
            foreach (string key in optionalKeys)
            {
                string path = CheshirePromptCatalog.BuildResourcePath(locale, key);
                TextAsset asset = Resources.Load<TextAsset>(path);
                Assert.IsNotNull(asset, $"Missing Resources asset: {path}");
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(asset.text),
                    $"Empty Resources asset: {path}");
            }
        }
    }
}
