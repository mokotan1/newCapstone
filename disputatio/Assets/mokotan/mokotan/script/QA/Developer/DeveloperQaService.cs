#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Profile;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaService : IDeveloperQaService
    {
        private static readonly HashSet<string> KnownFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "capability", "preset", "scene", "interaction", "state", "evidence", "scenario"
        };

        private const string ProfileUnavailableMessage = "QA profile service unavailable";

        private readonly DeveloperQaCapabilityRegistry _registry;
        private readonly IQaProfileService _profileService;

        public DeveloperQaService()
            : this(new DeveloperQaCapabilityRegistry(), null)
        {
        }

        public DeveloperQaService(DeveloperQaCapabilityRegistry registry)
            : this(registry, null)
        {
        }

        public DeveloperQaService(
            DeveloperQaCapabilityRegistry registry,
            IQaProfileService profileService)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _profileService = profileService;
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
                    data: DeveloperQaMaps.From(new Dictionary<string, string>
                    {
                        ["count"] = count.ToString(),
                        ["current_capabilities"] = _registry.FormatCurrentCapabilityIds()
                    })));
            }

            if (command.Family == "capability" && command.Name == "describe")
            {
                return Task.FromResult(DescribeCapability(command.TargetId));
            }

            if (IsCapabilityDispatchCommand(command.Family, command.Name))
            {
                return Task.FromResult(DispatchCapability(command));
            }

            if (command.Family == "scenario" && command.Name == "run")
            {
                return Task.FromResult(BeginScenarioProfileSession(command));
            }

            if (command.Family == "scenario" &&
                (command.Name == "cancel" || command.Name == "abort"))
            {
                return Task.FromResult(RestoreScenarioProfileSession());
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
                DeveloperQaMaps.Empty);
        }

        public IReadOnlyCollection<DeveloperQaCapability> ListCapabilities()
        {
            return _registry.List();
        }

        private static bool IsCapabilityDispatchCommand(string family, string name)
        {
            if (family == "interaction" && name == "invoke")
            {
                return true;
            }

            if (family == "preset" && name == "apply")
            {
                return true;
            }

            if (family == "state" && (name == "assert" || name == "capture"))
            {
                return true;
            }

            if (family == "evidence" && name == "capture")
            {
                return true;
            }

            return false;
        }

        private DeveloperQaResult DispatchCapability(DeveloperQaCommand command)
        {
            string targetId = command.TargetId;
            if (string.IsNullOrWhiteSpace(targetId) || !_registry.TryGet(targetId, out _))
            {
                return CreateMissingCapability(targetId);
            }

            if (!_registry.TryGetHandler(targetId, out DeveloperQaCapabilityHandler handler) ||
                handler == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.UnsupportedCommand,
                    $"{command.Family}.{command.Name} for '{targetId}' has no handler yet.");
            }

            try
            {
                DeveloperQaResult result = handler(command);
                return result ?? new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    $"Handler for '{targetId}' returned null.");
            }
            catch (Exception ex)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    $"Handler for '{targetId}' failed: {ex.GetType().Name}.");
            }
        }

        private DeveloperQaResult BeginScenarioProfileSession(DeveloperQaCommand command)
        {
            if (_profileService == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    ProfileUnavailableMessage);
            }

            QaRunId runId = ResolveRunId(command);
            QaProfileOperationResult profileResult = _profileService.BeginQaProfile(runId);
            if (!profileResult.IsSuccess)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    string.IsNullOrWhiteSpace(profileResult.Message)
                        ? "Failed to begin QA profile."
                        : profileResult.Message);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "QA profile session begun.",
                data: DeveloperQaMaps.From(new Dictionary<string, string>
                {
                    ["run_id"] = runId.ToString(),
                    ["command_id"] = command.Id
                }));
        }

        private DeveloperQaResult RestoreScenarioProfileSession()
        {
            if (_profileService == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    ProfileUnavailableMessage);
            }

            QaProfileOperationResult profileResult = _profileService.RestorePreviousProfile();
            if (!profileResult.IsSuccess)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    string.IsNullOrWhiteSpace(profileResult.Message)
                        ? "Failed to restore previous QA profile."
                        : profileResult.Message);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "QA profile session restored.");
        }

        /// <summary>
        /// Prefer Parameters["run_id"], then command.Id when Guid-parseable; else <see cref="QaRunId.NewId"/>.
        /// </summary>
        private static QaRunId ResolveRunId(DeveloperQaCommand command)
        {
            if (command.Parameters != null &&
                command.Parameters.TryGetValue("run_id", out string fromParam) &&
                QaRunId.TryParse(fromParam, out QaRunId parsedFromParam) &&
                !parsedFromParam.IsNone)
            {
                return parsedFromParam;
            }

            if (QaRunId.TryParse(command.Id, out QaRunId parsedFromId) && !parsedFromId.IsNone)
            {
                return parsedFromId;
            }

            return QaRunId.NewId();
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
                data: DeveloperQaMaps.From(new Dictionary<string, string>
                {
                    ["id"] = capability.Id,
                    ["scene_id"] = capability.SceneId,
                    ["kind"] = capability.Kind.ToString(),
                    ["input_schema"] = capability.InputSchema,
                    ["output_schema"] = capability.OutputSchema
                }));
        }

        private DeveloperQaResult CreateMissingCapability(string missingId)
        {
            string id = string.IsNullOrWhiteSpace(missingId) ? string.Empty : missingId;
            IReadOnlyDictionary<string, string> data = DeveloperQaMaps.From(
                new Dictionary<string, string>
                {
                    ["current_capabilities"] = _registry.FormatCurrentCapabilityIds()
                });

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
