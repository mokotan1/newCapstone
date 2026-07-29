#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Hall (<see cref="SceneNames.HallPlayable"/>) QA adapter (Task 12 + Wave 2 nav).
    /// Hall_playerble.unity has no literal "kitchen" interaction id; the kitchen wing is
    /// reached via InteractionRoute id <see cref="KitchenEntryInteractionId"/> ("left" →
    /// fungus Left_Clicked). Capability ids <c>hall.nav.click-kitchen-entry</c> and target
    /// <see cref="KitchenEntryTargetIdValue"/> therefore map to
    /// <see cref="CorridorEntranceController.OnInteraction(string)"/>("left") — documented
    /// here so callers do not hunt for a non-existent "kitchen" route.
    ///
    /// No ForceSolve; missing controller → explicit failure (click → EnvironmentBlocked,
    /// assert-route → AssertionFailed).
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class HallQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string KitchenEntryTargetIdValue = "hall.kitchen-entry";

        /// <summary>
        /// Real Hall_playerble.unity InteractionRoute.interactionId for the kitchen wing.
        /// There is no "kitchen" id in-scene; kitchen entry is via "left" → Left_Clicked.
        /// </summary>
        public const string KitchenEntryInteractionId = "left";

        public const string NavClickKitchenEntryCapabilityId = "hall.nav.click-kitchen-entry";
        public const string NavProbeCapabilityId = "hall.nav.probe";
        public const string NavAssertRouteCapabilityId = "hall.nav.assert-route";
        public const string NavCaptureCapabilityId = "hall.nav.capture";

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

        /// <summary>
        /// Registers Hall nav developer capabilities and their handlers.
        /// Handlers thin-wrap <see cref="TryClick"/> and <see cref="CaptureSnapshot"/> —
        /// never force-solve. <c>hall.nav.click-kitchen-entry</c> → OnInteraction("left").
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.HallPlayable;
            var adapter = new HallQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    NavClickKitchenEntryCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    NavProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertRoute: false));

            registry.Register(
                new DeveloperQaCapability(
                    NavCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertRoute: false));

            registry.Register(
                new DeveloperQaCapability(
                    NavAssertRouteCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertRoute: true));
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            CorridorEntranceController controller = ResolveController();
            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["kitchenEntryInteractionId"] = KitchenEntryInteractionId
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != KitchenEntryTargetId)
            {
                error = "HallQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            CorridorEntranceController controller = ResolveController();
            if (controller == null)
            {
                error = "CorridorEntranceController not found in the active scene. This adapter " +
                    "only works while Hall_playerble is the active Play Mode scene.";
                return false;
            }

            // hall.kitchen-entry / hall.nav.click-kitchen-entry → OnInteraction("left")
            // (Hall_playerble has no literal "kitchen" interaction id; kitchen wing is left).
            controller.OnInteraction(KitchenEntryInteractionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "HallQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "HallQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult MapClick(HallQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "HallQaAdapter instance is required for kitchen-entry click.");
            }

            string error;
            if (adapter.TryClick(KitchenEntryTargetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Hall kitchen-entry click dispatched (OnInteraction(\"left\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True",
                        ["interactionId"] = KitchenEntryInteractionId
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "Hall kitchen-entry click blocked (Hall_playerble scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(HallQaAdapter adapter, bool assertRoute)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "HallQaAdapter instance is required for nav snapshot.");
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

            string controllerFound;
            if (!data.TryGetValue("controllerFound", out controllerFound))
            {
                controllerFound = "unknown";
            }

            if (assertRoute &&
                !string.Equals(controllerFound, bool.TrueString, StringComparison.Ordinal))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "Expected controllerFound=True but was '" + controllerFound + "'.",
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                assertRoute
                    ? "Hall nav assert-route passed."
                    : "Hall nav snapshot captured.",
                data: data);
        }

        private static CorridorEntranceController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<CorridorEntranceController>();
        }
    }
}
#endif
