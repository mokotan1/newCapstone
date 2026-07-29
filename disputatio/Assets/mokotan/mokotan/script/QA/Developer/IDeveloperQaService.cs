#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Developer
{
    public interface IDeveloperQaService
    {
        Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken);

        DeveloperQaSnapshot CaptureSnapshot();

        IReadOnlyCollection<DeveloperQaCapability> ListCapabilities();
    }
}
#endif
