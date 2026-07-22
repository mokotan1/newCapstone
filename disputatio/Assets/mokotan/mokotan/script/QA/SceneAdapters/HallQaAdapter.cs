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
    /// Hall (<see cref="SceneNames.HallPlayable"/>) QA adapter (Task 12). Registration-only stub:
    /// the scene's real controller (<see cref="CorridorEntranceController"/>, a
    /// <see cref="RoomInteractionController"/> subclass) only exposes generic
    /// <c>OnInteraction(string interactionId)</c>, and the concrete interaction id strings for
    /// "go to Kitchen" etc. are Inspector-serialized <c>InteractionRoute[]</c> data on the scene
    /// object -- not discoverable from source code. Rather than guess an id (which would either
    /// silently no-op via <c>RoomInteractionController</c>'s fire-and-forget
    /// <c>LogIgnored</c> path, or -- worse -- coincidentally match the wrong route), this adapter
    /// is registered purely so <c>hall.kitchen-quest</c> validates against the schema/registry and
    /// is discoverable via qa_list; <see cref="TryClick"/> fails explicitly with this gap instead
    /// of fabricating a route.
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class HallQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string KitchenEntryTargetIdValue = "hall.kitchen-entry";

        private static readonly QaTargetId KitchenEntryTargetId = QaTargetId.Create(KitchenEntryTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { KitchenEntryTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.HallPlayable; }
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
                ["corridorEntranceControllerFound"] = (ResolveController() != null).ToString(),
                ["gap"] = "Task 12 registration-only stub; real InteractionRoute ids are scene-Inspector data."
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
            return "Gap (Task 12): HallQaAdapter is a registration-only stub for target '" + targetId +
                "' -- the real Fungus/InteractionRoute wiring for Hall is Inspector-serialized data " +
                "not available from source, so no interaction id is guessed. Follow-up task must " +
                "inspect the Hall_playerble scene asset to wire a real interactionId.";
        }

        private static CorridorEntranceController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<CorridorEntranceController>();
        }
    }
}
#endif
