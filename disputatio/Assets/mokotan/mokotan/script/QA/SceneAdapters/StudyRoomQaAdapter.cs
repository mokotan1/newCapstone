#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// StudyRoom diary-mirror QA adapter (Task 4). Scene targets/presets stay minimal;
    /// developer capabilities reuse <see cref="StudyRoomPuzzleDevTool"/> (grant/reset/probe)
    /// and <see cref="StudyRoomMirrorQaHelpers"/> (assert/capture). Force-solve is intentionally
    /// not exposed as a PASS path.
    /// </summary>
    public sealed class StudyRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string GrantBookmarkCapabilityId = "studyroom.mirror.grant-bookmark";
        public const string ResetCapabilityId = "studyroom.mirror.reset";
        public const string ProbeCapabilityId = "studyroom.mirror.probe";
        public const string AssertSolvedCapabilityId = "studyroom.mirror.assert-solved";
        public const string CaptureCapabilityId = "studyroom.mirror.capture";

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            Array.Empty<QaTargetId>();

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds =
            Array.Empty<string>();

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
        /// Registers the five StudyRoom mirror developer capabilities and their handlers.
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
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
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
            error = "StudyRoomQaAdapter does not own click target '" + targetId +
                "' (place-bookmark lands in Task 6).";
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

        private static DeveloperQaResult EnvironmentBlocked(string message)
        {
            return new DeveloperQaResult(DeveloperQaResultCode.EnvironmentBlocked, message);
        }
    }
}
#endif
