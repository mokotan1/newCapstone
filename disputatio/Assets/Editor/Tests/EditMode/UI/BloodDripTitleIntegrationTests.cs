using NUnit.Framework;
using TMPro;
using UnityEngine;

/// <summary>
/// Smoke-test coverage for BTD-06 integration: mock payload load, renderer apply, Korean switch.
/// Manual play-mode check: see <see cref="BloodDripTitleDemo.SmokeTestNote"/>.
/// </summary>
[TestFixture]
public class BloodDripTitleIntegrationTests
{
    GameObject root;

    [SetUp]
    public void SetUp()
    {
        TitleStyleService.ResetCacheForTests();
        TitleFontRegistry.ResetCacheForTest();
    }

    [TearDown]
    public void TearDown()
    {
        TitleStyleService.ResetCacheForTests();
        TitleFontRegistry.ResetCacheForTest();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void LoadMockPayload_DefaultsToDisputatioEnglish()
    {
        var payload = TitleStyleService.LoadMockPayload();

        Assert.AreEqual("DISPUTATIO", payload.Text);
        Assert.AreEqual("en", payload.Language);
        Assert.AreEqual("cinzel", payload.FontKey);
    }

    [Test]
    public void LoadKoreanMockPayload_UsesKoreanTextAndNanumKey()
    {
        var payload = TitleStyleService.LoadKoreanMockPayload();

        Assert.AreEqual("ko", payload.Language);
        Assert.AreEqual("nanum", payload.FontKey);
        Assert.IsFalse(string.IsNullOrWhiteSpace(payload.Text));
        Assert.IsFalse(payload.Text.Contains("DISPUTATIO", System.StringComparison.Ordinal));
    }

    [Test]
    public void Demo_ApplyEnglishThenKorean_UpdatesRendererText()
    {
        var demo = CreateDemoHarness(out TextMeshProUGUI label);

        demo.ApplyEnglishMock();
        Assert.AreEqual("DISPUTATIO", label.text);

        demo.ApplyKoreanMock();
        Assert.AreEqual(TitleStyleService.LoadKoreanMockPayload().Text, label.text);
    }

    [Test]
    public void Demo_ApplyKoreanMock_ResolvesLanguageFallbackFontWhenRegistryPresent()
    {
        var registry = ScriptableObject.CreateInstance<TitleFontRegistry>();
        var demo = CreateDemoHarness(out TextMeshProUGUI label, registry);

        demo.ApplyKoreanMock();

        Assert.IsNotNull(label.font);
        Assert.AreEqual("ko", TitleStyleService.LoadKoreanMockPayload().Language);
    }

    BloodDripTitleDemo CreateDemoHarness(out TextMeshProUGUI label, TitleFontRegistry registry = null)
    {
        root = new GameObject("BloodDripTitleIntegrationTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var labelGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvasGo.transform, false);
        label = labelGo.GetComponent<TextMeshProUGUI>();
        label.fontSize = 48f;

        var rendererGo = new GameObject("Renderer", typeof(BloodDripTitleRenderer));
        rendererGo.transform.SetParent(root.transform, false);
        var renderer = rendererGo.GetComponent<BloodDripTitleRenderer>();
        renderer.SetTitleTextForTests(label);
        renderer.SetLoadMockPayloadOnStartForTests(false);
        if (registry != null)
        {
            var registryField = typeof(BloodDripTitleRenderer).GetField(
                "fontRegistry",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            registryField?.SetValue(renderer, registry);
        }

        var demoGo = new GameObject("Demo", typeof(BloodDripTitleDemo));
        demoGo.transform.SetParent(root.transform, false);
        var demo = demoGo.GetComponent<BloodDripTitleDemo>();
        var rendererProp = typeof(BloodDripTitleDemo).GetField(
            "renderer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        rendererProp?.SetValue(demo, renderer);
        return demo;
    }
}
