#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Scene adapters in Assembly-CSharp register these handlers so
    /// <see cref="DeveloperQaService"/> can dispatch without referencing gameplay types.
    /// </summary>
    public delegate DeveloperQaResult DeveloperQaCapabilityHandler(DeveloperQaCommand command);
}
#endif
