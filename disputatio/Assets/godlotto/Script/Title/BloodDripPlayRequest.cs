using System;
using UnityEngine;

/// <summary>
/// Spawn parameters for a single <see cref="BloodDrip"/> animation.
/// Positions are in the parent container's local space (typically BloodDripContainer under a Canvas).
/// </summary>
public struct BloodDripPlayRequest
{
    public const float DefaultIntensityScale = 1f;

    /// <summary>Glyph lower anchor in container local space.</summary>
    public Vector2 AnchorLocalPosition;

    /// <summary>Floor line Y in the same local space as the anchor.</summary>
    public float FloorLocalY;

    public BloodDripStyle Style;

    public Color MainColor;
    public Color BrightColor;
    public Color DarkColor;

    /// <summary>Scales streak length, drop size, and pool contribution (e.g. from dripIntensity).</summary>
    public float IntensityScale;

    /// <summary>Optional horizontal offset from anchor X. Zero picks a random jitter in the default range.</summary>
    public float HorizontalOffset;

    /// <summary>Attached streak length in pixels. Zero picks a random length in the prototype range.</summary>
    public float StreakLength;

    /// <summary>When set, drives random ranges for deterministic replay (e.g. payload seed).</summary>
    public System.Random RandomSource;

    /// <summary>SimpleDrop diameter in pixels. Zero picks a random size in the default range.</summary>
    public float DropSize;

    /// <summary>AttachedStreak growth seconds. Zero uses <see cref="BloodDripDefaults.GrowDuration"/>.</summary>
    public float GrowDurationSeconds;

    /// <summary>Detach / free-fall seconds. Zero uses style-specific defaults.</summary>
    public float FallDurationSeconds;

    public Action<BloodDripImpactInfo> ImpactCallback;

    public static BloodDripPlayRequest CreateAttachedStreak(
        Vector2 anchorLocalPosition,
        float floorLocalY,
        Color main,
        Color bright,
        Color dark,
        float intensityScale = DefaultIntensityScale)
    {
        return new BloodDripPlayRequest
        {
            AnchorLocalPosition = anchorLocalPosition,
            FloorLocalY = floorLocalY,
            Style = BloodDripStyle.AttachedStreak,
            MainColor = main,
            BrightColor = bright,
            DarkColor = dark,
            IntensityScale = Mathf.Max(0.01f, intensityScale),
            HorizontalOffset = 0f,
            StreakLength = 0f,
            RandomSource = null,
            ImpactCallback = null,
        };
    }

    public static BloodDripPlayRequest CreateSimpleDrop(
        Vector2 anchorLocalPosition,
        float floorLocalY,
        Color main,
        Color bright,
        Color dark,
        float intensityScale = DefaultIntensityScale)
    {
        var request = CreateAttachedStreak(
            anchorLocalPosition,
            floorLocalY,
            main,
            bright,
            dark,
            intensityScale);
        request.Style = BloodDripStyle.SimpleDrop;
        return request;
    }
}

public enum BloodDripStyle
{
    /// <summary>oozeFrom — streak grows from glyph, tip detaches and falls.</summary>
    AttachedStreak,

    /// <summary>dripFrom — standalone droplet scales in, waits, then falls.</summary>
    SimpleDrop,
}

/// <summary>Payload for impact / pool integration (BloodPool, BloodDripTitleRenderer).</summary>
public struct BloodDripImpactInfo
{
    public Vector2 LocalPosition;
    public float PoolContribution;
    public float DropSize;
    public BloodDripStyle Style;
}
