#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Core;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.Developer
{
    public sealed class DeveloperQaService : IDeveloperQaService
    {
        private static readonly HashSet<string> KnownFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "capability", "preset", "scene", "interaction", "state", "evidence", "scenario"
        };

        private const string ProfileUnavailableMessage = "QA profile service unavailable";
        private const string EvidenceUnavailableMessage = "QA evidence recorder unavailable";
        private const string RealInputUnavailableMessage =
            "RealInput driver unavailable (EventSystem/resolver missing).";
        private const string RealInputModeKey = "mode";
        private const string RealInputModeValue = "realInput";

        private readonly DeveloperQaCapabilityRegistry _registry;
        private readonly IQaProfileService _profileService;
        private readonly IQaEvidenceRecorder _evidenceRecorder;
        private readonly IQaInputDriver _realInputDriver;
        private readonly DeveloperQaScenarioRunner _scenarioRunner;

        public DeveloperQaService()
            : this(new DeveloperQaCapabilityRegistry(), null, null, null)
        {
        }

        public DeveloperQaService(DeveloperQaCapabilityRegistry registry)
            : this(registry, null, null, null)
        {
        }

        public DeveloperQaService(
            DeveloperQaCapabilityRegistry registry,
            IQaProfileService profileService)
            : this(registry, profileService, null, null)
        {
        }

        public DeveloperQaService(
            DeveloperQaCapabilityRegistry registry,
            IQaProfileService profileService,
            IQaEvidenceRecorder evidenceRecorder)
            : this(registry, profileService, evidenceRecorder, null)
        {
        }

        public DeveloperQaService(
            DeveloperQaCapabilityRegistry registry,
            IQaProfileService profileService,
            IQaEvidenceRecorder evidenceRecorder,
            IQaInputDriver realInputDriver)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _profileService = profileService;
            _evidenceRecorder = evidenceRecorder;
            _realInputDriver = realInputDriver;
            _scenarioRunner = new DeveloperQaScenarioRunner(ExecuteStepCommand);
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

            if (command.Family == "interaction" && command.Name == "pointer")
            {
                return Task.FromResult(ExecutePointer(command));
            }

            if (IsCapabilityDispatchCommand(command.Family, command.Name))
            {
                return Task.FromResult(DispatchCapability(command));
            }

            if (command.Family == "evidence" && command.Name == "capture")
            {
                return Task.FromResult(CaptureEvidence(command));
            }

            if (command.Family == "scenario" && command.Name == "run")
            {
                return Task.FromResult(BeginScenario(command));
            }

            if (command.Family == "scenario" && command.Name == "resume")
            {
                return Task.FromResult(_scenarioRunner.Resume());
            }

            if (command.Family == "scenario" && command.Name == "status")
            {
                return Task.FromResult(_scenarioRunner.Status());
            }

            if (command.Family == "scenario" &&
                (command.Name == "cancel" || command.Name == "abort"))
            {
                return Task.FromResult(CancelScenario());
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

        private DeveloperQaResult CaptureEvidence(DeveloperQaCommand command)
        {
            if (_evidenceRecorder == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    EvidenceUnavailableMessage);
            }

            QaRunId runId = ResolveRunId(command);
            DeveloperQaResult beginResult = EnsureEvidenceRunBegun(runId, command.Id);
            if (beginResult.Code != DeveloperQaResultCode.Ok)
            {
                return beginResult;
            }

            QaEvidenceOperationResult appendResult = _evidenceRecorder.AppendEvent(
                QaEvidenceEvent.Create(
                    QaEvidenceEventType.Note,
                    commandId: command.Id,
                    code: "EvidenceCapture",
                    message: "DeveloperQa evidence.capture checkpoint."));

            if (!appendResult.IsSuccess)
            {
                return MapEvidenceFailure(appendResult);
            }

            string runDirectory = ResolveActiveRunDirectory();
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Evidence capture recorded.",
                data: DeveloperQaMaps.From(new Dictionary<string, string>
                {
                    ["run_id"] = runId.ToString(),
                    ["command_id"] = command.Id,
                    ["run_directory"] = runDirectory ?? string.Empty
                }));
        }

        private DeveloperQaResult BeginScenario(DeveloperQaCommand command)
        {
            DeveloperQaResult sessionBegin = BeginScenarioProfileSession(command);
            if (sessionBegin.Code != DeveloperQaResultCode.Ok)
            {
                return sessionBegin;
            }

            bool hasScenario =
                (command.Parameters != null
                 && ((command.Parameters.ContainsKey("scenario_id")
                      && !string.IsNullOrWhiteSpace(command.Parameters["scenario_id"]))
                     || (command.Parameters.ContainsKey("scenario_path")
                         && !string.IsNullOrWhiteSpace(command.Parameters["scenario_path"]))))
                || !string.IsNullOrWhiteSpace(command.TargetId);

            if (!hasScenario)
            {
                // Backward compatible: scenario.run without JSON still opens the QA profile.
                return sessionBegin;
            }

            bool executeSteps = true;
            if (command.Parameters != null
                && command.Parameters.TryGetValue("execute", out string executeText)
                && !string.IsNullOrWhiteSpace(executeText)
                && bool.TryParse(executeText, out bool parsed))
            {
                executeSteps = parsed;
            }

            DeveloperQaResult runnerResult = _scenarioRunner.Begin(command, executeSteps);
            if (runnerResult.Code == DeveloperQaResultCode.InvalidCommand
                || runnerResult.Code == DeveloperQaResultCode.InternalError
                || runnerResult.Code == DeveloperQaResultCode.UnsupportedCommand)
            {
                // Profile was opened for the run; roll it back when JSON/load is invalid.
                RestoreScenarioProfileSession();
                return runnerResult;
            }

            // Merge profile/evidence keys with runner status keys.
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (sessionBegin.Data != null)
            {
                foreach (KeyValuePair<string, string> pair in sessionBegin.Data)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            if (runnerResult.Data != null)
            {
                foreach (KeyValuePair<string, string> pair in runnerResult.Data)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            if (runnerResult.Code == DeveloperQaResultCode.Ok
                && executeSteps
                && runnerResult.Data != null
                && runnerResult.Data.TryGetValue("state", out string state)
                && state == DeveloperQaScenarioStates.Completed)
            {
                DeveloperQaResult restore = RestoreScenarioProfileSession();
                if (restore.Code != DeveloperQaResultCode.Ok)
                {
                    return restore;
                }
            }

            return new DeveloperQaResult(
                runnerResult.Code,
                string.IsNullOrEmpty(runnerResult.Message)
                    ? sessionBegin.Message
                    : runnerResult.Message,
                missingCapabilityId: runnerResult.MissingCapabilityId,
                checkpointId: runnerResult.CheckpointId,
                data: DeveloperQaMaps.From(merged));
        }

        private DeveloperQaResult CancelScenario()
        {
            DeveloperQaResult cancelResult = _scenarioRunner.Cancel();
            DeveloperQaResult restoreResult = RestoreScenarioProfileSession();
            if (restoreResult.Code != DeveloperQaResultCode.Ok
                && restoreResult.Code != DeveloperQaResultCode.EnvironmentBlocked)
            {
                return restoreResult;
            }

            // Prefer profile restore failure when the profile service is missing
            // (keeps Task 3 contract), otherwise return cancel status data.
            if (restoreResult.Code == DeveloperQaResultCode.EnvironmentBlocked
                && _profileService == null)
            {
                return restoreResult;
            }

            if (restoreResult.Code != DeveloperQaResultCode.Ok)
            {
                return restoreResult;
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                cancelResult.Message,
                checkpointId: cancelResult.CheckpointId,
                data: cancelResult.Data);
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

            var data = new Dictionary<string, string>
            {
                ["run_id"] = runId.ToString(),
                ["command_id"] = command.Id
            };

            if (_evidenceRecorder != null)
            {
                DeveloperQaResult evidenceBegin = EnsureEvidenceRunBegun(runId, command.Id);
                if (evidenceBegin.Code != DeveloperQaResultCode.Ok)
                {
                    return evidenceBegin;
                }

                string runDirectory = ResolveActiveRunDirectory();
                if (!string.IsNullOrEmpty(runDirectory))
                {
                    data["run_directory"] = runDirectory;
                }
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "QA profile session begun.",
                data: DeveloperQaMaps.From(data));
        }

        /// <summary>
        /// Executes one scenario step without re-entering <c>scenario.*</c> commands
        /// (avoids recursive run/resume/cancel from JSON steps).
        /// </summary>
        private DeveloperQaResult ExecuteStepCommand(DeveloperQaCommand command)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.Id))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Command id is required.");
            }

            if (string.IsNullOrWhiteSpace(command.Family) ||
                !KnownFamilies.Contains(command.Family))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.UnsupportedCommand,
                    $"Unknown family '{command.Family}'.");
            }

            if (command.Family == "scenario")
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Nested scenario.* steps are not allowed.");
            }

            if (command.Family == "capability" && command.Name == "list")
            {
                int count = _registry.List().Count;
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    count == 0 ? "empty" : $"count={count}",
                    data: DeveloperQaMaps.From(new Dictionary<string, string>
                    {
                        ["count"] = count.ToString(),
                        ["current_capabilities"] = _registry.FormatCurrentCapabilityIds()
                    }));
            }

            if (command.Family == "capability" && command.Name == "describe")
            {
                return DescribeCapability(command.TargetId);
            }

            if (command.Family == "interaction" && command.Name == "pointer")
            {
                return ExecutePointer(command);
            }

            if (IsCapabilityDispatchCommand(command.Family, command.Name))
            {
                return DispatchCapability(command);
            }

            if (command.Family == "evidence" && command.Name == "capture")
            {
                return CaptureEvidence(command);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.UnsupportedCommand,
                $"{command.Family}.{command.Name} not implemented yet.");
        }

        /// <summary>
        /// Player-visible pointer click via injected RealInput driver (design §6.2).
        /// Never reports fake Ok when the driver/EventSystem/resolver is missing.
        /// </summary>
        private DeveloperQaResult ExecutePointer(DeveloperQaCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.TargetId))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "interaction.pointer requires targetId.");
            }

            string mode = RealInputModeValue;
            if (command.Parameters != null
                && command.Parameters.TryGetValue(RealInputModeKey, out string modeText)
                && !string.IsNullOrWhiteSpace(modeText))
            {
                mode = modeText.Trim();
            }

            if (!string.Equals(mode, RealInputModeValue, StringComparison.OrdinalIgnoreCase))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.UnsupportedCommand,
                    "interaction.pointer mode '" + mode + "' is not supported (expected realInput).");
            }

            if (_realInputDriver == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    RealInputUnavailableMessage);
            }

            if (!QaTargetId.TryCreate(command.TargetId, out QaTargetId targetId, out string targetError))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Invalid targetId for interaction.pointer: " + targetError);
            }

            QaInputResult inputResult;
            try
            {
                inputResult = _realInputDriver
                    .ClickAsync(targetId, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception ex)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    "RealInput click failed: " + ex.GetType().Name + ".");
            }

            return MapInputResult(inputResult);
        }

        private static DeveloperQaResult MapInputResult(QaInputResult inputResult)
        {
            if (inputResult == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    "RealInput driver returned null.");
            }

            var data = new Dictionary<string, string>
            {
                ["input_mode"] = inputResult.Mode.ToString(),
                ["input_code"] = inputResult.Code.ToString(),
                ["target_id"] = inputResult.TargetId.Value
            };

            if (inputResult.IsSuccess)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    string.IsNullOrEmpty(inputResult.Message)
                        ? "RealInput pointer click succeeded."
                        : inputResult.Message,
                    data: DeveloperQaMaps.From(data));
            }

            switch (inputResult.Code)
            {
                case QaInputResultCode.Cancelled:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.Cancelled,
                        inputResult.Message,
                        data: DeveloperQaMaps.From(data));
                case QaInputResultCode.InvalidArgument:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.InvalidCommand,
                        inputResult.Message,
                        data: DeveloperQaMaps.From(data));
                case QaInputResultCode.UnknownTarget:
                case QaInputResultCode.InputLayerFailure:
                case QaInputResultCode.ApiInteractionFailed:
                case QaInputResultCode.UnsupportedInteraction:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.EnvironmentBlocked,
                        string.IsNullOrEmpty(inputResult.Message)
                            ? "RealInput pointer blocked."
                            : inputResult.Message,
                        data: DeveloperQaMaps.From(data));
                default:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.InternalError,
                        string.IsNullOrEmpty(inputResult.Message)
                            ? "RealInput pointer failed."
                            : inputResult.Message,
                        data: DeveloperQaMaps.From(data));
            }
        }

        private DeveloperQaResult EnsureEvidenceRunBegun(QaRunId runId, string commandId)
        {
            string runIdText = runId.IsNone ? QaRunId.NewId().ToString() : runId.ToString();
            QaEvidenceOperationResult begin = _evidenceRecorder.BeginRun(runIdText);
            if (begin.IsSuccess)
            {
                return new DeveloperQaResult(DeveloperQaResultCode.Ok, begin.Message);
            }

            // A prior evidence.capture / scenario.run in the same service session is fine —
            // keep appending into the already-active directory.
            if (begin.Code == QaEvidenceOperationCode.AlreadyActive)
            {
                return new DeveloperQaResult(DeveloperQaResultCode.Ok, begin.Message);
            }

            return MapEvidenceFailure(begin, commandId);
        }

        private static DeveloperQaResult MapEvidenceFailure(
            QaEvidenceOperationResult operation,
            string commandId = null)
        {
            if (operation == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    "Evidence recorder returned null.");
            }

            switch (operation.Code)
            {
                case QaEvidenceOperationCode.InvalidRequest:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.InvalidCommand,
                        string.IsNullOrWhiteSpace(operation.Message)
                            ? "Invalid evidence request."
                            : operation.Message);
                case QaEvidenceOperationCode.NotActive:
                case QaEvidenceOperationCode.AlreadyFinalized:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.EnvironmentBlocked,
                        string.IsNullOrWhiteSpace(operation.Message)
                            ? "Evidence run is not writable."
                            : operation.Message);
                default:
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.InternalError,
                        string.IsNullOrWhiteSpace(operation.Message)
                            ? "Evidence recorder failed" +
                              (string.IsNullOrEmpty(commandId) ? "." : " for '" + commandId + "'.")
                            : operation.Message);
            }
        }

        private string ResolveActiveRunDirectory()
        {
            var development = _evidenceRecorder as DevelopmentQaEvidenceRecorder;
            return development != null ? development.RunDirectoryPath : null;
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
