using NUnit.Framework;
using UnityEngine;

public class BloodDripDefaultsTests
{
    [Test]
    public void ComputeImpactPosition_MatchesFloorOffsetFromPrototype()
    {
        var anchor = new Vector2(120f, 200f);
        const float floorY = 40f;

        Vector2 impact = BloodDripDefaults.ComputeImpactPosition(anchor, floorY);

        Assert.AreEqual(120f, impact.x, 0.001f);
        Assert.AreEqual(floorY - BloodDripDefaults.FloorImpactOffset, impact.y, 0.001f);
    }

    [Test]
    public void ComputeAttachedPoolContribution_ScalesWithIntensity()
    {
        float baseAmount = BloodDripDefaults.ComputeAttachedPoolContribution(1f);
        float halfAmount = BloodDripDefaults.ComputeAttachedPoolContribution(0.5f);

        Assert.AreEqual(BloodDripDefaults.AttachedPoolContribution, baseAmount, 0.001f);
        Assert.AreEqual(BloodDripDefaults.AttachedPoolContribution * 0.5f, halfAmount, 0.001f);
    }

    [Test]
    public void ResolveStreakLength_UsesExplicitValueWhenProvided()
    {
        const float explicitLength = 52f;
        float resolved = BloodDripDefaults.ResolveStreakLength(explicitLength, null);

        Assert.AreEqual(explicitLength, resolved, 0.001f);
    }

    [Test]
    public void SampleRange_WithSeededRandom_IsDeterministic()
    {
        var random = new System.Random(1138);

        float first = BloodDripDefaults.SampleRange(random, 10f, 20f);
        random = new System.Random(1138);
        float second = BloodDripDefaults.SampleRange(random, 10f, 20f);

        Assert.AreEqual(first, second, 0.0001f);
        Assert.GreaterOrEqual(first, 10f);
        Assert.LessOrEqual(first, 20f);
    }

    [Test]
    public void CreateAttachedStreakRequest_SetsExpectedDefaults()
    {
        var request = BloodDripPlayRequest.CreateAttachedStreak(
            new Vector2(10f, 80f),
            12f,
            Color.red,
            Color.white,
            Color.black,
            0.75f);

        Assert.AreEqual(BloodDripStyle.AttachedStreak, request.Style);
        Assert.AreEqual(0.75f, request.IntensityScale, 0.0001f);
        Assert.AreEqual(new Vector2(10f, 80f), request.AnchorLocalPosition);
        Assert.AreEqual(12f, request.FloorLocalY, 0.0001f);
    }
}
