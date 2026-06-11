/// <summary>
/// HTML prototype timing constants exposed for EditMode tests and tuning references.
/// </summary>
public static class BloodDripTiming
{
    public const float OozeGrowSeconds = BloodDripDefaults.GrowDuration;
    public const float OozeDetachDelaySeconds = BloodDripDefaults.HoldBeforeDetach;
    public const float OozeFallSeconds = BloodDripDefaults.DetachFallDuration;
    public const float DropScaleUpSeconds = BloodDripDefaults.SimpleDropScaleInDuration;
    public const int SplashCount = BloodDripDefaults.SplashParticleCount;
    public const float OozePoolGrowAmount = BloodDripDefaults.AttachedPoolContribution;
}
