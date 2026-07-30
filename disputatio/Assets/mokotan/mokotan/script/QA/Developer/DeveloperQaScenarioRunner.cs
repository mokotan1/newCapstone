#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Developer
{
    /// <summary>
    /// Executes DeveloperQa-command scenario JSON with status / resume / cancel (Task 9).
    /// Full <c>QaScenarioRunner</c> integration is skipped because that schema cannot
    /// express StudyRoom capability IDs (<c>studyroom.mirror.*</c>).
    /// Steps are awaited so Fungus Say/Wait can complete across player-loop frames.
    /// </summary>
    public sealed class DeveloperQaScenarioRunner
    {
        private readonly DeveloperQaScenarioValidator _validator;
        private readonly Func<DeveloperQaCommand, CancellationToken, Task<DeveloperQaResult>> _executeStep;
        private Session _session;

        public DeveloperQaScenarioRunner(Func<DeveloperQaCommand, DeveloperQaResult> executeStep)
            : this(
                (command, _) => Task.FromResult(executeStep(command)),
                new DeveloperQaScenarioValidator())
        {
        }

        public DeveloperQaScenarioRunner(
            Func<DeveloperQaCommand, DeveloperQaResult> executeStep,
            DeveloperQaScenarioValidator validator)
            : this(
                (command, _) => Task.FromResult(executeStep(command)),
                validator)
        {
        }

        public DeveloperQaScenarioRunner(
            Func<DeveloperQaCommand, CancellationToken, Task<DeveloperQaResult>> executeStep)
            : this(executeStep, new DeveloperQaScenarioValidator())
        {
        }

        public DeveloperQaScenarioRunner(
            Func<DeveloperQaCommand, CancellationToken, Task<DeveloperQaResult>> executeStep,
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
            return BeginAsync(command, executeSteps, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
        }

        public Task<DeveloperQaResult> BeginAsync(
            DeveloperQaCommand command,
            bool executeSteps,
            CancellationToken cancellationToken)
        {
            if (_session != null
                && (_session.State == DeveloperQaScenarioStates.Running))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "A DeveloperQa scenario is already running. Cancel or wait for completion."));
            }

            string json;
            DeveloperQaResult loadResult = TryLoadScenarioJson(command, out json);
            if (loadResult.Code != DeveloperQaResultCode.Ok)
            {
                return Task.FromResult(loadResult);
            }

            DeveloperQaScenarioValidationResult validation = _validator.Validate(json);
            if (!validation.IsValid)
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "Invalid scenario: " + string.Join("; ", validation.Errors)));
            }

            DeveloperQaScenarioDefinition scenario = validation.Scenario;
            _session = new Session(scenario)
            {
                State = DeveloperQaScenarioStates.Running,
                StepIndex = 0
            };

            if (!executeSteps)
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Scenario session started (deferred execution).",
                    checkpointId: _session.CheckpointId,
                    data: BuildStatusData()));
            }

            return ExecuteRemainingAsync(cancellationToken);
        }

        public DeveloperQaResult Resume()
        {
            return ResumeAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<DeveloperQaResult> ResumeAsync(CancellationToken cancellationToken)
        {
            if (_session == null)
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.InvalidCommand,
                    "No scenario session to resume."));
            }

            if (_session.State == DeveloperQaScenarioStates.Cancelled)
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.Cancelled,
                    "Scenario was cancelled; start a new run."));
            }

            if (_session.State == DeveloperQaScenarioStates.Completed)
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Scenario already completed.",
                    data: BuildStatusData()));
            }

            _session.State = DeveloperQaScenarioStates.Running;
            return ExecuteRemainingAsync(cancellationToken);
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

            if (command.Parameters != null
                && command.Parameters.TryGetValue("scenario_json", out string fromJson)
                && !string.IsNullOrWhiteSpace(fromJson))
            {
                json = fromJson;
                return new DeveloperQaResult(DeveloperQaResultCode.Ok, "loaded");
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

            string trimmedId = scenarioId.Trim();
            string fileName = trimmedId;
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

                string nested = FindNestedScenarioPath(
                    Path.Combine(dataPath, "Resources", "QA", "Scenarios"),
                    trimmedId);
                if (!string.IsNullOrEmpty(nested))
                {
                    return nested;
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

            string[] searchRoots =
            {
                Path.GetFullPath(Path.Combine(cwd, "disputatio", "Assets", "Resources", "QA", "Scenarios")),
                Path.GetFullPath(Path.Combine(cwd, "Assets", "Resources", "QA", "Scenarios"))
            };
            for (int i = 0; i < searchRoots.Length; i++)
            {
                string nested = FindNestedScenarioPath(searchRoots[i], trimmedId);
                if (!string.IsNullOrEmpty(nested))
                {
                    return nested;
                }
            }

            string root = string.IsNullOrEmpty(dataPath) ? cwd : dataPath;
            return Path.Combine(root, "Resources", "QA", "Scenarios", fileName);
        }

        /// <summary>
        /// Finds a nested room-pack JSON under <c>QA/Scenarios/Rooms/**</c> whose
        /// top-level <c>id</c> matches <paramref name="scenarioId"/>.
        /// </summary>
        private static string FindNestedScenarioPath(string scenariosRoot, string scenarioId)
        {
            if (string.IsNullOrEmpty(scenariosRoot) || !Directory.Exists(scenariosRoot))
            {
                return null;
            }

            string roomsRoot = Path.Combine(scenariosRoot, "Rooms");
            if (!Directory.Exists(roomsRoot))
            {
                return null;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(roomsRoot, "*.json", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "manifest.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "catalog.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "exclusions.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    continue;
                }

                if (ScenarioJsonDeclaresId(text, scenarioId))
                {
                    return path;
                }
            }

            return null;
        }

        private static bool ScenarioJsonDeclaresId(string json, string scenarioId)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(scenarioId))
            {
                return false;
            }

            // Lightweight match avoids a full deserialize on every file during path lookup.
            string quoted = "\"" + scenarioId + "\"";
            int idKey = json.IndexOf("\"id\"", StringComparison.Ordinal);
            if (idKey < 0)
            {
                return false;
            }

            int valueStart = json.IndexOf(quoted, idKey, StringComparison.Ordinal);
            return valueStart > idKey;
        }

        private async Task<DeveloperQaResult> ExecuteRemainingAsync(CancellationToken cancellationToken)
        {
            IList<DeveloperQaScenarioStepDefinition> steps = _session.Scenario.Steps;
            while (_session.StepIndex < steps.Count)
            {
                if (cancellationToken.IsCancellationRequested
                    || _session.State == DeveloperQaScenarioStates.Cancelled)
                {
                    _session.State = DeveloperQaScenarioStates.Cancelled;
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

                DeveloperQaResult stepResult = await _executeStep(stepCommand, cancellationToken)
                    .ConfigureAwait(true);
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
