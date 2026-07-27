#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaSnapshot
    {
        public string CapturedAtUtc { get; }
        public string ActiveSceneId { get; }
        public string QaProfileId { get; }
        public string CapabilityRegistryVersion { get; }
        public IReadOnlyDictionary<string, string> State { get; }

        public DeveloperQaSnapshot(
            string capturedAtUtc,
            string activeSceneId,
            string qaProfileId,
            string capabilityRegistryVersion,
            IReadOnlyDictionary<string, string> state)
        {
            CapturedAtUtc = capturedAtUtc;
            ActiveSceneId = activeSceneId ?? string.Empty;
            QaProfileId = qaProfileId ?? string.Empty;
            CapabilityRegistryVersion = capabilityRegistryVersion ?? "0";
            State = state ?? new Dictionary<string, string>();
        }
    }
}
#endif
