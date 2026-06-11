using UnityEngine;

/// <summary>
/// Timing and sizing defaults ported from <c>oozeFrom</c>, <c>dripFrom</c>, and <c>impact</c>
/// in docs/blood-drip-title-final.html (not browser-specific APIs).
/// </summary>
public static class BloodDripDefaults
{
    public const float StreakWidth = 4f;
    public const float StreakLengthMin = 34f;
    public const float StreakLengthMax = 80f;
    public const float HorizontalJitter = 6f;

    public const float GrowDuration = 1.9f;
    public const float HoldBeforeDetach = 2f;
    public const float DetachFallDuration = 0.45f;
    public const float StreakFadeDuration = 2.6f;
    public const float PostDetachCleanupDelay = 2.7f;

    public const float AttachedPoolContribution = 8f;
    public const float TipWidth = 8f;
    public const float TipHeight = 10f;
    public const float FloorImpactOffset = 4f;

    public const float SimpleDropScaleInDuration = 0.4f;
    public const float SimpleDropWaitMin = 0.6f;
    public const float SimpleDropWaitMax = 1.5f;
    public const float SimpleDropFallMin = 0.42f;
    public const float SimpleDropFallMax = 0.6f;
    public const float SimpleDropSizeMin = 7f;
    public const float SimpleDropSizeMax = 11f;
    public const float SimpleDropFallMargin = 6f;

    public const int SplashParticleCount = 3;
    public const float SplashDuration = 0.35f;
    public const float SplashSizeMin = 2f;
    public const float SplashSizeMax = 4f;
    public const float SplashHorizontalSpread = 14f;
    public const float SplashVerticalMin = -10f;
    public const float SplashVerticalMax = -2f;

    /// <summary>Approximates CSS cubic-bezier(.55,.06,.68,.19) for streak growth.</summary>
    public static readonly AnimationCurve GrowEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.2f),
        new Keyframe(0.35f, 0.12f, 0.4f, 0.9f),
        new Keyframe(1f, 1f, 1.6f, 0f));

    /// <summary>Approximates CSS cubic-bezier(.55,0,.85,.3) for gravity fall.</summary>
    public static readonly AnimationCurve FallEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.1f),
        new Keyframe(0.25f, 0.08f, 0.6f, 1.4f),
        new Keyframe(1f, 1f, 2.2f, 0f));

    /// <summary>Approximates CSS cubic-bezier(.34,1.56,.64,1) for droplet pop-in.</summary>
    public static readonly AnimationCurve PopInEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.55f, 1.12f, 2f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));

    public static float EvaluateIntensityScale(float intensityScale)
    {
        if (float.IsNaN(intensityScale) || float.IsInfinity(intensityScale))
            return BloodDripPlayRequest.DefaultIntensityScale;

        return Mathf.Max(0.01f, intensityScale);
    }

    public static float ResolveStreakLength(float requestedLength, System.Random random)
    {
        if (requestedLength > 0f)
            return requestedLength;

        return SampleRange(random, StreakLengthMin, StreakLengthMax);
    }

    public static float ResolveHorizontalOffset(float requestedOffset, System.Random random)
    {
        if (Mathf.Abs(requestedOffset) > 0.001f)
            return requestedOffset;

        return SampleRange(random, -HorizontalJitter, HorizontalJitter);
    }

    public static float ComputeAttachedPoolContribution(float intensityScale)
    {
        return AttachedPoolContribution * EvaluateIntensityScale(intensityScale);
    }

    public static float ComputeSimpleDropSize(System.Random random, float intensityScale)
    {
        return SampleRange(random, SimpleDropSizeMin, SimpleDropSizeMax)
            * EvaluateIntensityScale(intensityScale);
    }

    public static Vector2 ComputeImpactPosition(Vector2 anchorLocalPosition, float floorLocalY)
    {
        return new Vector2(anchorLocalPosition.x, floorLocalY - FloorImpactOffset);
    }

    public static float SampleRange(System.Random random, float min, float max)
    {
        if (random != null)
            return (float)(min + random.NextDouble() * (max - min));

        return Random.Range(min, max);
    }
}
