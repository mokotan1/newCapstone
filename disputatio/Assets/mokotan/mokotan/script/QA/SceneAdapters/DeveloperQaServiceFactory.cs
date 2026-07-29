#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Developer;
using Godlotto.QA.Evidence;
using Godlotto.QA.Input;
using Godlotto.QA.Profile;
using Godlotto.QA.Scenes;
using UnityEngine.EventSystems;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Shared production wiring for <see cref="DeveloperQaService"/> (Task 8 + Wave 1/2).
    /// Panel bridge and CLI bridge both create services through this factory so multi-room
    /// capabilities (StudyRoom, Kitchen, MainMenu, MaidRoom, Hall, ChildRoom, WifeRoom,
    /// BedRoom) and optional profile/evidence are registered exactly once in one place.
    /// When <see cref="EventSystem.current"/> is present, injects a Kitchen-aware RealInput
    /// driver so <c>interaction.pointer</c> steps can exercise the EventSystem path (§6.2).
    /// </summary>
    public static class DeveloperQaServiceFactory
    {
        /// <summary>
        /// Creates a service with StudyRoom, Kitchen, MainMenu, MaidRoom, Hall, ChildRoom,
        /// WifeRoom, and BedRoom capabilities registered.
        /// Pass <paramref name="evidenceRecorder"/> (e.g. Editor <c>docs/qa/runs</c> recorder)
        /// for production evidence.capture; omit in unit tests that inject their own recorder.
        /// </summary>
        public static IDeveloperQaService Create(
            IQaProfileService profileService = null,
            IQaEvidenceRecorder evidenceRecorder = null)
        {
            var registry = new DeveloperQaCapabilityRegistry();
            StudyRoomQaAdapter.RegisterCapabilities(registry);
            KitchenQaAdapter.RegisterCapabilities(registry);
            MainMenuQaAdapter.RegisterCapabilities(registry);
            MaidRoomQaAdapter.RegisterCapabilities(registry);
            HallQaAdapter.RegisterCapabilities(registry);
            ChildRoomQaAdapter.RegisterCapabilities(registry);
            WifeRoomQaAdapter.RegisterCapabilities(registry);
            BedRoomQaAdapter.RegisterCapabilities(registry);
            return new DeveloperQaService(
                registry,
                profileService,
                evidenceRecorder,
                TryCreateRealInputDriver());
        }

        /// <summary>
        /// Builds a RealInput driver when EventSystem is available; otherwise <c>null</c>
        /// (pointer steps then report EnvironmentBlocked — never fake Ok).
        /// </summary>
        public static IQaInputDriver TryCreateRealInputDriver()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return null;
            }

            return new QaEventSystemInputDriver(eventSystem, ResolveTargetGameObject);
        }

        private static UnityEngine.GameObject ResolveTargetGameObject(QaTargetId targetId)
        {
            // Kitchen faucet first; future room adapters register here.
            UnityEngine.GameObject kitchen = KitchenQaTargetResolver.TryResolve(targetId);
            return kitchen;
        }
    }
}
#endif
