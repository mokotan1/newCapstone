using NUnit.Framework;
using UnityEngine;

public class BloodDripTests
{
    [Test]
    public void Defaults_TimingMatchesHtmlPrototype()
    {
        Assert.AreEqual(1.9f, BloodDripDefaults.GrowDuration, 0.0001f);
        Assert.AreEqual(2f, BloodDripDefaults.HoldBeforeDetach, 0.0001f);
        Assert.AreEqual(0.45f, BloodDripDefaults.DetachFallDuration, 0.0001f);
        Assert.AreEqual(0.4f, BloodDripDefaults.SimpleDropScaleInDuration, 0.0001f);
        Assert.AreEqual(3, BloodDripDefaults.SplashParticleCount);
    }

    [Test]
    public void GrowEase_StartAndEnd_AreZeroAndOne()
    {
        Assert.AreEqual(0f, BloodDripDefaults.GrowEase.Evaluate(0f), 0.0001f);
        Assert.AreEqual(1f, BloodDripDefaults.GrowEase.Evaluate(1f), 0.0001f);
    }

    [Test]
    public void CreateAttachedStreakRequest_UsesAttachedStyle()
    {
        var request = BloodDripPlayRequest.CreateAttachedStreak(
            new Vector2(10f, 20f),
            floorLocalY: 100f,
            Color.red,
            Color.white,
            Color.black);

        Assert.AreEqual(BloodDripStyle.AttachedStreak, request.Style);
        Assert.AreEqual(new Vector2(10f, 20f), request.AnchorLocalPosition);
        Assert.AreEqual(100f, request.FloorLocalY, 0.0001f);
    }

    [Test]
    public void CreateSimpleDropRequest_UsesSimpleDropStyle()
    {
        var request = BloodDripPlayRequest.CreateSimpleDrop(
            new Vector2(0f, 10f),
            floorLocalY: 80f,
            Color.red,
            Color.white,
            Color.black);

        Assert.AreEqual(BloodDripStyle.SimpleDrop, request.Style);
    }

    [Test]
    public void Palette_FromTitleStyle_UsesPayloadColors()
    {
        var payload = TitleStylePayload.CreateDefault();
        var palette = BloodDripPalette.FromTitleStyle(payload);

        Assert.AreEqual(payload.Color, palette.Main);
        Assert.AreEqual(payload.DarkColor, palette.Dark);
        Assert.AreEqual(payload.BrightColor, palette.Bright);
    }
}
