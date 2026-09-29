#if UNITY_EDITOR
using System;
using Godlotto.Interaction;
using Godlotto.QA.Evidence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godlotto.QA.EditorCli
{
    /// <summary>
    /// Wires Editor live scene, input gate, and console error observations into
    /// <see cref="QaStateProbe"/> for gateway assertions and evidence finalization.
    /// </summary>
    internal static class EditorQaLiveSnapshotProbe
    {
        private static bool logHookRegistered;

        public static Func<QaDriverSnapshot> CreateCaptureCallback()
        {
            EnsureConsoleHook();
            var probe = new QaStateProbe(
                sceneNameProvider: () => SceneManager.GetActiveScene().name,
                inputGateLockedProvider: () => InteractionInputGate.IsBlocked,
                consoleErrorCountProvider: () => EditorQaConsoleErrorTracker.ErrorCount);
            return () => probe.Capture();
        }

        private static void EnsureConsoleHook()
        {
            if (logHookRegistered)
            {
                return;
            }

            logHookRegistered = true;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                EditorQaConsoleErrorTracker.RecordError();
            }
        }
    }

    internal static class EditorQaConsoleErrorTracker
    {
        public static int ErrorCount { get; private set; }

        public static void RecordError()
        {
            ErrorCount++;
        }

        public static void ResetForTests()
        {
            ErrorCount = 0;
        }
    }
}
#endif
