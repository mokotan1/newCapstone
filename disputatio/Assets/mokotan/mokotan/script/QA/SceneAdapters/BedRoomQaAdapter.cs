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
    /// BedRoom QA adapter (Wave 3 book). Drives the real BedRoom InteractionRoute id
    /// <see cref="BookInteractionId"/> ("book") through
    /// <see cref="BedRoomInteractionController.OnInteraction(string)"/> — the clearest concrete
    /// world click (design also mentions panel). No ForceSolve; missing controller → explicit
    /// failure (click → EnvironmentBlocked, assert-controller → AssertionFailed).
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class BedRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string BookTargetIdValue = "bedroom.book";

        /// <summary>
        /// Real BedRoom InteractionRoute.interactionId for the book world click.
        /// </summary>
        public const string BookInteractionId = "book";

        public const string BookClickCapabilityId = "bedroom.book.click";
        public const string BookProbeCapabilityId = "bedroom.book.probe";
        public const string BookAssertControllerCapabilityId = "bedroom.book.assert-controller";
        public const string BookCaptureCapabilityId = "bedroom.book.capture";

        private static readonly QaTargetId BookTargetId = QaTargetId.Create(BookTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { BookTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.BedRoom; }
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
        /// Registers BedRoom book developer capabilities and their handlers.
        /// Handlers thin-wrap <see cref="TryClick"/> and <see cref="CaptureSnapshot"/> —
        /// never force-solve. <c>bedroom.book.click</c> → OnInteraction("book").
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.BedRoom;
            var adapter = new BedRoomQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    BookClickCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    BookProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    BookCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{controllerFound:bool}"),
                _ => MapSnapshot(adapter, assertController: false));

            registry.Register(
                new DeveloperQaCapability(
                    BookAssertControllerCapabilityId,
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
            BedRoomInteractionController controller = ResolveController();
            var values = new Dictionary<string, string>
            {
                ["controllerFound"] = (controller != null).ToString(),
                ["bookInteractionId"] = BookInteractionId
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != BookTargetId)
            {
                error = "BedRoomQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            BedRoomInteractionController controller = ResolveController();
            if (controller == null)
            {
                error = "BedRoomInteractionController not found in the active scene. This adapter " +
                    "only works while the BedRoom scene is the active Play Mode scene.";
                return false;
            }

            controller.OnInteraction(BookInteractionId);
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "BedRoomQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "BedRoomQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult MapClick(BedRoomQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "BedRoomQaAdapter instance is required for book click.");
            }

            string error;
            if (adapter.TryClick(BookTargetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "BedRoom book click dispatched (OnInteraction(\"book\")).",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True",
                        ["interactionId"] = BookInteractionId
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "BedRoom book click blocked (BedRoom scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(BedRoomQaAdapter adapter, bool assertController)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "BedRoomQaAdapter instance is required for book snapshot.");
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
                    ? "BedRoom book assert-controller passed."
                    : "BedRoom book snapshot captured.",
                data: data);
        }

        private static BedRoomInteractionController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<BedRoomInteractionController>();
        }
    }
}
#endif
