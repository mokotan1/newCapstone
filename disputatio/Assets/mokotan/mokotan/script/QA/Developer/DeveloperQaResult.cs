#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaResult
    {
        public DeveloperQaResultCode Code { get; }
        public string Message { get; }
        public string MissingCapabilityId { get; }
        public string CheckpointId { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public DeveloperQaResult(
            DeveloperQaResultCode code,
            string message = null,
            string missingCapabilityId = null,
            string checkpointId = null,
            IReadOnlyDictionary<string, string> data = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            MissingCapabilityId = missingCapabilityId;
            CheckpointId = checkpointId;
            Data = DeveloperQaMaps.AsReadOnly(data);
        }
    }
}
#endif
