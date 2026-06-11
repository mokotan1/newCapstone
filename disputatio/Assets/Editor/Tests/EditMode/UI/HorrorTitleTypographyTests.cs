using NUnit.Framework;
using TMPro;
using UnityEngine;

[TestFixture]
public class HorrorTitleTypographyTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        TitleFontRegistry.ResetCacheForTest();

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void BuildLineSizedRichText_SplitsLastWordOntoSmallerSecondLine()
    {
        string rich = HorrorTitleTypography.BuildLineSizedRichText("The Unholy of mention", 96f);

        StringAssert.Contains("The Unholy of", rich);
        StringAssert.Contains("mention", rich);
        StringAssert.Contains("<size=96>", rich);
        StringAssert.Contains("<size=75>", rich);
    }

    [Test]
    public void ApplyToMainMenu_PreservesPlainWording()
    {
        var label = CreateLabel("The Unholy of mention");

        HorrorTitleTypography.ApplyToMainMenu(label);

        string plain = HorrorTitleTypography.ExtractPlainText(label.text);
        Assert.AreEqual("The Unholy of mention", plain);
        Assert.Greater(label.characterSpacing, 0f);
        Assert.Less(label.lineSpacing, 0f);
        Assert.IsTrue(label.TryGetComponent(out HorrorTitleCharacterJitter _));
    }

    [Test]
    public void ExtractPlainText_RemovesRichTextTags()
    {
        string plain = HorrorTitleTypography.ExtractPlainText("<size=80>DISPUTATIO</size>");

        Assert.AreEqual("DISPUTATIO", plain);
    }

    TextMeshProUGUI CreateLabel(string text)
    {
        root = new GameObject("HorrorTitleTypographyTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);

        var labelGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(canvasGo.transform, false);

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 96f;
        label.rectTransform.sizeDelta = new Vector2(780f, 220f);
        return label;
    }
}
