#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Thin StudyRoom developer-panel bridge (Tasks 5 + 8). Builds the same
    /// <see cref="DeveloperQaCommand"/> payloads the CLI sends
    /// (<c>interaction.invoke</c> / <c>state.capture</c> + StudyRoom capability ids)
    /// and routes them through <see cref="IDeveloperQaService"/>.
    /// Default service creation uses <see cref="DeveloperQaServiceFactory"/> (shared with CLI).
    ///
    /// Placement: default assembly under <c>SceneAdapters/</c> (Kitchen pattern) because
    /// <c>Godlotto.QA.UI</c> cannot reference Assembly-CSharp DevMode / StudyRoom types
    /// without a circular asmdef dependency.
    /// </summary>
    public static class DeveloperQaPanelBridge
    {
        public const string FamilyInteraction = "interaction";
        public const string FamilyState = "state";
        public const string NameInvoke = "invoke";
        public const string NameCapture = "capture";

        private static IDeveloperQaService service;
        private static bool disableDefaultServiceCreationForTests;

        /// <summary>Test-only: when true, <see cref="TryGetService"/> will not auto-create.</summary>
        public static bool DisableDefaultServiceCreationForTests
        {
            get { return disableDefaultServiceCreationForTests; }
            set { disableDefaultServiceCreationForTests = value; }
        }

        /// <summary>Injects a shared service (tests / host wiring).</summary>
        public static void Configure(IDeveloperQaService developerQaService)
        {
            service = developerQaService;
        }

        /// <summary>Clears injected/default service (tests).</summary>
        public static void ResetForTests()
        {
            service = null;
            disableDefaultServiceCreationForTests = false;
        }

        public static DeveloperQaCommand BuildGrantBookmarkCommand(string commandId = null)
        {
            return DeveloperQaCommand.Create(
                ResolveCommandId(commandId),
                FamilyInteraction,
                NameInvoke,
                StudyRoomQaAdapter.GrantBookmarkCapabilityId);
        }

        public static DeveloperQaCommand BuildResetCommand(string commandId = null)
        {
            return DeveloperQaCommand.Create(
                ResolveCommandId(commandId),
                FamilyInteraction,
                NameInvoke,
                StudyRoomQaAdapter.ResetCapabilityId);
        }

        public static DeveloperQaCommand BuildProbeCommand(string commandId = null)
        {
            return DeveloperQaCommand.Create(
                ResolveCommandId(commandId),
                FamilyState,
                NameCapture,
                StudyRoomQaAdapter.ProbeCapabilityId);
        }

        public static Task<DeveloperQaResult> ExecuteAsync(
            DeveloperQaCommand command,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetService(out IDeveloperQaService resolved))
            {
                return Task.FromResult(new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "DeveloperQaService unavailable."));
            }

            return resolved.ExecuteAsync(command, cancellationToken);
        }

        /// <summary>
        /// Panel grant path. Returns false when the service cannot be resolved so callers
        /// can fall back to <see cref="StudyRoomPuzzleDevTool"/> / controller.
        /// </summary>
        public static bool TryGrantBookmark(out DeveloperQaResult result)
        {
            return TryExecute(BuildGrantBookmarkCommand(), out result);
        }

        /// <summary>
        /// Panel reset path. Returns false when the service cannot be resolved so callers
        /// can fall back to the legacy controller path.
        /// </summary>
        public static bool TryReset(out DeveloperQaResult result)
        {
            return TryExecute(BuildResetCommand(), out result);
        }

        /// <summary>
        /// Panel probe path (optional display). Returns false when the service cannot be resolved.
        /// </summary>
        public static bool TryProbe(out DeveloperQaResult result)
        {
            return TryExecute(BuildProbeCommand(), out result);
        }

        private static bool TryExecute(DeveloperQaCommand command, out DeveloperQaResult result)
        {
            result = null;
            if (command == null || !TryGetService(out IDeveloperQaService resolved))
            {
                return false;
            }

            try
            {
                result = resolved.ExecuteAsync(command, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                return result != null;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        private static bool TryGetService(out IDeveloperQaService resolved)
        {
            if (service != null)
            {
                resolved = service;
                return true;
            }

            if (disableDefaultServiceCreationForTests)
            {
                resolved = null;
                return false;
            }

            // Shared factory: StudyRoom capabilities registered in one place (Task 8).
            // Editor installer may already have Configure'd a production service with evidence.
            service = DeveloperQaServiceFactory.Create();
            resolved = service;
            return true;
        }

        private static string ResolveCommandId(string commandId)
        {
            return string.IsNullOrWhiteSpace(commandId)
                ? Guid.NewGuid().ToString("N")
                : commandId;
        }
    }
}
#endif
