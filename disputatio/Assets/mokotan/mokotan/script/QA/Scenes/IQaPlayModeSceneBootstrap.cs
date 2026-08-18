#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.QA.Scenes
{
    /// <summary>Result code for <see cref="IQaPlayModeSceneBootstrap.EnsureReadyAsync"/>.</summary>
    public enum QaPlayModeBootstrapResultCode
    {
        Success,

        /// <summary>
        /// Play Mode / scene readiness could not be achieved (missing scene, timeout, cancelled).
        /// Callers should surface this as an environment BLOCKED outcome, not a product FAIL.
        /// </summary>
        Blocked
    }

    /// <summary>Immutable result of a Play Mode / scene bootstrap attempt.</summary>
    public sealed class QaPlayModeBootstrapResult
    {
        public QaPlayModeBootstrapResultCode Code { get; }

        public string Message { get; }

        public bool EnteredPlayMode { get; }

        private QaPlayModeBootstrapResult(
            QaPlayModeBootstrapResultCode code,
            string message,
            bool enteredPlayMode)
        {
            Code = code;
            Message = message ?? string.Empty;
            EnteredPlayMode = enteredPlayMode;
        }

        public bool IsSuccess
        {
            get { return Code == QaPlayModeBootstrapResultCode.Success; }
        }

        public static QaPlayModeBootstrapResult Success(string message = null, bool enteredPlayMode = false)
        {
            return new QaPlayModeBootstrapResult(
                QaPlayModeBootstrapResultCode.Success,
                message ?? "Play Mode scene ready.",
                enteredPlayMode);
        }

        public static QaPlayModeBootstrapResult Blocked(string message, bool enteredPlayMode = false)
        {
            return new QaPlayModeBootstrapResult(
                QaPlayModeBootstrapResultCode.Blocked,
                message ?? "BLOCKED: Play Mode scene bootstrap failed.",
                enteredPlayMode);
        }
    }

    /// <summary>
    /// Ensures the Editor/player is in Play Mode with the scenario's declared scene active
    /// before presets or interactions run. Editor implementations must avoid domain reload
    /// while a <c>qa_run</c> Task is in flight. Implementations that entered Play Mode must
    /// undo that in <see cref="RestoreIfOwned"/>.
    /// </summary>
    public interface IQaPlayModeSceneBootstrap
    {
        Task<QaPlayModeBootstrapResult> EnsureReadyAsync(
            string sceneName,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Stops Play Mode when this bootstrap entered it for the current run. Safe no-op
        /// when ownership was not taken or Play Mode was already active beforehand.
        /// </summary>
        void RestoreIfOwned();
    }
}
#endif
