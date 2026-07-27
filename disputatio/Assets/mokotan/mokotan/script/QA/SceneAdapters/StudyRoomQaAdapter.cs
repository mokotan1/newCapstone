#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// StudyRoom diary-mirror QA adapter (Tasks 4 + 6). Developer capabilities reuse
    /// <see cref="StudyRoomPuzzleDevTool"/> (grant/reset/probe),
    /// <see cref="StudyRoomMirrorQaHelpers"/> (assert/capture), and the real
    /// <see cref="FilterCardBookDropZone"/> / <see cref="StudyRoomDiaryMirrorPuzzleController"/>
    /// drop → evaluate → <see cref="StudyRoomMirrorPuzzleSuccessRouter"/> path for place-bookmark.
    /// Force-solve is intentionally not exposed as a PASS path.
    /// </summary>
    public sealed class StudyRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string GrantBookmarkCapabilityId = "studyroom.mirror.grant-bookmark";
        public const string ResetCapabilityId = "studyroom.mirror.reset";
        public const string ProbeCapabilityId = "studyroom.mirror.probe";
        public const string AssertSolvedCapabilityId = "studyroom.mirror.assert-solved";
        public const string CaptureCapabilityId = "studyroom.mirror.capture";
        public const string BeforePlacementCapabilityId = "studyroom.mirror.preset.before-placement";
        public const string PlaceBookmarkCapabilityId = "studyroom.mirror.place-bookmark";

        /// <summary>Short preset id accepted by <see cref="ApplyPreset"/> (Kitchen-style).</summary>
        public const string BeforePlacementPresetId = "before-placement";

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            Array.Empty<QaTargetId>();

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds =
            new[] { BeforePlacementPresetId };

        public string SceneName
        {
            get { return SceneNames.StudyRoom; }
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
        /// Registers StudyRoom mirror developer capabilities and their handlers.
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.StudyRoom;

            registry.Register(
                new DeveloperQaCapability(
                    GrantBookmarkCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{succeeded:bool,itemName:string}"),
                HandleGrantBookmark);

            registry.Register(
                new DeveloperQaCapability(
                    ResetCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Recovery,
                    "{}",
                    "{reset:bool}"),
                HandleReset);

            registry.Register(
                new DeveloperQaCapability(
                    ProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{diarySolved:bool,haveTutorKey:bool,hasBookmarkMirror:bool}"),
                HandleProbe);

            registry.Register(
                new DeveloperQaCapability(
                    AssertSolvedCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{diarySolved:bool}"),
                HandleAssertSolved);

            registry.Register(
                new DeveloperQaCapability(
                    CaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{diarySolved:bool,haveTutorKey:bool,hasBookmarkMirror:bool,hasPlacement:bool}"),
                HandleCapture);

            registry.Register(
                new DeveloperQaCapability(
                    BeforePlacementCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Preset,
                    "{}",
                    "{diarySolved:bool,haveTutorKey:bool}"),
                HandleBeforePlacement);

            registry.Register(
                new DeveloperQaCapability(
                    PlaceBookmarkCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{placed:bool,diarySolved:bool,hasPlacement:bool}"),
                HandlePlaceBookmark);
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            if (!IsBeforePlacementPresetId(presetId))
            {
                return QaScenePresetResult.UnknownPreset(presetId);
            }

            DeveloperQaResult result = HandleBeforePlacement(null);
            if (result.Code == DeveloperQaResultCode.Ok)
            {
                return QaScenePresetResult.Success(result.Message);
            }

            return QaScenePresetResult.Failed(
                string.IsNullOrEmpty(result.Message)
                    ? "StudyRoom before-placement preset failed."
                    : result.Message);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            StudyRoomPuzzleDebugInfo info = StudyRoomPuzzleDevTool.CaptureDebugInfo();
            return QaSceneSnapshot.Create(
                SceneName,
                DateTime.UtcNow,
                StudyRoomMirrorQaHelpers.BuildProbeData(info));
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != null &&
                string.Equals(targetId.Value, PlaceBookmarkCapabilityId, StringComparison.Ordinal))
            {
                DeveloperQaResult placeResult = HandlePlaceBookmark(null);
                if (placeResult.Code == DeveloperQaResultCode.Ok)
                {
                    error = null;
                    return true;
                }

                error = string.IsNullOrEmpty(placeResult.Message)
                    ? "StudyRoom place-bookmark failed."
                    : placeResult.Message;
                return false;
            }

            error = "StudyRoomQaAdapter does not own click target '" + targetId + "'.";
            return false;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "StudyRoomQaAdapter does not support drag for '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "StudyRoomQaAdapter does not support key input for '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult HandleGrantBookmark(DeveloperQaCommand _)
        {
            if (!StudyRoomPuzzleDevTool.CanUse)
            {
                return EnvironmentBlocked(
                    "StudyRoom grant-bookmark requires developer mode (CanUse=false).");
            }

            DeveloperModeItemSelectionGrantResult grant = StudyRoomPuzzleDevTool.GrantBookmarkMirror();
            if (grant == null || !grant.Succeeded)
            {
                string reason = grant != null && !string.IsNullOrEmpty(grant.FailureReason)
                    ? grant.FailureReason
                    : "BookmarkMirror grant failed.";
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    reason,
                    data: new Dictionary<string, string>
                    {
                        ["succeeded"] = "False",
                        ["itemName"] = StudyRoomPuzzleDevTool.BookmarkMirrorItemName
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "BookmarkMirror granted.",
                data: new Dictionary<string, string>
                {
                    ["succeeded"] = "True",
                    ["itemName"] = StudyRoomPuzzleDevTool.BookmarkMirrorItemName
                });
        }

        private static DeveloperQaResult HandleReset(DeveloperQaCommand _)
        {
            if (!StudyRoomPuzzleDevTool.CanUse)
            {
                return EnvironmentBlocked(
                    "StudyRoom mirror reset requires developer mode (CanUse=false).");
            }

            bool reset = StudyRoomPuzzleDevTool.ResetPuzzle();
            if (!reset)
            {
                return EnvironmentBlocked(
                    "StudyRoom mirror reset failed: Flowchart(Variablemanager) unavailable or gate blocked.");
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "StudyRoom mirror puzzle reset.",
                data: new Dictionary<string, string>
                {
                    ["reset"] = "True",
                    [StudyRoomMirrorQaHelpers.DataKeyDiarySolved] = "False",
                    [StudyRoomMirrorQaHelpers.DataKeyHaveTutorKey] = "False"
                });
        }

        private static DeveloperQaResult HandleBeforePlacement(DeveloperQaCommand _)
        {
            if (!StudyRoomPuzzleDevTool.CanUse)
            {
                return EnvironmentBlocked(
                    "StudyRoom before-placement requires developer mode (CanUse=false).");
            }

            bool reset = StudyRoomPuzzleDevTool.ResetPuzzle();
            if (!reset)
            {
                return EnvironmentBlocked(
                    "StudyRoom before-placement failed: Flowchart(Variablemanager) unavailable or scene gate blocked.");
            }

            StudyRoomPuzzleDebugInfo info = StudyRoomPuzzleDevTool.CaptureDebugInfo();
            if (info.DiarySolved || info.HaveTutorKey)
            {
                return EnvironmentBlocked(
                    "StudyRoom before-placement could not establish unsolved baseline " +
                    "(DiarySolved/HaveTutorKey still true).");
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "StudyRoom before-placement baseline ready (unsolved).",
                data: new Dictionary<string, string>
                {
                    [StudyRoomMirrorQaHelpers.DataKeyDiarySolved] = "False",
                    [StudyRoomMirrorQaHelpers.DataKeyHaveTutorKey] = "False",
                    [StudyRoomMirrorQaHelpers.DataKeyHasBookmarkMirror] = info.HasBookmarkMirror.ToString(),
                    [StudyRoomMirrorQaHelpers.DataKeyHasPlacement] = info.HasPlacement.ToString()
                });
        }

        private static DeveloperQaResult HandlePlaceBookmark(DeveloperQaCommand _)
        {
            if (!StudyRoomPuzzleDevTool.CanUse)
            {
                return EnvironmentBlocked(
                    "StudyRoom place-bookmark requires developer mode (CanUse=false).");
            }

            FilterCardBookDropZone dropZone = ResolveDiaryMirrorDropZone();
            StudyRoomDiaryMirrorPuzzleController controller =
                dropZone != null
                    ? dropZone.diaryMirrorPuzzleController
                    : UnityEngine.Object.FindFirstObjectByType<StudyRoomDiaryMirrorPuzzleController>();

            if (dropZone == null || controller == null)
            {
                return EnvironmentBlocked(
                    "StudyRoom place-bookmark blocked: FilterCardBookDropZone / " +
                    "StudyRoomDiaryMirrorPuzzleController not found in the active scene.");
            }

            Item bookmark = ResolveBookmarkItem(dropZone);
            if (bookmark == null)
            {
                return EnvironmentBlocked(
                    "StudyRoom place-bookmark blocked: BookmarkMirror item asset unavailable.");
            }

            if (!StudyRoomPuzzleDevTool.InventoryHasBookmarkMirror() &&
                !InventoryContainsItem(bookmark))
            {
                return EnvironmentBlocked(
                    "StudyRoom place-bookmark blocked: BookmarkMirror not in inventory " +
                    "(grant-bookmark first).");
            }

            // Real drop route — same OnDrop body the player UI uses. Never ForceSolve.
            Item previousDragged = InventorySlot.draggedItem;
            bool previousConsume = dropZone.consumeItemOnDrop;
            try
            {
                InventorySlot.draggedItem = bookmark;
                dropZone.consumeItemOnDrop = false;
                dropZone.OnDrop(CreatePointerEventData());
            }
            finally
            {
                dropZone.consumeItemOnDrop = previousConsume;
                if (InventorySlot.draggedItem == bookmark)
                    InventorySlot.ClearDragState();
                else if (previousDragged != null && InventorySlot.draggedItem == null)
                    InventorySlot.draggedItem = previousDragged;
            }

            if (!controller.TrySnapToConfiguredSolutionAndEvaluateForQa())
            {
                return EnvironmentBlocked(
                    "StudyRoom place-bookmark blocked: mirror card was not activated by drop " +
                    "(NotifyMirrorCardActivated path incomplete).");
            }

            StudyRoomPuzzleDebugInfo info = StudyRoomPuzzleDevTool.CaptureDebugInfo();
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "BookmarkMirror placed via real drop/success route.",
                data: new Dictionary<string, string>
                {
                    ["placed"] = "True",
                    [StudyRoomMirrorQaHelpers.DataKeyDiarySolved] = info.DiarySolved.ToString(),
                    [StudyRoomMirrorQaHelpers.DataKeyHasPlacement] = info.HasPlacement.ToString(),
                    [StudyRoomMirrorQaHelpers.DataKeyHaveTutorKey] = info.HaveTutorKey.ToString(),
                    ["usedForceSolve"] = "False"
                });
        }

        private static DeveloperQaResult HandleProbe(DeveloperQaCommand _)
        {
            return StudyRoomMirrorQaHelpers.EvaluateProbe();
        }

        private static DeveloperQaResult HandleAssertSolved(DeveloperQaCommand _)
        {
            return StudyRoomMirrorQaHelpers.EvaluateAssertSolved();
        }

        private static DeveloperQaResult HandleCapture(DeveloperQaCommand _)
        {
            return StudyRoomMirrorQaHelpers.EvaluateCapture();
        }

        private static FilterCardBookDropZone ResolveDiaryMirrorDropZone()
        {
            FilterCardBookDropZone[] zones =
                UnityEngine.Object.FindObjectsByType<FilterCardBookDropZone>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (zones == null || zones.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < zones.Length; i++)
            {
                FilterCardBookDropZone zone = zones[i];
                if (zone != null && zone.diaryMirrorPuzzleController != null)
                {
                    return zone;
                }
            }

            return zones[0];
        }

        private static Item ResolveBookmarkItem(FilterCardBookDropZone dropZone)
        {
            if (dropZone != null && dropZone.requiredItem != null)
            {
                return dropZone.requiredItem;
            }

            return StudyRoomPuzzleDevTool.ResolveBookmarkMirrorItem();
        }

        private static bool InventoryContainsItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null || inventory.Items == null)
            {
                return false;
            }

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                if (inventory.Items[i] == item)
                {
                    return true;
                }
            }

            return false;
        }

        private static PointerEventData CreatePointerEventData()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            return new PointerEventData(eventSystem);
        }

        private static bool IsBeforePlacementPresetId(string presetId)
        {
            return string.Equals(presetId, BeforePlacementPresetId, StringComparison.Ordinal)
                || string.Equals(presetId, BeforePlacementCapabilityId, StringComparison.Ordinal);
        }

        private static DeveloperQaResult EnvironmentBlocked(string message)
        {
            return new DeveloperQaResult(DeveloperQaResultCode.EnvironmentBlocked, message);
        }
    }
}
#endif
