#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Fungus;
using Godlotto.QA.Developer;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Pure StudyRoom diary-mirror probe/assert helpers that accept an optional
    /// <see cref="Flowchart"/> so EditMode tests can avoid DeveloperModeController gates
    /// for read-only evaluation paths.
    /// </summary>
    public static class StudyRoomMirrorQaHelpers
    {
        public const string DataKeyIsStudyRoomScene = "isStudyRoomScene";
        public const string DataKeyHasBookmarkMirror = "hasBookmarkMirror";
        public const string DataKeyDiarySolved = "diarySolved";
        public const string DataKeyHaveTutorKey = "haveTutorKey";
        public const string DataKeyHasPlacement = "hasPlacement";

        public static Dictionary<string, string> BuildProbeData(StudyRoomPuzzleDebugInfo info)
        {
            return new Dictionary<string, string>
            {
                [DataKeyIsStudyRoomScene] = info.IsStudyRoomScene.ToString(),
                [DataKeyHasBookmarkMirror] = info.HasBookmarkMirror.ToString(),
                [DataKeyDiarySolved] = info.DiarySolved.ToString(),
                [DataKeyHaveTutorKey] = info.HaveTutorKey.ToString(),
                [DataKeyHasPlacement] = info.HasPlacement.ToString()
            };
        }

        public static DeveloperQaResult EvaluateProbe(Flowchart flowchart = null, string activeSceneName = null)
        {
            StudyRoomPuzzleDebugInfo info =
                StudyRoomPuzzleDevTool.CaptureDebugInfo(flowchart, activeSceneName);
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "StudyRoom mirror probe captured.",
                data: BuildProbeData(info));
        }

        public static DeveloperQaResult EvaluateCapture(Flowchart flowchart = null, string activeSceneName = null)
        {
            StudyRoomPuzzleDebugInfo info =
                StudyRoomPuzzleDevTool.CaptureDebugInfo(flowchart, activeSceneName);
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "StudyRoom mirror evidence snapshot captured.",
                data: BuildProbeData(info));
        }

        public static DeveloperQaResult EvaluateAssertSolved(Flowchart flowchart = null, string activeSceneName = null)
        {
            StudyRoomPuzzleDebugInfo info =
                StudyRoomPuzzleDevTool.CaptureDebugInfo(flowchart, activeSceneName);
            Dictionary<string, string> data = BuildProbeData(info);

            if (!info.DiarySolved)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "StudyRoom diary mirror is not solved (DiarySolved=false).",
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "StudyRoom diary mirror assert-solved passed.",
                data: data);
        }
    }
}
#endif
