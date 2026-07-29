#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaCapability
    {
        public string Id { get; }
        public string SceneId { get; }
        public DeveloperQaCapabilityKind Kind { get; }
        public string InputSchema { get; }
        public string OutputSchema { get; }

        public DeveloperQaCapability(
            string id,
            string sceneId,
            DeveloperQaCapabilityKind kind,
            string inputSchema,
            string outputSchema)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            SceneId = sceneId ?? string.Empty;
            Kind = kind;
            InputSchema = inputSchema ?? "{}";
            OutputSchema = outputSchema ?? "{}";
        }
    }
}
#endif
