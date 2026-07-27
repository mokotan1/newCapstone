#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Executes DeveloperQa-command scenario JSON with status / resume / cancel (Task 9).
    /// Full <c>QaScenarioRunner</c> integration is skipped because that schema cannot
    /// express StudyRoom capability IDs (<c>studyroom.mirror.*</c>).
    /// </summary>
    public sealed class DeveloperQaScenarioRunner
    {
        private readonly DeveloperQaScenarioValidator _validator;
        private readonly Func<DeveloperQaCommand, DeveloperQaResult> _executeStep;
        private Session _session;

        public DeveloperQaScenarioRunner(Func<DeveloperQaCommand, DeveloperQaResult> executeStep)
            : this(executeStep, new DeveloperQaScenarioValidator())
        {
        }

        public DeveloperQaScenarioRunner(
            Func<DeveloperQaCommand, DeveloperQaResult> executeStep,
            DeveloperQaScenarioValidator validator)
        {
            _executeStep = executeStep ?? throw new ArgumentNullException(nameof(executeStep));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public bool HasSession
        {
            get { return _session != null; }
        }

        public DeveloperQaResult Begin(
            DeveloperQaCommand command,
            bool executeSteps)
        {
            if (_session != null
                && (_session.State == DeveloperQaScenarioStates.Running))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "A DeveloperQa scenario is already running. Cancel or wait for completion.");
            }

            string json;
            DeveloperQaResult loadResult = TryLoadScenarioJson(command, out json);
            if (loadResult.Code != DeveloperQaResultCode.Ok)
            {
                return loadResult;
            }

            DeveloperQaScenarioValidationResult validation = _validator.Validate(json);
            if (!validation.IsValid)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Invalid scenario: " + string.Join("; ", validation.Errors));
            }

            DeveloperQaScenarioDefinition scenario = validation.Scenario;
            _session = new Session(scenario)
            {
                State = DeveloperQaScenarioStates.Running,
                StepIndex = 0
            };

            if (!executeSteps)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Scenario session started (deferred execution).",
                    checkpointId: _session.CheckpointId,
                    data: BuildStatusData());
            }

            return ExecuteRemaining();
        }

        public DeveloperQaResult Resume()
        {
            if (_session == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "No scenario session to resume.");
            }

            if (_session.State == DeveloperQaScenarioStates.Cancelled)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Cancelled,
                    "Scenario was cancelled; start a new run.");
            }

            if (_session.State == DeveloperQaScenarioStates.Completed)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Scenario already completed.",
                    data: BuildStatusData());
            }

            _session.State = DeveloperQaScenarioStates.Running;
            return ExecuteRemaining();
        }

        public DeveloperQaResult Cancel()
        {
            if (_session == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "No active scenario session.",
                    data: DeveloperQaMaps.From(new Dictionary<string, string>
                    {
                        ["state"] = DeveloperQaScenarioStates.Idle
                    }));
            }

            _session.State = DeveloperQaScenarioStates.Cancelled;
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Scenario cancelled.",
                checkpointId: _session.CheckpointId,
                data: BuildStatusData());
        }

        public DeveloperQaResult Status()
        {
            if (_session == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "No scenario session.",
                    data: DeveloperQaMaps.From(new Dictionary<string, string>
                    {
                        ["state"] = DeveloperQaScenarioStates.Idle
                    }));
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Scenario status.",
                checkpointId: _session.CheckpointId,
                data: BuildStatusData());
        }

        /// <summary>
        /// Resolves scenario JSON from <c>scenario_path</c>, then <c>scenario_id</c>
        /// (file under Resources/QA/Scenarios), then <see cref="DeveloperQaCommand.TargetId"/>.
        /// </summary>
        public static DeveloperQaResult TryLoadScenarioJson(
            DeveloperQaCommand command,
            out string json)
        {
            json = null;
            if (command == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Command is required.");
            }

            string path = null;
            if (command.Parameters != null
                && command.Parameters.TryGetValue("scenario_path", out string fromPath)
                && !string.IsNullOrWhiteSpace(fromPath))
            {
                path = fromPath;
            }

            string scenarioId = null;
            if (command.Parameters != null
                && command.Parameters.TryGetValue("scenario_id", out string fromId)
                && !string.IsNullOrWhiteSpace(fromId))
            {
                scenarioId = fromId.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(command.TargetId))
            {
                scenarioId = command.TargetId.Trim();
            }

            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(scenarioId))
            {
                path = ResolveScenarioPath(scenarioId);
            }

            if (string.IsNullOrEmpty(path))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "scenario_id or scenario_path is required to run a scenario JSON.");
            }

            if (!File.Exists(path))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Scenario file not found: " + path);
            }

            try
            {
                json = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.InternalError,
                    "Failed to read scenario file: " + ex.Message);
            }

            return new DeveloperQaResult(DeveloperQaResultCode.Ok, "loaded");
        }

        public static string ResolveScenarioPath(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return null;
            }

            string fileName = scenarioId.Trim();
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName + ".json";
            }

            string dataPath = UnityEngine.Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                string underAssets = Path.Combine(
                    dataPath,
                    "Resources",
                    "QA",
                    "Scenarios",
                    fileName);
                if (File.Exists(underAssets))
                {
                    return underAssets;
                }
            }

            string cwd = Directory.GetCurrentDirectory();
            string[] relatives =
            {
                Path.Combine("disputatio", "Assets", "Resources", "QA", "Scenarios", fileName),
                Path.Combine("Assets", "Resources", "QA", "Scenarios", fileName)
            };

            for (int i = 0; i < relatives.Length; i++)
            {
                string candidate = Path.GetFullPath(Path.Combine(cwd, relatives[i]));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string root = string.IsNullOrEmpty(dataPath) ? cwd : dataPath;
            return Path.Combine(root, "Resources", "QA", "Scenarios", fileName);
        }

        private DeveloperQaResult ExecuteRemaining()
        {
            IList<DeveloperQaScenarioStepDefinition> steps = _session.Scenario.Steps;
            while (_session.StepIndex < steps.Count)
            {
                if (_session.State == DeveloperQaScenarioStates.Cancelled)
                {
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.Cancelled,
                        "Scenario cancelled.",
                        checkpointId: _session.CheckpointId,
                        data: BuildStatusData());
                }

                DeveloperQaScenarioStepDefinition step = steps[_session.StepIndex];
                _session.CurrentStepId = step.Id;
                _session.CheckpointId = Guid.NewGuid().ToString("N");

                DeveloperQaCommand stepCommand = DeveloperQaCommand.Create(
                    step.Id,
                    step.Family,
                    step.Name,
                    step.TargetId,
                    step.Parameters);

                DeveloperQaResult stepResult = _executeStep(stepCommand);
                _session.LastResultCode = stepResult != null
                    ? stepResult.Code.ToString()
                    : DeveloperQaResultCode.InternalError.ToString();
                _session.LastMessage = stepResult != null ? stepResult.Message : "null step result";

                if (stepResult == null)
                {
                    _session.State = DeveloperQaScenarioStates.Failed;
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.InternalError,
                        "Step '" + step.Id + "' returned null.",
                        checkpointId: _session.CheckpointId,
                        data: BuildStatusData());
                }

                if (stepResult.Code != DeveloperQaResultCode.Ok)
                {
                    _session.State = DeveloperQaScenarioStates.Failed;
                    return new DeveloperQaResult(
                        stepResult.Code,
                        stepResult.Message,
                        missingCapabilityId: stepResult.MissingCapabilityId,
                        checkpointId: _session.CheckpointId,
                        data: MergeData(stepResult.Data, BuildStatusData()));
                }

                _session.StepIndex++;
            }

            _session.State = DeveloperQaScenarioStates.Completed;
            _session.CurrentStepId = string.Empty;
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Scenario completed.",
                checkpointId: _session.CheckpointId,
                data: BuildStatusData());
        }

        private IReadOnlyDictionary<string, string> BuildStatusData()
        {
            var data = new Dictionary<string, string>
            {
                ["state"] = _session.State,
                ["scenario_id"] = _session.Scenario.Id ?? string.Empty,
                ["scene"] = _session.Scenario.Scene ?? string.Empty,
                ["step_index"] = _session.StepIndex.ToString(CultureInfo.InvariantCulture),
                ["step_count"] = _session.Scenario.Steps.Count.ToString(CultureInfo.InvariantCulture),
                ["step_id"] = _session.CurrentStepId ?? string.Empty,
                ["checkpoint_id"] = _session.CheckpointId ?? string.Empty
            };

            if (!string.IsNullOrEmpty(_session.LastResultCode))
            {
                data["last_result_code"] = _session.LastResultCode;
            }

            if (!string.IsNullOrEmpty(_session.LastMessage))
            {
                data["last_message"] = _session.LastMessage;
            }

            return DeveloperQaMaps.From(data);
        }

        private static IReadOnlyDictionary<string, string> MergeData(
            IReadOnlyDictionary<string, string> primary,
            IReadOnlyDictionary<string, string> secondary)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (secondary != null)
            {
                foreach (KeyValuePair<string, string> pair in secondary)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            if (primary != null)
            {
                foreach (KeyValuePair<string, string> pair in primary)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            return DeveloperQaMaps.From(merged);
        }

        private sealed class Session
        {
            public Session(DeveloperQaScenarioDefinition scenario)
            {
                Scenario = scenario;
                CheckpointId = Guid.NewGuid().ToString("N");
                State = DeveloperQaScenarioStates.Idle;
                CurrentStepId = scenario.Steps.Count > 0 ? scenario.Steps[0].Id : string.Empty;
            }

            public DeveloperQaScenarioDefinition Scenario { get; }

            public string State { get; set; }

            public int StepIndex { get; set; }

            public string CurrentStepId { get; set; }

            public string CheckpointId { get; set; }

            public string LastResultCode { get; set; }

            public string LastMessage { get; set; }
        }
    }
}
#endif
