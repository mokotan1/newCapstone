#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// TutorRoom QA adapter. <c>tutorroom.quiz-input</c> opens the real
    /// <see cref="QuizInputHandler"/> panel (same path Fungus uses via
    /// <c>TutorChatbot.ActivateQuizInputField</c>). Missing handler → explicit failure.
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

        /// <summary>EditMode hook: inject or clear the QuizInputHandler resolver.</summary>
        internal static Func<QuizInputHandler> QuizInputHandlerResolverForTests { get; set; }

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

        internal static void ResetQuizInputHandlerResolverForTests()
        {
            QuizInputHandlerResolverForTests = null;
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            QuizInputHandler handler = ResolveQuizInputHandler();
            var values = new Dictionary<string, string>
            {
                ["quizInputFound"] = (handler != null).ToString(),
                ["quizInputPanelActive"] = handler != null && handler.IsQuizInputPanelActive
                    ? bool.TrueString
                    : bool.FalseString
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != QuizInputTargetId)
            {
                error = "TutorRoomQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            QuizInputHandler handler = ResolveQuizInputHandler();
            if (handler == null)
            {
                error =
                    "QuizInputHandler not found in the active scene. This adapter only works " +
                    "while '" + SceneNames.TutorRoom + "' is the active Play Mode scene with a wired quiz panel.";
                return false;
            }

            handler.ActivateQuizInputField();
            if (!handler.IsQuizInputPanelActive)
            {
                error =
                    "QuizInputHandler.ActivateQuizInputField ran but the quiz input panel is still inactive " +
                    "(inputPanel reference may be missing in the TutorRoom scene).";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "TutorRoomQaAdapter does not support drag for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "TutorRoomQaAdapter does not support key input for target '" + targetId + "'.";
            return false;
        }

        private static QuizInputHandler ResolveQuizInputHandler()
        {
            if (QuizInputHandlerResolverForTests != null)
            {
                return QuizInputHandlerResolverForTests();
            }

            return UnityEngine.Object.FindFirstObjectByType<QuizInputHandler>(FindObjectsInactive.Include);
        }
    }
}
#endif
