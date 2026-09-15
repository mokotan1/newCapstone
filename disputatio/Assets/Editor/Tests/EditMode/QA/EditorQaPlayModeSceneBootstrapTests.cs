#if UNITY_EDITOR
using Godlotto.QA.EditorCli;
using NUnit.Framework;

[TestFixture]
public sealed class EditorQaPlayModeSceneBootstrapTests
{
    [Test]
    public void IsPlayModeReady_WhenEditorIsPlaying_ReturnsTrueEvenWhilePlayingFlagIsSet()
    {
        bool ready = EditorQaPlayModeSceneBootstrap.IsPlayModeReady(
            isPlaying: true,
            isPlayingOrWillChangePlaymode: true);

        Assert.IsTrue(
            ready,
            "Unity keeps isPlayingOrWillChangePlaymode true during Play Mode; readiness must not wait for it to become false.");
    }
}
#endif
