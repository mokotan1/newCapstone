using NUnit.Framework;
using TMPro;
using UnityEngine;

[TestFixture]
public class BloodDripTitlePreserveTextTests
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
    public void ApplyVisualsOnly_DoesNotReplaceExistingTitleText()
    {
        const string sceneTitle = "The Unholy of mention";
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        label.text = sceneTitle;

        var payload = TitleStyleService.LoadMockPayload();
        renderer.ApplyVisualsOnly(payload);

        Assert.AreEqual(sceneTitle, label.text);
        Assert.AreNotEqual(payload.Text, label.text);
    }

    [Test]
    public void ApplyEffectToExistingTitle_DoesNotReplaceExistingTitleText()
    {
        const string sceneTitle = "The Unholy of mention";
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        label.text = sceneTitle;

        renderer.ApplyEffectToExistingTitle(TitleStyleService.LoadMockPayload());

        Assert.AreEqual(sceneTitle, label.text);
    }

    [Test]
    public void ApplyVisualsOnly_StillAppliesColorAndGlyphAnchors()
    {
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        label.text = "AB";
        label.fontSize = 48f;

        var payload = TitleStyleService.LoadMockPayload();
        renderer.ApplyVisualsOnly(payload);
        Canvas.ForceUpdateCanvases();
        renderer.RefreshGlyphAnchors();

        Assert.AreEqual(payload.Color, label.color);
        Assert.GreaterOrEqual(renderer.GlyphAnchors.Count, 2);
    }

    [Test]
    public void ApplyPayload_StillReplacesTitleText()
    {
        var renderer = CreateRenderer(out TextMeshProUGUI label);
        label.text = "The Unholy of mention";

        var payload = TitleStyleService.LoadMockPayload();
        renderer.ApplyPayload(payload);

        Assert.AreEqual(payload.Text, label.text);
    }

    BloodDripTitleRenderer CreateRenderer(out TextMeshProUGUI label)
    {
        root = new GameObject("BloodDripTitlePreserveTextTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var dripContainerGo = new GameObject("BloodDripContainer", typeof(RectTransform));
        dripContainerGo.transform.SetParent(canvasGo.transform, false);
        var dripContainer = dripContainerGo.GetComponent<RectTransform>();
        dripContainer.anchorMin = Vector2.zero;
        dripContainer.anchorMax = Vector2.one;
        dripContainer.offsetMin = Vector2.zero;
        dripContainer.offsetMax = Vector2.zero;

        var labelGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvasGo.transform, false);
        label = labelGo.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 48f;
        label.rectTransform.sizeDelta = new Vector2(640f, 120f);

        var rendererGo = new GameObject("Renderer", typeof(BloodDripTitleRenderer));
        rendererGo.transform.SetParent(root.transform, false);
        var renderer = rendererGo.GetComponent<BloodDripTitleRenderer>();
        renderer.SetTitleTextForTests(label);
        renderer.SetLoadMockPayloadOnStartForTests(false);

        var dripContainerField = typeof(BloodDripTitleRenderer).GetField(
            "dripContainer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        dripContainerField?.SetValue(renderer, dripContainer);

        return renderer;
    }
}
