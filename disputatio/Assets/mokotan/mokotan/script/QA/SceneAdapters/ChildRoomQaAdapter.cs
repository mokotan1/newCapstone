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
    /// ChildRoom QA adapter (Wave 3 seals). Drives the real ChildRoom InteractionRoute id
    /// <see cref="Seal5InteractionId"/> ("seal5") through
    /// <see cref="ChildRoomPuzzleController.OnInteraction(string)"/> — the same entry point a
    /// player click uses. No ForceSolve; missing controller → explicit failure (click →
    /// EnvironmentBlocked, assert-controller → AssertionFailed).
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class ChildRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string Seal5TargetIdValue = "childroom.seals.seal5";

        /// <summary>
        /// Real ChildRoom InteractionRoute.interactionId for seal inventory unlock path.
        /// </summary>
        public const string Seal5InteractionId = "seal5";

        public const string SealsClickSeal5CapabilityId = "childroom.seals.click-seal5";
        public const string SealsProbeCapabilityId = "childroom.seals.probe";
        public const string SealsAssertControllerCapabilityId = "childroom.seals.assert-controller";
        public const string SealsCaptureCapabilityId = "childroom.seals.capture";

        private static readonly QaTargetId Seal5TargetId = QaTargetId.Create(Seal5TargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { Seal5TargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.ChildRoom; }
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
        /// Registers ChildRoom seals developer capabilities and their handlers.
        /// Handlers thin-wrap <see cref="TryClick"/> and <see cref="CaptureSnapshot"/> —
        /// never force-solve. <c>childroom.seals.click-seal5</c> → OnInteraction("seal5").
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.ChildRoom;
            var adapter = new ChildRoomQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    SealsClickSeal5CapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    SealsProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    SealsCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    SealsAssertControllerCapabilityId,
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
            ChildRoomPuzzleController controller = ResolveController();
            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["seal5InteractionId"] = Seal5InteractionId
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != Seal5TargetId)
            {
                error = "ChildRoomQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            ChildRoomPuzzleController controller = ResolveController();
            if (controller == null)
            {
                error = "ChildRoomPuzzleController not found in the active scene. This adapter " +
                    "only works while the ChildRoom scene is the active Play Mode scene.";
                return false;
            }

            controller.OnInteraction(Seal5InteractionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "ChildRoomQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "ChildRoomQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult MapClick(ChildRoomQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "ChildRoomQaAdapter instance is required for seal5 click.");
            }

            string error;
            if (adapter.TryClick(Seal5TargetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "ChildRoom seal5 click dispatched (OnInteraction(\"seal5\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True",
                        ["interactionId"] = Seal5InteractionId
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "ChildRoom seal5 click blocked (ChildRoom scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(ChildRoomQaAdapter adapter, bool assertController)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "ChildRoomQaAdapter instance is required for seals snapshot.");
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
                    ? "ChildRoom seals assert-controller passed."
                    : "ChildRoom seals snapshot captured.",
                data: data);
        }

        private static ChildRoomPuzzleController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<ChildRoomPuzzleController>();
        }
    }
}
#endif
