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

            // Task 2+ fills real handlers; capability.list returns Ok with empty list for now.
            if (command.Family == "capability" && command.Name == "list")
            {
                return Task.FromResult(new DeveloperQaResult(DeveloperQaResultCode.Ok, "empty"));
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
                "0",
                new Dictionary<string, string>());
        }

        public IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()
        {
            return Array.Empty<DeveloperQaCapability>();
        }
    }
}
#endif
