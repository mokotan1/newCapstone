#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Developer;
using Godlotto.QA.Evidence;
using Godlotto.QA.Profile;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Shared production wiring for <see cref="DeveloperQaService"/> (Task 8).
    /// Panel bridge and CLI bridge both create services through this factory so StudyRoom
    /// capabilities (and optional profile/evidence) are registered exactly once in one place.
    /// </summary>
    public static class DeveloperQaServiceFactory
    {
        /// <summary>
        /// Creates a service with StudyRoom capabilities registered.
        /// Pass <paramref name="evidenceRecorder"/> (e.g. Editor <c>docs/qa/runs</c> recorder)
        /// for production evidence.capture; omit in unit tests that inject their own recorder.
        /// </summary>
        public static IDeveloperQaService Create(
            IQaProfileService profileService = null,
            IQaEvidenceRecorder evidenceRecorder = null)
        {
            var registry = new DeveloperQaCapabilityRegistry();
            StudyRoomQaAdapter.RegisterCapabilities(registry);
            return new DeveloperQaService(registry, profileService, evidenceRecorder);
        }
    }
}
#endif
