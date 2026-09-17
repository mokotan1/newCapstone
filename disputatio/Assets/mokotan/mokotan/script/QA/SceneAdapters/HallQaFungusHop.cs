#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Hall_Left / Hall_Left2 have no C# interaction ids. Kitchen is reached by
    /// executing the wired Fungus blocks, then loading via those blocks.
    /// This is a static hop helper, not a new gameplay Manager.
    /// </summary>
    public static class HallQaFungusHop
    {
        public const string FrontBlockName = "Front_clicked";
        public const string DoorBlockName = "Door_Clicked";
        public const string ExecuteFrontCapabilityId = "hall.nav.execute-front";
        public const string ExecuteDoorCapabilityId = "hall.nav.execute-door";
        public const string ResetToHallCapabilityId = "hall.nav.reset-to-hall";

        /// <summary>
        /// Returns the Fungus block that advances the frozen Hall→Kitchen hop
        /// from the given scene, or null when the scene uses CorridorEntranceController.
        /// </summary>
        public static string BlockNameForScene(string sceneName)
        {
            if (string.Equals(sceneName, SceneNames.HallLeft, System.StringComparison.Ordinal))
            {
                return FrontBlockName;
            }

            if (string.Equals(sceneName, SceneNames.HallLeft2, System.StringComparison.Ordinal))
            {
                return DoorBlockName;
            }

            return null;
        }

        /// <summary>
        /// Play Mode only: execute a named Fungus block on any loaded Flowchart.
        /// </summary>
        public static DeveloperQaResult MapExecute(string blockName)
        {
            string error;
            if (!TryExecuteBlock(blockName, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    error,
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "False",
                        ["blockName"] = blockName ?? string.Empty,
                        ["activeScene"] = SceneManager.GetActiveScene().name
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Hall fungus hop dispatched (" + blockName + ").",
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "True",
                    ["blockName"] = blockName,
                    ["activeScene"] = SceneManager.GetActiveScene().name
                });
        }

        /// <summary>
        /// Play Mode only: reload Hall_playerble so the event-system layer starts clean.
        /// </summary>
        public static DeveloperQaResult MapResetToHall()
        {
            string error;
            if (!TryResetToHall(out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    error,
                    data: new Dictionary<string, string>
                    {
                        ["reset"] = "False",
                        ["activeScene"] = SceneManager.GetActiveScene().name
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Hall hop layer reset to Hall_playerble.",
                data: new Dictionary<string, string>
                {
                    ["reset"] = "True",
                    ["activeScene"] = SceneManager.GetActiveScene().name
                });
        }

        public static bool TryExecuteBlock(string blockName, out string error)
        {
            if (!Application.isPlaying)
            {
                error = "Hall fungus hop requires Play Mode.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(blockName))
            {
                error = "Hall fungus hop block name is empty.";
                return false;
            }

            Flowchart[] flowcharts = Object.FindObjectsByType<Flowchart>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < flowcharts.Length; i++)
            {
                Flowchart flowchart = flowcharts[i];
                if (flowchart == null || !flowchart.HasBlock(blockName))
                {
                    continue;
                }

                if (FungusDialogueBridge.ExecuteBlockSafely(flowchart, blockName))
                {
                    error = null;
                    return true;
                }
            }

            error = "Fungus block '" + blockName + "' was not found or was already executing.";
            return false;
        }

        public static bool TryResetToHall(out string error)
        {
            if (!Application.isPlaying)
            {
                error = "Hall hop reset requires Play Mode.";
                return false;
            }

            if (!SceneTransitionService.LoadSceneSafely(SceneNames.HallPlayable))
            {
                error = "SceneTransitionService refused Hall_playerble load.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
#endif
