#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Developer
{
    public enum DeveloperQaResultCode
    {
        Ok,
        InvalidCommand,
        UnsupportedCommand,
        MissingCapability,
        AssertionFailed,
        Cancelled,
        InternalError,
        EnvironmentBlocked
    }
}
#endif
