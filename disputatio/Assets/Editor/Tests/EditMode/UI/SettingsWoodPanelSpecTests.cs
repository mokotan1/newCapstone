using NUnit.Framework;
using UnityEngine;

public class SettingsWoodPanelSpecTests
{
    [Test]
    public void ColorsMatchHtmlSpecHex()
    {
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0x201B17), SettingsWoodPanelSpec.PanelBackground);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0x151310), SettingsWoodPanelSpec.InputBackground);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0xE8D6AC), SettingsWoodPanelSpec.PrimaryText);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0xB3A384), SettingsWoodPanelSpec.SecondaryText);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0x65503A), SettingsWoodPanelSpec.Border);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0xD0AB5D), SettingsWoodPanelSpec.Accent);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0x49351E), SettingsWoodPanelSpec.SelectedBackground);
        Assert.AreEqual(SettingsWoodPanelSpec.Hex(0xA3B875), SettingsWoodPanelSpec.ReadyStatus);
    }

    [Test]
    public void CanvasScaleUsesMinOfReferenceRatios()
    {
        Assert.AreEqual(720f / 1080f, SettingsWoodPanelSpec.CanvasScale(1280f, 720f), 0.0001f);
        Assert.AreEqual(768f / 1080f, SettingsWoodPanelSpec.CanvasScale(1366f, 768f), 0.0001f);
        Assert.AreEqual(1f, SettingsWoodPanelSpec.CanvasScale(1920f, 1080f), 0.0001f);
        Assert.AreEqual(1440f / 1080f, SettingsWoodPanelSpec.CanvasScale(3440f, 1440f), 0.0001f);
        Assert.AreEqual(1f, SettingsWoodPanelSpec.CanvasScale(2560f, 1080f), 0.0001f);
    }

    [Test]
    public void FramePixelRectIsCenteredWithinOnePixel()
    {
        AssertFrame(1280f, 720f, 1120f, 624f, 80f, 48f);
        AssertFrame(1920f, 1080f, 1680f, 936f, 120f, 72f);
        AssertFrame(3440f, 1440f, 2240f, 1248f, 600f, 96f);
        AssertFrame(2560f, 1080f, 1680f, 936f, 440f, 72f);
    }

    [Test]
    public void AnswerScaleDoesNotChangeUiFontSize()
    {
        Assert.AreEqual(24f, SettingsWoodPanelSpec.UiFontSize);
        Assert.AreEqual(SettingsWoodPanelSpec.UiFontSize, SettingsWoodPanelSpec.UiFontSizeForAnswerScale(100));
        Assert.AreEqual(SettingsWoodPanelSpec.UiFontSize, SettingsWoodPanelSpec.UiFontSizeForAnswerScale(120));
        Assert.AreEqual(SettingsWoodPanelSpec.UiFontSize, SettingsWoodPanelSpec.UiFontSizeForAnswerScale(140));
        Assert.AreEqual(27f, SettingsWoodPanelSpec.AnswerFontSizeForScale(100), 0.01f);
        Assert.AreEqual(32.4f, SettingsWoodPanelSpec.AnswerFontSizeForScale(120), 0.01f);
        Assert.AreEqual(37.8f, SettingsWoodPanelSpec.AnswerFontSizeForScale(140), 0.01f);
    }

    [Test]
    public void DropdownCaptionInnerWidthFitsResolutionLabel()
    {
        float inner = SettingsWoodPanelSpec.ControlWidth
            - SettingsWoodPanelSpec.DropdownCaptionPaddingLeft
            - SettingsWoodPanelSpec.DropdownCaptionPaddingRight;
        Assert.AreEqual(248f, inner, 0.01f);
        Assert.That(inner, Is.GreaterThanOrEqualTo(248f));
        Assert.AreEqual(
            SettingsWoodPanelSpec.DropdownListPaddingY * 2f
            + SettingsWoodPanelSpec.DropdownItemHeight * SettingsWoodPanelSpec.DropdownListMaxItems,
            SettingsWoodPanelSpec.DropdownListMaxHeight);
    }

    [TestCase(99, 100)]
    [TestCase(100, 100)]
    [TestCase(110, 120)]
    [TestCase(130, 140)]
    [TestCase(200, 140)]
    public void NormalizeScalePercentSnapsToAllowedSteps(int input, int expected)
    {
        Assert.AreEqual(expected, SettingsWoodPanelSpec.NormalizeScalePercent(input));
    }

    [Test]
    public void ContentColumnsLeaveFixedGapInsteadOfStretching()
    {
        float occupied = SettingsWoodPanelSpec.SidebarWidth
            + SettingsWoodPanelSpec.ColumnGap
            + SettingsWoodPanelSpec.ContentWidth;
        float innerWidth = SettingsWoodPanelSpec.FrameWidth - SettingsWoodPanelSpec.FramePadding * 2f;
        Assert.AreEqual(innerWidth, occupied, 0.01f);
        float rowOccupied = SettingsWoodPanelSpec.LabelColumnWidth
            + SettingsWoodPanelSpec.ColumnGap
            + SettingsWoodPanelSpec.ControlWidth;
        Assert.AreEqual(SettingsWoodPanelSpec.ContentWidth, rowOccupied, 0.01f);
    }

    static void AssertFrame(
        float viewportWidth,
        float viewportHeight,
        float expectedWidth,
        float expectedHeight,
        float expectedX,
        float expectedY)
    {
        Vector2 size = SettingsWoodPanelSpec.FramePixelSize(viewportWidth, viewportHeight);
        Vector2 topLeft = SettingsWoodPanelSpec.FrameTopLeftPx(viewportWidth, viewportHeight);
        Assert.AreEqual(expectedWidth, size.x, 1f);
        Assert.AreEqual(expectedHeight, size.y, 1f);
        Assert.AreEqual(expectedX, topLeft.x, 1f);
        Assert.AreEqual(expectedY, topLeft.y, 1f);
    }
}
