#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// Records which live fields were actually observed when a <see cref="QaDriverSnapshot"/>
    /// was captured. Missing observations must not satisfy assertions such as
    /// <c>inputUnlocked</c> or <c>noNewConsoleError</c>.
    /// </summary>
    public sealed class QaSnapshotObservations
    {
        public const string SceneNameObservedKey = "Observed.SceneName";
        public const string InputGateObservedKey = "Observed.InputGate";
        public const string ConsoleErrorObservedKey = "Observed.ConsoleErrorCount";

        public bool SceneNameObserved { get; }

        public bool InputGateObserved { get; }

        public bool ConsoleErrorObserved { get; }

        public QaSnapshotObservations(
            bool sceneNameObserved = true,
            bool inputGateObserved = true,
            bool consoleErrorObserved = true)
        {
            SceneNameObserved = sceneNameObserved;
            InputGateObserved = inputGateObserved;
            ConsoleErrorObserved = consoleErrorObserved;
        }

        public static QaSnapshotObservations AllObserved()
        {
            return new QaSnapshotObservations(true, true, true);
        }

        public static QaSnapshotObservations NoneObserved()
        {
            return new QaSnapshotObservations(false, false, false);
        }
    }
}
#endif
