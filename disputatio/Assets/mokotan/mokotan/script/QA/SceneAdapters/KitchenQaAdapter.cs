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
    /// Kitchen scene QA adapter (Task 12). Drives two real, already-shipped interaction routes
    /// through <see cref="KitchenInteractionController.OnInteraction(string)"/> (inherited from
    /// <see cref="RoomInteractionController"/>, the exact entry point a player click/tap uses):
    /// the sink faucet (<see cref="KitchenSinkInteractionGate.FaucetInteractionId"/>) and the
    /// Cheshire "parret" world click (<see cref="KitchenParretInteractionGate.ParretInteractionId"/>).
    /// Setup/reset uses only the public mutators already exposed by <see cref="KitchenPuzzleState"/>
    /// (<see cref="KitchenPuzzleState.SetFaucetClicked"/>, <see cref="KitchenPuzzleState.SetParretClicked"/>)
    /// -- no private reflection.
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks (same rationale
    /// as <see cref="MainMenuQaAdapter"/> -- default assembly only, mirrors the QaProfileService split).
    ///
    /// Known gap (Task 12): <see cref="RoomInteractionController.OnInteraction(string)"/> is
    /// deliberately fire-and-forget -- an unknown interaction id, a puzzle-state gate, or a modal
    /// input block are all silently logged (never thrown, never returned as a failure) by design.
    /// <see cref="TryClick"/> therefore reports success as soon as the owning controller is found
    /// in the active scene, matching the real public contract exactly; it cannot distinguish
    /// "block executed" from "gated/no-op" without further state-probe wiring
    /// (<c>QaStateProbe</c> has no Kitchen-specific providers yet -- see class remarks in
    /// <c>QaCommandGateway</c>). Scenario JSON compensates with explicit <c>state.assert</c> steps.
    /// </summary>
    public sealed class KitchenQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string FaucetTargetIdValue = "kitchen.sink.faucet";
        public const string ParretTargetIdValue = "kitchen.parret";
        public const string BeforeFaucetPresetId = "before-faucet";
        public const string BeforeParretPresetId = "before-parret";

        private static readonly QaTargetId FaucetTargetId = QaTargetId.Create(FaucetTargetIdValue);
        private static readonly QaTargetId ParretTargetId = QaTargetId.Create(ParretTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { FaucetTargetId, ParretTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds =
            new List<string> { BeforeFaucetPresetId, BeforeParretPresetId };

        public string SceneName
        {
            get { return SceneNames.Kitchen; }
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
            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState == null)
            {
                return QaScenePresetResult.Failed(
                    "KitchenPuzzleState not found in the active scene. This preset only works " +
                    "while the Kitchen scene is the active Play Mode scene.");
            }

            if (string.Equals(presetId, BeforeFaucetPresetId, StringComparison.Ordinal))
            {
                // Idempotent baseline: the faucet has not been clicked yet in this run.
                puzzleState.SetFaucetClicked(false);
                return QaScenePresetResult.Success("Kitchen faucet baseline reset (FaucetClicked=false).");
            }

            if (string.Equals(presetId, BeforeParretPresetId, StringComparison.Ordinal))
            {
                // Idempotent baseline for repeat testing: only ParretClicked is reset via the
                // real public setter. ComeParret is Fungus-driven (RefreshComeParretFromFungus)
                // and has no public setter, so this preset cannot force it -- if ComeParret is
                // still false, KitchenParretInteractionGate silently no-ops the click (see class
                // remarks); that is a real, documented gap, not something this preset can paper over.
                puzzleState.SetParretClicked(false);
                return QaScenePresetResult.Success("Kitchen parret baseline reset (ParretClicked=false).");
            }

            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            var values = new Dictionary<string, string>
            {
                ["puzzleStateFound"] = (puzzleState != null).ToString(),
                ["faucetClicked"] = puzzleState != null ? puzzleState.FaucetClicked.ToString() : "unknown",
                ["comeParret"] = puzzleState != null ? puzzleState.ComeParret.ToString() : "unknown",
                ["parretClicked"] = puzzleState != null ? puzzleState.ParretClicked.ToString() : "unknown"
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            KitchenInteractionController controller = ResolveController();
            if (controller == null)
            {
                error = "KitchenInteractionController not found in the active scene. This adapter " +
                    "only works while the Kitchen scene is the active Play Mode scene.";
                return false;
            }

            if (targetId == FaucetTargetId)
            {
                controller.OnInteraction(KitchenSinkInteractionGate.FaucetInteractionId);
                error = null;
                return true;
            }

            if (targetId == ParretTargetId)
            {
                controller.OnInteraction(KitchenParretInteractionGate.ParretInteractionId);
                error = null;
                return true;
            }

            error = "KitchenQaAdapter does not own target '" + targetId + "'.";
            return false;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "KitchenQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "KitchenQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static KitchenInteractionController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<KitchenInteractionController>();
        }

        private static KitchenPuzzleState ResolvePuzzleState()
        {
            return UnityEngine.Object.FindFirstObjectByType<KitchenPuzzleState>();
        }
    }
}
#endif
