#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.Interaction;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// MaidRoom QA adapter (Task 12). Registration-only stub, same rationale as
    /// <see cref="HallQaAdapter"/>: the real controller
    /// (<see cref="MaidRoomPuzzleController"/>, a <see cref="RoomInteractionController"/>
    /// subclass) only exposes generic <c>OnInteraction(string interactionId)</c>, and the "food
    /// tray" interaction id (and the "food effect" it triggers) is Inspector-serialized
    /// <c>InteractionRoute[]</c>/Fungus data on the scene object with no discoverable C# constant
    /// (unlike Kitchen's <c>KitchenSinkInteractionGate</c>/<c>KitchenParretInteractionGate</c>).
    /// This adapter is registered purely so <c>maidroom.food-effect</c> validates against the
    /// schema/registry and is discoverable via qa_list; <see cref="TryClick"/> fails explicitly
    /// with this gap instead of fabricating a route.
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class MaidRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string FoodTrayTargetIdValue = "maidroom.food-tray";

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

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            var values = new Dictionary<string, string>
            {
                ["maidRoomPuzzleControllerFound"] = (ResolveController() != null).ToString(),
                ["gap"] = "Task 12 registration-only stub; real food-tray interactionId is scene-Inspector data."
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            error = BuildGapMessage(targetId);
            return false;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = BuildGapMessage(sourceTargetId);
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = BuildGapMessage(targetId);
            return false;
        }

        private static string BuildGapMessage(QaTargetId targetId)
        {
            return "Gap (Task 12): MaidRoomQaAdapter is a registration-only stub for target '" + targetId +
                "' -- the real Fungus/InteractionRoute wiring for the MaidRoom food effect is " +
                "Inspector-serialized data not available from source, so no interaction id is " +
                "guessed. Follow-up task must inspect the MaidRoom scene asset to wire a real interactionId.";
        }

        private static MaidRoomPuzzleController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<MaidRoomPuzzleController>();
        }
    }
}
#endif
