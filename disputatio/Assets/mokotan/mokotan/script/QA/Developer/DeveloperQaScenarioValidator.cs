#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace Godlotto.QA.Developer
{
    /// <summary>Immutable result of <see cref="DeveloperQaScenarioValidator.Validate"/>.</summary>
    public sealed class DeveloperQaScenarioValidationResult
    {
        private static readonly IReadOnlyList<string> EmptyErrors =
            new ReadOnlyCollection<string>(new List<string>());

        public bool IsValid { get; }

        public DeveloperQaScenarioDefinition Scenario { get; }

        public IReadOnlyList<string> Errors { get; }

        private DeveloperQaScenarioValidationResult(
            bool isValid,
            DeveloperQaScenarioDefinition scenario,
            IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Scenario = scenario;
            Errors = errors ?? EmptyErrors;
        }

        public static DeveloperQaScenarioValidationResult Success(DeveloperQaScenarioDefinition scenario)
        {
            return new DeveloperQaScenarioValidationResult(true, scenario, EmptyErrors);
        }

        public static DeveloperQaScenarioValidationResult Failure(IReadOnlyList<string> errors)
        {
            return new DeveloperQaScenarioValidationResult(false, null, errors ?? EmptyErrors);
        }
    }

    /// <summary>
    /// Strict parser for DeveloperQa scenario JSON (Task 9). Uses Newtonsoft POCO
    /// binding only (<c>TypeNameHandling.None</c>) — no arbitrary C# type execution.
    /// </summary>
    public sealed class DeveloperQaScenarioValidator
    {
        private static readonly JsonSerializerSettings SafeSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private static readonly HashSet<string> AllowedFamilies = new HashSet<string>(StringComparer.Ordinal)
        {
            "capability", "preset", "scene", "interaction", "state", "evidence"
        };

        public DeveloperQaScenarioValidationResult Validate(string scenarioJson)
        {
            if (string.IsNullOrWhiteSpace(scenarioJson))
            {
                return DeveloperQaScenarioValidationResult.Failure(
                    new[] { "Scenario JSON must not be blank." });
            }

            DeveloperQaScenarioDefinition scenario;
            try
            {
                scenario = JsonConvert.DeserializeObject<DeveloperQaScenarioDefinition>(
                    scenarioJson,
                    SafeSettings);
            }
            catch (JsonException ex)
            {
                return DeveloperQaScenarioValidationResult.Failure(
                    new[] { "Scenario JSON is malformed: " + ex.Message });
            }

            if (scenario == null)
            {
                return DeveloperQaScenarioValidationResult.Failure(
                    new[] { "Scenario JSON did not deserialize to an object." });
            }

            return Validate(scenario);
        }

        public DeveloperQaScenarioValidationResult Validate(DeveloperQaScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                return DeveloperQaScenarioValidationResult.Failure(
                    new[] { "Scenario definition is required." });
            }

            var errors = new List<string>();

            if (scenario.SchemaVersion != DeveloperQaScenarioDefinition.SupportedSchemaVersion)
            {
                errors.Add(
                    "Unsupported schemaVersion "
                    + scenario.SchemaVersion
                    + " (expected "
                    + DeveloperQaScenarioDefinition.SupportedSchemaVersion
                    + ").");
            }

            if (string.IsNullOrWhiteSpace(scenario.Id))
            {
                errors.Add("Scenario id is required.");
            }

            if (string.IsNullOrWhiteSpace(scenario.Scene))
            {
                errors.Add("Scenario scene is required.");
            }

            if (scenario.Steps == null || scenario.Steps.Count == 0)
            {
                errors.Add("Scenario steps must contain at least one step.");
            }
            else
            {
                var seenIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < scenario.Steps.Count; i++)
                {
                    DeveloperQaScenarioStepDefinition step = scenario.Steps[i];
                    string prefix = "steps[" + i + "]";
                    if (step == null)
                    {
                        errors.Add(prefix + " must not be null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(step.Id))
                    {
                        errors.Add(prefix + ".id is required.");
                    }
                    else if (!seenIds.Add(step.Id))
                    {
                        errors.Add("Duplicate step id '" + step.Id + "'.");
                    }

                    if (string.IsNullOrWhiteSpace(step.Family) || !AllowedFamilies.Contains(step.Family))
                    {
                        errors.Add(
                            prefix
                            + ".family '"
                            + step.Family
                            + "' is not an allowed DeveloperQa family "
                            + "(scenario.* steps are not nestable).");
                    }

                    if (string.IsNullOrWhiteSpace(step.Name))
                    {
                        errors.Add(prefix + ".name is required.");
                    }

                    if (RequiresTargetId(step.Family, step.Name)
                        && string.IsNullOrWhiteSpace(step.TargetId))
                    {
                        errors.Add(prefix + ".targetId is required for " + step.Family + "." + step.Name + ".");
                    }
                }
            }

            if (errors.Count > 0)
            {
                return DeveloperQaScenarioValidationResult.Failure(errors);
            }

            return DeveloperQaScenarioValidationResult.Success(scenario);
        }

        private static bool RequiresTargetId(string family, string name)
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

            if (family == "capability" && name == "describe")
            {
                return true;
            }

            if (family == "scene" && (name == "load" || name == "waitReady"))
            {
                return true;
            }

            return false;
        }
    }
}
#endif
