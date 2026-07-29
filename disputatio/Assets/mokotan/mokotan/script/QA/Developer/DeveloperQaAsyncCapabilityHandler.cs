#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Async capability handler. Prefer this when a step must yield Unity frames
    /// (Fungus Say/Wait) before returning Ok.
    /// </summary>
    public delegate Task<DeveloperQaResult> DeveloperQaAsyncCapabilityHandler(
        DeveloperQaCommand command,
        CancellationToken cancellationToken);
}
#endif
