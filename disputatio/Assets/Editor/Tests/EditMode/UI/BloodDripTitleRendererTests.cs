using NUnit.Framework;
using TMPro;
using UnityEngine;

[TestFixture]
public class BloodDripTitleRendererTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        TitleStyleService.ResetCacheForTests();
        TitleFontRegistry.ResetCacheForTest();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ApplyPayload_SetsTextAndColor()
    {
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        var payload = TitleStylePayload.FromJson(@"{
            ""text"": ""TEST"",
            ""language"": ""en"",
            ""fontKey"": ""cinzel"",
            ""color"": ""#ff0000""
        }");

        renderer.ApplyPayload(payload);

        Assert.AreEqual("TEST", label.text);
        Assert.AreEqual(payload.Color, label.color);
    }

    [Test]
    public void RebuildGlyphAnchors_CountsVisibleNonWhitespaceCharacters()
    {
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        label.text = "AB";
        label.fontSize = 48f;

        var payload = TitleStylePayload.FromJson(@"{
            ""text"": ""AB"",
            ""language"": ""en"",
            ""fontKey"": ""cinzel""
        }");
        renderer.ApplyPayload(payload);
        Canvas.ForceUpdateCanvases();
        renderer.RefreshGlyphAnchors();

        Assert.GreaterOrEqual(renderer.GlyphAnchors.Count, 2);
        for (int i = 0; i < renderer.GlyphAnchors.Count; i++)
            Assert.IsFalse(char.IsWhiteSpace(renderer.GlyphAnchors[i].Character));
    }

    [Test]
    public void SampleRange_WithSeed_IsDeterministic()
    {
        var renderer = CreateRenderer(out _);
        var seededPayload = TitleStylePayload.FromJson(@"{
            ""text"": ""DISPUTATIO"",
            ""language"": ""en"",
            ""fontKey"": ""cinzel"",
            ""seed"": 1138
        }");

        renderer.PrepareRandomStateForTests(seededPayload);
        float first = renderer.SampleRangeForTests(10f, 20f);
        int indexFirst = renderer.SampleIndexForTests(5);

        renderer.PrepareRandomStateForTests(seededPayload);
        float second = renderer.SampleRangeForTests(10f, 20f);
        int indexSecond = renderer.SampleIndexForTests(5);

        Assert.AreEqual(first, second, 0.0001f);
        Assert.AreEqual(indexFirst, indexSecond);
    }

    BloodDripTitleRenderer CreateRenderer(out TextMeshProUGUI label)
    {
        root = new GameObject("BloodDripTitleRendererTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var labelGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvasGo.transform, false);
        label = labelGo.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 48f;
        label.text = "DISPUTATIO";
        label.rectTransform.sizeDelta = new Vector2(640f, 120f);

        var rendererGo = new GameObject("Renderer", typeof(BloodDripTitleRenderer));
        rendererGo.transform.SetParent(root.transform, false);
        var renderer = rendererGo.GetComponent<BloodDripTitleRenderer>();
        renderer.SetTitleTextForTests(label);
        renderer.SetLoadMockPayloadOnStartForTests(false);
        return renderer;
    }
}
