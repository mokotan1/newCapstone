#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Developer
{
    public enum DeveloperQaCapabilityKind
    {
        Preset,
        Interaction,
        Probe,
        Assertion,
        Recovery
    }
}
#endif
