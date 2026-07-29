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
    /// WifeRoom QA adapter (Wave 3 wallclock). Drives the real WifeRoom InteractionRoute id
    /// <see cref="WallclockInteractionId"/> ("wallclock") through
    /// <see cref="WifeRoomPuzzleController.OnInteraction(string)"/> — the same entry point a
    /// player click uses. No ForceSolve; missing controller → explicit failure (click →
    /// EnvironmentBlocked, assert-controller → AssertionFailed).
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class WifeRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string WallclockTargetIdValue = "wiferoom.wallclock";

        /// <summary>
        /// Real WifeRoom InteractionRoute.interactionId for the wall clock world click.
        /// </summary>
        public const string WallclockInteractionId = "wallclock";

        public const string WallclockClickCapabilityId = "wiferoom.wallclock.click";
        public const string WallclockProbeCapabilityId = "wiferoom.wallclock.probe";
        public const string WallclockAssertControllerCapabilityId = "wiferoom.wallclock.assert-controller";
        public const string WallclockCaptureCapabilityId = "wiferoom.wallclock.capture";

        private static readonly QaTargetId WallclockTargetId = QaTargetId.Create(WallclockTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { WallclockTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.WifeRoom; }
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
        /// Registers WifeRoom wallclock developer capabilities and their handlers.
        /// Handlers thin-wrap <see cref="TryClick"/> and <see cref="CaptureSnapshot"/> —
        /// never force-solve. <c>wiferoom.wallclock.click</c> → OnInteraction("wallclock").
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.WifeRoom;
            var adapter = new WifeRoomQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    WallclockClickCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    WallclockProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    WallclockCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    WallclockAssertControllerCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: true));
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            WifeRoomPuzzleController controller = ResolveController();
            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["wallclockInteractionId"] = WallclockInteractionId
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != WallclockTargetId)
            {
                error = "WifeRoomQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            WifeRoomPuzzleController controller = ResolveController();
            if (controller == null)
            {
                error = "WifeRoomPuzzleController not found in the active scene. This adapter " +
                    "only works while the WifeRoom scene is the active Play Mode scene.";
                return false;
            }

            controller.OnInteraction(WallclockInteractionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "WifeRoomQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "WifeRoomQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult MapClick(WifeRoomQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "WifeRoomQaAdapter instance is required for wallclock click.");
            }

            string error;
            if (adapter.TryClick(WallclockTargetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "WifeRoom wallclock click dispatched (OnInteraction(\"wallclock\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True",
                        ["interactionId"] = WallclockInteractionId
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "WifeRoom wallclock click blocked (WifeRoom scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(WifeRoomQaAdapter adapter, bool assertController)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "WifeRoomQaAdapter instance is required for wallclock snapshot.");
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

            if (assertController &&
                !string.Equals(controllerFound, bool.TrueString, StringComparison.Ordinal))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "Expected controllerFound=True but was '" + controllerFound + "'.",
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                assertController
                    ? "WifeRoom wallclock assert-controller passed."
                    : "WifeRoom wallclock snapshot captured.",
                data: data);
        }

        private static WifeRoomPuzzleController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<WifeRoomPuzzleController>();
        }
    }
}
#endif
