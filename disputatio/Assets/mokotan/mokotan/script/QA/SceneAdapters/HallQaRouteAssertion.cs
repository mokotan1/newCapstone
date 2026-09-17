#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// AC07: Hall assert-route passes only after Kitchen arrival, a finished
    /// transition, and an open input gate. Controller presence in Hall is not a pass.
    /// </summary>
    public sealed class HallQaRouteAssertionResult
    {
        public HallQaRouteAssertionResult(bool passed, IReadOnlyList<string> reasonCodes)
        {
            Passed = passed;
            ReasonCodes = reasonCodes ?? Array.Empty<string>();
        }

        public bool Passed { get; }

        public IReadOnlyList<string> ReasonCodes { get; }
    }

    public static class HallQaRouteAssertion
    {
        public const string ReasonDestinationMismatch = "destination-mismatch";
        public const string ReasonControllerOnly = "controller-only";
        public const string ReasonTransitionPending = "transition-pending";
        public const string ReasonInputGateBlocked = "input-gate-blocked";

        public static HallQaRouteAssertionResult Evaluate(IReadOnlyDictionary<string, string> snapshot)
        {
            var reasons = new List<string>();
            string activeScene = Read(snapshot, "activeScene");
            bool atKitchen = string.Equals(activeScene, SceneNames.Kitchen, StringComparison.Ordinal);

            if (!atKitchen)
            {
                reasons.Add(ReasonDestinationMismatch);
            }

            if (IsTrue(snapshot, "controllerFound") && !atKitchen)
            {
                reasons.Add(ReasonControllerOnly);
            }

            if (!IsFalse(snapshot, "transitionPending"))
            {
                reasons.Add(ReasonTransitionPending);
            }

            if (!IsFalse(snapshot, "inputGateBlocked"))
            {
                reasons.Add(ReasonInputGateBlocked);
            }

            return new HallQaRouteAssertionResult(reasons.Count == 0, reasons);
        }

        private static string Read(IReadOnlyDictionary<string, string> snapshot, string key)
        {
            string value;
            if (snapshot == null || !snapshot.TryGetValue(key, out value) || value == null)
            {
                return string.Empty;
            }

            return value;
        }

        private static bool IsTrue(IReadOnlyDictionary<string, string> snapshot, string key)
        {
            return string.Equals(Read(snapshot, key), bool.TrueString, StringComparison.Ordinal);
        }

        private static bool IsFalse(IReadOnlyDictionary<string, string> snapshot, string key)
        {
            return string.Equals(Read(snapshot, key), bool.FalseString, StringComparison.Ordinal);
        }
    }
}
#endif
