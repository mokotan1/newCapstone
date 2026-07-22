#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// MainMenuScene QA adapter (Task 12). Wraps <see cref="MainMenu.OnStartButton"/> — already a
    /// public API on the real domain component, exactly the entry point a player click invokes —
    /// so a QA scenario can exercise the real "새 게임" reset path
    /// (<see cref="PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings"/>) end-to-end
    /// without private reflection or any scene edits.
    ///
    /// Placement note: this class intentionally lives in the default assembly (Assembly-CSharp),
    /// NOT inside the <c>Godlotto.QA.Scenes</c> asmdef folder. <c>Godlotto.QA.Scenes</c> has zero
    /// assembly references (see its .asmdef) and therefore cannot reference either
    /// <c>Godlotto.QA.Input</c> (needed for <see cref="IQaApiInteractable"/>) or Assembly-CSharp
    /// domain types (<see cref="MainMenu"/>) without a circular reference. This mirrors the
    /// existing <c>QaProfileService</c> (concrete, Assembly-CSharp) /
    /// <c>IQaProfileService</c> (abstract, <c>Godlotto.QA.Profile.Abstractions</c> asmdef) split
    /// already used by this codebase — see <c>QaCommandGateway.CreateFallbackProfileService</c>'s
    /// remarks for the same rationale. <see cref="Godlotto.QA.SceneAdapters.QaSceneAdapterRegistration"/>
    /// documents this decision for all six Task 12 adapters.
    /// </summary>
    public sealed class MainMenuQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string StartButtonTargetIdValue = "mainmenu.start-button";

        private static readonly QaTargetId StartButtonTargetId = QaTargetId.Create(StartButtonTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId> { StartButtonTargetId };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds = Array.Empty<string>();

        public string SceneName
        {
            get { return SceneNames.MainMenu; }
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
        /// No presets are declared yet -- MainMenuScene starts from a well-defined state on
        /// load, so the scenario relies on the click step itself instead of a setup preset.
        /// </summary>
        public QaScenePresetResult ApplyPreset(string presetId)
        {
            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            var values = new Dictionary<string, string>
            {
                ["mainMenuFound"] = (ResolveMainMenu() != null).ToString()
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            if (targetId != StartButtonTargetId)
            {
                error = "MainMenuQaAdapter does not own target '" + targetId + "'.";
                return false;
            }

            MainMenu mainMenu = ResolveMainMenu();
            if (mainMenu == null)
            {
                error = "MainMenu instance not found in the active scene. This adapter only " +
                    "drives the real MainMenu.OnStartButton() entry point, so it requires " +
                    "MainMenuScene to be the active Play Mode scene when qa_run executes.";
                return false;
            }

            mainMenu.OnStartButton();
            error = null;
            return true;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "MainMenuQaAdapter does not support drag interactions.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "MainMenuQaAdapter does not support key interactions.";
            return false;
        }

        private static MainMenu ResolveMainMenu()
        {
            return UnityEngine.Object.FindFirstObjectByType<MainMenu>();
        }
    }
}
#endif
