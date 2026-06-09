using NUnit.Framework;

public class ModalGamePauseTests
{
    [Test]
    public void ResolveTimeScaleOnClose_ReturnsZeroWhileAnyModalRemainsOpen()
    {
        Assert.AreEqual(0f, ModalGamePause.ResolveTimeScaleOnClose(settingsOpen: true, dialogueLogOpen: false));
        Assert.AreEqual(0f, ModalGamePause.ResolveTimeScaleOnClose(settingsOpen: false, dialogueLogOpen: true));
        Assert.AreEqual(0f, ModalGamePause.ResolveTimeScaleOnClose(settingsOpen: true, dialogueLogOpen: true));
    }

    [Test]
    public void ResolveTimeScaleOnClose_ReturnsOneWhenNoModalRemainsOpen()
    {
        Assert.AreEqual(1f, ModalGamePause.ResolveTimeScaleOnClose(settingsOpen: false, dialogueLogOpen: false));
    }

    [Test]
    public void ShouldEndWorldInputBlocker_IsFalseWhileAnyModalRemainsOpen()
    {
        Assert.IsFalse(ModalGamePause.ShouldEndWorldInputBlocker(settingsOpen: true, dialogueLogOpen: false));
        Assert.IsFalse(ModalGamePause.ShouldEndWorldInputBlocker(settingsOpen: false, dialogueLogOpen: true));
        Assert.IsFalse(ModalGamePause.ShouldEndWorldInputBlocker(settingsOpen: true, dialogueLogOpen: true));
    }

    [Test]
    public void ShouldEndWorldInputBlocker_IsTrueWhenNoModalRemainsOpen()
    {
        Assert.IsTrue(ModalGamePause.ShouldEndWorldInputBlocker(settingsOpen: false, dialogueLogOpen: false));
    }
}
