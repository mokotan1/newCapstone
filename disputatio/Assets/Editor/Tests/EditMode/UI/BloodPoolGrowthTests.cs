using NUnit.Framework;

public class BloodPoolGrowthTests
{
    const float MaxWidth = 420f;
    const float BaseHeight = 6f;
    const float HeightPerWidth = 0.035f;
    const float MaxHeight = 20f;
    const float GrowthMultiplier = 1.4f;

    [Test]
    public void ComputeNextWidth_AccumulatesAndCapsAtMax()
    {
        float width = 0f;
        width = BloodPoolGrowth.ComputeNextWidth(width, 10f, GrowthMultiplier, MaxWidth);
        Assert.AreEqual(14f, width, 0.0001f);

        width = BloodPoolGrowth.ComputeNextWidth(width, 500f, GrowthMultiplier, MaxWidth);
        Assert.AreEqual(MaxWidth, width, 0.0001f);
    }

    [Test]
    public void ComputeNextWidth_NonPositiveDripAmount_DoesNotGrow()
    {
        float width = 12f;
        float next = BloodPoolGrowth.ComputeNextWidth(width, 0f, GrowthMultiplier, MaxWidth);
        Assert.AreEqual(12f, next, 0.0001f);
    }

    [Test]
    public void ComputeHeight_ScalesWithWidthAndCapsAtMax()
    {
        float height = BloodPoolGrowth.ComputeHeight(100f, BaseHeight, HeightPerWidth, MaxHeight);
        Assert.AreEqual(BaseHeight + 100f * HeightPerWidth, height, 0.0001f);

        float capped = BloodPoolGrowth.ComputeHeight(MaxWidth, BaseHeight, HeightPerWidth, MaxHeight);
        Assert.AreEqual(MaxHeight, capped, 0.0001f);
    }

    [Test]
    public void ComputeHeight_MatchesHtmlPrototypeAtSampleWidth()
    {
        // growPool(pool, 8) twice from zero → width 22.4, height ≈ 6.784
        float width = BloodPoolGrowth.ComputeNextWidth(0f, 8f, GrowthMultiplier, MaxWidth);
        width = BloodPoolGrowth.ComputeNextWidth(width, 8f, GrowthMultiplier, MaxWidth);
        float height = BloodPoolGrowth.ComputeHeight(width, BaseHeight, HeightPerWidth, MaxHeight);

        Assert.AreEqual(22.4f, width, 0.0001f);
        Assert.AreEqual(6.784f, height, 0.001f);
    }
}
