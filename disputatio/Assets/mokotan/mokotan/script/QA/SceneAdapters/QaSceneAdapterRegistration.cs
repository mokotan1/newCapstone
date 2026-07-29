#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Task 12: single entry point that builds/populates a <see cref="QaSceneRegistry"/> with all
    /// initial scene adapters (<see cref="MainMenuQaAdapter"/>, <see cref="KitchenQaAdapter"/>,
    /// <see cref="HallQaAdapter"/>, <see cref="MaidRoomQaAdapter"/>, <see cref="TutorRoomQaAdapter"/>,
    /// <see cref="StudyRoomQaAdapter"/>, <see cref="ChildRoomQaAdapter"/>,
    /// <see cref="WifeRoomQaAdapter"/>, <see cref="BedRoomQaAdapter"/>).
    ///
    /// Placement rationale: <c>Godlotto.QA.Scenes</c> (the asmdef the task description names as
    /// the home for these adapters) declares zero assembly references (see its .asmdef), so it
    /// cannot reference either <c>Godlotto.QA.Input</c> (needed for <see cref="Godlotto.QA.Input.IQaApiInteractable"/>)
    /// or Assembly-CSharp domain types (<c>MainMenu</c>, <c>KitchenInteractionController</c>, etc.)
    /// without creating a circular assembly reference. This mirrors an existing split already used
    /// by this codebase: <c>IQaProfileService</c> lives in the <c>Godlotto.QA.Profile.Abstractions</c>
    /// asmdef while its concrete implementation <c>QaProfileService</c> lives in the default
    /// assembly (Assembly-CSharp) precisely because it needs <c>PlayDataPrefsCleaner</c> (see
    /// <c>QaCommandGateway.CreateFallbackProfileService</c>'s remarks for the same rationale).
    /// Task 12's five adapters follow the identical pattern: each implements the QA-side interfaces
    /// (<see cref="IQaSceneAdapter"/>, <see cref="Godlotto.QA.Input.IQaApiInteractable"/>) but is a
    /// concrete class placed in <c>Assets/mokotan/mokotan/script/QA/SceneAdapters/</c> (no asmdef
    /// -> compiles into the default assembly), so it can call domain controllers directly with no
    /// reflection and no circular reference.
    ///
    /// This class is the single source of truth for "which adapters exist" -- both the Editor CLI
    /// path (<c>QaEditorCommandGatewayInstaller.CreateEditorGateway</c>) and the standalone
    /// development player path (<c>DeveloperModeController.CreatePlayerCommandGateway</c>) must
    /// call <see cref="BuildRegistry"/> so <c>qa_list</c>/<c>qa_run</c> and the in-game QA panel
    /// always see the exact same registered scenes/targets/presets.
    /// </summary>
    public static class QaSceneAdapterRegistration
    {
        /// <summary>Builds a fresh <see cref="QaSceneRegistry"/> with all Task 12 adapters registered.</summary>
        public static QaSceneRegistry BuildRegistry()
        {
            var registry = new QaSceneRegistry();
            RegisterAll(registry);
            return registry;
        }

        /// <summary>
        /// Registers all Task 12 adapters into <paramref name="registry"/>. Exposed separately
        /// from <see cref="BuildRegistry"/> so tests can register into a registry they already
        /// constructed (e.g. to assert idempotency across independent instances).
        /// </summary>
        public static void RegisterAll(QaSceneRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            TryRegister(registry, new MainMenuQaAdapter());
            TryRegister(registry, new KitchenQaAdapter());
            TryRegister(registry, new HallQaAdapter());
            TryRegister(registry, new MaidRoomQaAdapter());
            TryRegister(registry, new TutorRoomQaAdapter());
            TryRegister(registry, new StudyRoomQaAdapter());
            TryRegister(registry, new ChildRoomQaAdapter());
            TryRegister(registry, new WifeRoomQaAdapter());
            TryRegister(registry, new BedRoomQaAdapter());
        }

        private static void TryRegister(QaSceneRegistry registry, IQaSceneAdapter adapter)
        {
            QaSceneRegistrationResult result = registry.Register(adapter);
            if (!result.IsSuccess)
            {
                Debug.LogWarning(
                    "[QaSceneAdapterRegistration] Failed to register " + adapter.GetType().Name +
                    ": " + result.Message);
            }
        }
    }
}
#endif
