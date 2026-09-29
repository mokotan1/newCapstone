#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.QA.Input;
using Godlotto.QA.Scenarios;

namespace Godlotto.QA.Developer
{
    public static class QaDeveloperStepEvidenceBuilder
    {
        public static IReadOnlyDictionary<string, string> ForDeveloperStep(
            string stepId,
            string family,
            string name,
            string targetId,
            DeveloperQaResult result)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["stepId"] = stepId ?? string.Empty,
                ["family"] = family ?? string.Empty,
                ["name"] = name ?? string.Empty,
                ["targetId"] = targetId ?? string.Empty
            };

            if (string.Equals(family, "interaction", StringComparison.Ordinal)
                && string.Equals(name, "pointer", StringComparison.Ordinal))
            {
                data["executionDispatch"] = QaStepEvidenceBuilder.ExecutionDispatchRealInput;
                data["inputMode"] = QaInteractionMode.RealInput.ToString();
            }
            else if (string.Equals(family, "interaction", StringComparison.Ordinal)
                     && string.Equals(name, "invoke", StringComparison.Ordinal))
            {
                data["executionDispatch"] = QaStepEvidenceBuilder.ExecutionDispatchCapabilityApi;
                data["capabilityId"] = targetId ?? string.Empty;
            }

            if (result != null)
            {
                data["resultCode"] = result.Code.ToString();
                if (result.Data != null)
                {
                    foreach (KeyValuePair<string, string> pair in result.Data)
                    {
                        if (string.IsNullOrEmpty(pair.Key))
                        {
                            continue;
                        }

                        if (string.Equals(pair.Key, "csharp_dispatch", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(pair.Key, "csharpDispatch", StringComparison.OrdinalIgnoreCase))
                        {
                            data["csharpDispatch"] = pair.Value ?? string.Empty;
                            continue;
                        }

                        data[pair.Key] = pair.Value ?? string.Empty;
                    }
                }
            }

            return data;
        }
    }
}
#endif
