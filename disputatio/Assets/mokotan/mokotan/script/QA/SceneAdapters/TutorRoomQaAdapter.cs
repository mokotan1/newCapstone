#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// TutorRoom QA adapter (Task 12). Registration-only stub: unlike Kitchen/Hall/MaidRoom, no
    /// dedicated C# interaction controller exists for TutorRoom at all (no
    /// <c>TutorRoomInteractionController</c>/<c>TutorRoomPuzzleController</c> subclass was found
    /// in <c>Assets/godlotto/Script/Interaction</c>), so there is no public API boundary at all to
    /// call for the "cheshire quiz" flow -- it is presumably driven purely by Fungus/Inspector
    /// data on the scene. This adapter is registered purely so <c>tutorroom.cheshire-quiz</c>
    /// validates against the schema/registry and is discoverable via qa_list;
    /// <see cref="TryClick"/> fails explicitly with this gap instead of fabricating a controller
    /// or an interaction id that has no source-code evidence behind it.
    ///
    /// Placement/assembly note: see <see cref="QaSceneAdapterRegistration"/> remarks.
    /// </summary>
    public sealed class TutorRoomQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string QuizInputTargetIdValue = "tutorroom.quiz-input";

        private static readonly QaTargetId QuizInputTargetId = QaTargetId.Create(QuizInputTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { QuizInputTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.TutorRoom; }
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
                ["gap"] = "Task 12 registration-only stub; no TutorRoom C# interaction controller exists to inspect."
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
            return "Gap (Task 12): TutorRoomQaAdapter is a registration-only stub for target '" + targetId +
                "' -- no TutorRoom interaction controller/interactionId exists in source yet " +
                "(Cheshire quiz flow appears to be pure Fungus/Inspector data). Follow-up task " +
                "must add a real TutorRoom controller boundary before this can be wired.";
        }
    }
}
#endif
