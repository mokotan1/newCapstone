#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// BasementHallway assert-route: expected destination arrival, finished transition,
    /// open input gate. Controller presence in the hallway is not a pass.
    /// </summary>
    public sealed class BasementHallwayQaRouteAssertionResult
    {
        public BasementHallwayQaRouteAssertionResult(bool passed, IReadOnlyList<string> reasonCodes)
        {
            Passed = passed;
            ReasonCodes = reasonCodes ?? Array.Empty<string>();
        }

        public bool Passed { get; }

        public IReadOnlyList<string> ReasonCodes { get; }
    }

    public static class BasementHallwayQaRouteAssertion
    {
        public const string ReasonDestinationMismatch = "destination-mismatch";
        public const string ReasonControllerOnly = "controller-only";
        public const string ReasonTransitionPending = "transition-pending";
        public const string ReasonInputGateBlocked = "input-gate-blocked";
        public const string ReasonNoExpectedDestination = "no-expected-destination";

        public static BasementHallwayQaRouteAssertionResult Evaluate(
            IReadOnlyDictionary<string, string> snapshot,
            string expectedScene)
        {
            var reasons = new List<string>();
            if (string.IsNullOrWhiteSpace(expectedScene))
            {
                reasons.Add(ReasonNoExpectedDestination);
                return new BasementHallwayQaRouteAssertionResult(false, reasons);
            }

            string activeScene = Read(snapshot, "activeScene");
            bool atDestination = string.Equals(activeScene, expectedScene, StringComparison.Ordinal);

            if (!atDestination)
            {
                reasons.Add(ReasonDestinationMismatch);
            }

            if (IsTrue(snapshot, "controllerFound") && !atDestination)
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

            return new BasementHallwayQaRouteAssertionResult(reasons.Count == 0, reasons);
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
