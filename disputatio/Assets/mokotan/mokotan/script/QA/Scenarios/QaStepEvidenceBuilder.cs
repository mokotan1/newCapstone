#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
namespace Godlotto.QA.Scenarios
{
    public static class QaStepEvidenceBuilder
    {
        public const string ExecutionDispatchRealInput = "real-input";
        public const string ExecutionDispatchApiOnly = "api-only";
        public const string ExecutionDispatchCapabilityApi = "capability-api";

        public static IReadOnlyDictionary<string, string> ForInputStep(
            QaScenarioStepDefinition step,
            string targetId,
            QaInputResult result)
        {
            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stepId"] = step.Id ?? string.Empty,
                ["targetId"] = targetId ?? string.Empty
            };

            if (result == null)
            {
                data["executionDispatch"] = ExecutionDispatchApiOnly;
                data["inputMode"] = QaInteractionMode.Api.ToString();
                data["inputCode"] = QaInputResultCode.InternalError.ToString();
                return data;
            }

            data["inputMode"] = result.Mode.ToString();
            data["inputCode"] = result.Code.ToString();
            data["executionDispatch"] = result.Mode == QaInteractionMode.RealInput
                ? ExecutionDispatchRealInput
                : ExecutionDispatchApiOnly;

            if (!string.IsNullOrEmpty(result.Message))
            {
                data["message"] = result.Message;
            }

            string mode = ReadStepMode(step);
            if (!string.IsNullOrEmpty(mode))
            {
                data["requestedMode"] = mode;
            }

            return data;
        }

        public static IReadOnlyDictionary<string, string> ForCapabilityStep(
            string stepId,
            string capabilityId,
            bool succeeded,
            IReadOnlyDictionary<string, string> resultData = null)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stepId"] = stepId ?? string.Empty,
                ["capabilityId"] = capabilityId ?? string.Empty,
                ["executionDispatch"] = ExecutionDispatchCapabilityApi,
                ["outcome"] = succeeded ? "Success" : "Failed"
            };

            MergeResultData(data, resultData);
            return data;
        }

        public static IReadOnlyDictionary<string, string> ForAssertionStep(
            string stepId,
            QaAssertionResult assertionResult,
            QaDriverSnapshot snapshot)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stepId"] = stepId ?? string.Empty,
                ["executionDispatch"] = ExecutionDispatchApiOnly,
                ["outcome"] = assertionResult != null && assertionResult.Passed ? "Passed" : "Failed"
            };

            if (assertionResult != null)
            {
                data["observedValue"] = assertionResult.ObservedValue ?? string.Empty;
            }

            if (snapshot != null)
            {
                data["sceneName"] = snapshot.SceneName ?? string.Empty;
                data["inputGateLocked"] = snapshot.InputGateLocked.ToString(CultureInfo.InvariantCulture);
                data["consoleErrorCount"] = snapshot.ConsoleErrorCount.ToString(CultureInfo.InvariantCulture);
            }

            return data;
        }

        public static bool StepRequiresRealInput(QaScenarioStepDefinition step)
        {
            if (step == null)
            {
                return false;
            }

            if (!string.Equals(step.Command, QaScenarioSchema.CommandInteractionPointer, StringComparison.Ordinal))
            {
                return false;
            }

            string mode = ReadStepMode(step);
            if (string.IsNullOrEmpty(mode))
            {
                return true;
            }

            return string.Equals(mode, "realInput", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadStepMode(QaScenarioStepDefinition step)
        {
            if (step.Parameters == null)
            {
                return string.Empty;
            }

            if (step.Parameters.TryGetValue("mode", out string mode))
            {
                return mode?.Trim() ?? string.Empty;
            }

            return string.Empty;
        }

        private static void MergeResultData(
            Dictionary<string, string> target,
            IReadOnlyDictionary<string, string> resultData)
        {
            if (resultData == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in resultData)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                if (string.Equals(pair.Key, "csharp_dispatch", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Key, "csharpDispatch", StringComparison.OrdinalIgnoreCase))
                {
                    target["csharpDispatch"] = pair.Value ?? string.Empty;
                    continue;
                }

                target[pair.Key] = pair.Value ?? string.Empty;
            }
        }
    }
}
#endif
