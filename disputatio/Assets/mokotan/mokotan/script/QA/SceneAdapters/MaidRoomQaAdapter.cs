#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// MaidRoom QA adapter (Task 12 + Wave 2 food capabilities). Drives the real
    /// MaidRoom.unity InteractionRoute id <see cref="FoodInteractionId"/> ("food",
    /// fungusBlockName food / GetFood variable) through
    /// <see cref="MaidRoomPuzzleController.OnInteraction(string)"/> — the same entry
    /// point a player click uses. No ForceSolve; missing controller → explicit failure.
    ///
    /// Preset <c>maidroom.food.preset.before-tray</c> is omitted: there is no safe public
    /// mutator to reset GetFood without inventing scene state.
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class MaidRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string FoodTrayTargetIdValue = "maidroom.food-tray";

        /// <summary>
        /// Real MaidRoom.unity InteractionRoute.interactionId (fungusBlockName: food).
        /// </summary>
        public const string FoodInteractionId = "food";

        public const string FoodClickCapabilityId = "maidroom.food.click-tray";
        public const string FoodProbeCapabilityId = "maidroom.food.probe";
        public const string FoodAssertEffectCapabilityId = "maidroom.food.assert-effect";
        public const string FoodCaptureCapabilityId = "maidroom.food.capture";

        private static readonly QaTargetId FoodTrayTargetId = QaTargetId.Create(FoodTrayTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { FoodTrayTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.MaidRoom; }
        }

        public IReadOnlyCollection<QaTargetId> TargetIds
        {
            get { return DeclaredTargetIds; }
        }

        public IReadOnlyCollection<string> PresetIds
        {
            get { return DeclaredPresetIds; }
        }

        /// <summary>
        /// Registers MaidRoom food-tray developer capabilities and their handlers.
        /// Handlers thin-wrap <see cref="TryClick"/> and <see cref="CaptureSnapshot"/> —
        /// never force-solve.
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.MaidRoom;
            var adapter = new MaidRoomQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    FoodClickCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    FoodProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool,getFood:bool|unknown}"),
                _ => MapSnapshot(adapter, assertEffect: false));

            registry.Register(
                new DeveloperQaCapability(
                    FoodCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool,getFood:bool|unknown}"),
                _ => MapSnapshot(adapter, assertEffect: false));

            registry.Register(
                new DeveloperQaCapability(
                    FoodAssertEffectCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{controllerFound:bool,getFood:bool|unknown}"),
                _ => MapSnapshot(adapter, assertEffect: true));
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            MaidRoomPuzzleController controller = ResolveController();
            Flowchart flowchart = FlowchartLocator.Find();
            string getFood = "unknown";
            if (flowchart != null)
            {
                getFood = flowchart.GetBooleanVariable(FungusVariableKeys.GetFood).ToString();
            }

            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["flowchartFound"] = (flowchart != null).ToString(),
                ["getFood"] = getFood
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != FoodTrayTargetId)
            {
                error = "MaidRoomQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            MaidRoomPuzzleController controller = ResolveController();
            if (controller == null)
            {
                error = "MaidRoomPuzzleController not found in the active scene. This adapter " +
                    "only works while the MaidRoom scene is the active Play Mode scene.";
                return false;
            }

            // MaidRoom.unity InteractionRoute: interactionId "food" → fungus block "food".
            controller.OnInteraction(FoodInteractionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "MaidRoomQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "MaidRoomQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult MapClick(MaidRoomQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "MaidRoomQaAdapter instance is required for food-tray click.");
            }

            string error;
            if (adapter.TryClick(FoodTrayTargetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "MaidRoom food-tray click dispatched (OnInteraction(\"food\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True"
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "MaidRoom food-tray click blocked (MaidRoom scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(MaidRoomQaAdapter adapter, bool assertEffect)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "MaidRoomQaAdapter instance is required for food snapshot.");
            }

            QaSceneSnapshot snapshot = adapter.CaptureSnapshot();
            var data = new Dictionary<string, string>();
            if (snapshot != null && snapshot.Values != null)
            {
                foreach (KeyValuePair<string, string> pair in snapshot.Values)
                {
                    data[pair.Key] = pair.Value;
                }
            }

            string getFood;
            if (!data.TryGetValue("getFood", out getFood))
            {
                getFood = "unknown";
            }

            string flowchartFound;
            if (!data.TryGetValue("flowchartFound", out flowchartFound))
            {
                flowchartFound = "False";
            }

            if (assertEffect)
            {
                // No Variablemanager / Flowchart → cannot evaluate GetFood honestly.
                if (!string.Equals(flowchartFound, bool.TrueString, StringComparison.Ordinal) ||
                    string.Equals(getFood, "unknown", StringComparison.Ordinal))
                {
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.EnvironmentBlocked,
                        "Fungus flowchart (Variablemanager) not found; cannot assert GetFood. " +
                        "Requires MaidRoom Play Mode with Variablemanager Flowchart present.",
                        data: data);
                }

                if (!string.Equals(getFood, bool.TrueString, StringComparison.Ordinal))
                {
                    return new DeveloperQaResult(
                        DeveloperQaResultCode.AssertionFailed,
                        "Expected getFood=True but was '" + getFood + "'.",
                        data: data);
                }
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                assertEffect
                    ? "MaidRoom food assert-effect passed."
                    : "MaidRoom food snapshot captured.",
                data: data);
        }

        private static MaidRoomPuzzleController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<MaidRoomPuzzleController>();
        }
    }
}
#endif
