#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaService : IDeveloperQaService
    {
        private static readonly HashSet<string> KnownFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "capability", "preset", "scene", "interaction", "state", "evidence", "scenario"
        };

        private readonly DeveloperQaCapabilityRegistry _registry;

        public DeveloperQaService()
            : this(new DeveloperQaCapabilityRegistry())
        {
        }

        public DeveloperQaService(DeveloperQaCapabilityRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Id))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Command id is required."));
            }

            if (string.IsNullOrWhiteSpace(command.Family) ||
                !KnownFamilies.Contains(command.Family))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.UnsupportedCommand,
                    $"Unknown family '{command.Family}'."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new DeveloperQaResult(DeveloperQaResultCode.Cancelled));
            }

            if (command.Family == "capability" && command.Name == "list")
            {
                int count = _registry.List().Count;
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    count == 0 ? "empty" : $"count={count}",
                    data: new Dictionary<string, string>
                    {
                        ["count"] = count.ToString(),
                        ["current_capabilities"] = _registry.FormatCurrentCapabilityIds()
                    }));
            }

            if (command.Family == "capability" && command.Name == "describe")
            {
                return Task.FromResult(DescribeCapability(command.TargetId));
            }

            if (command.Family == "interaction" && command.Name == "invoke")
            {
                return Task.FromResult(InvokeInteraction(command.TargetId));
            }

            return Task.FromResult(new DeveloperQaResult(
                DeveloperQaResultCode.UnsupportedCommand,
                $"{command.Family}.{command.Name} not implemented yet."));
        }

        public DeveloperQaSnapshot CaptureSnapshot()
        {
            return new DeveloperQaSnapshot(
                DateTime.UtcNow.ToString("o"),
                string.Empty,
                string.Empty,
                _registry.Version,
                new Dictionary<string, string>());
        }

        public IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()
        {
            return _registry.List();
        }

        private DeveloperQaResult DescribeCapability(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId) || !_registry.TryGet(targetId, out DeveloperQaCapability capability))
            {
                return CreateMissingCapability(targetId);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                $"Described '{capability.Id}'.",
                data: new Dictionary<string, string>
                {
                    ["id"] = capability.Id,
                    ["scene_id"] = capability.SceneId,
                    ["kind"] = capability.Kind.ToString(),
                    ["input_schema"] = capability.InputSchema,
                    ["output_schema"] = capability.OutputSchema
                });
        }

        private DeveloperQaResult InvokeInteraction(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId) || !_registry.TryGet(targetId, out _))
            {
                return CreateMissingCapability(targetId);
            }

            // Known capability without an adapter yet (Task 3+).
            return new DeveloperQaResult(
                DeveloperQaResultCode.UnsupportedCommand,
                $"interaction.invoke for '{targetId}' not implemented yet.");
        }

        private DeveloperQaResult CreateMissingCapability(string missingId)
        {
            string id = string.IsNullOrWhiteSpace(missingId) ? string.Empty : missingId;
            var data = new Dictionary<string, string>
            {
                ["current_capabilities"] = _registry.FormatCurrentCapabilityIds()
            };

            return new DeveloperQaResult(
                DeveloperQaResultCode.MissingCapability,
                string.IsNullOrEmpty(id)
                    ? "Capability target id is required."
                    : $"Missing capability '{id}'.",
                missingCapabilityId: id,
                checkpointId: Guid.NewGuid().ToString("N"),
                data: data);
        }
    }
}
#endif
