using NUnit.Framework;

[TestFixture]
public class SettingsCheshirePreviewGateTests
{
    [Test]
    public void IdleStatus_LoopbackNotReady_IsConnecting()
    {
        Assert.That(
            SettingsCheshirePreviewGate.IdleStatusKey(false, true, false),
            Is.EqualTo("AiSettingsConnecting"));
    }

    [Test]
    public void IdleStatus_LoopbackReady_IsPreviewReady()
    {
        Assert.That(
            SettingsCheshirePreviewGate.IdleStatusKey(false, true, true),
            Is.EqualTo("SettingsPreviewReady"));
    }

    [Test]
    public void IdleStatus_RemoteUrl_IsReadyWithoutLocalModel()
    {
        Assert.That(
            SettingsCheshirePreviewGate.IdleStatusKey(false, false, false),
            Is.EqualTo("SettingsPreviewReady"));
    }

    [Test]
    public void IdleStatus_PlayerDisabled_UsesDisabledKey()
    {
        Assert.That(
            SettingsCheshirePreviewGate.IdleStatusKey(true, true, true),
            Is.EqualTo("LocalAiDisabled"));
    }

    [Test]
    public void BlockedAsk_LoopbackNotReady_UsesNotReadyKey()
    {
        Assert.That(
            SettingsCheshirePreviewGate.BlockedAskKey(false, true, false),
            Is.EqualTo("LocalAiNotReady"));
        Assert.That(SettingsCheshirePreviewGate.CanAsk(false, true, false), Is.False);
    }

    [Test]
    public void BlockedAsk_WhenSendable_ReturnsNull()
    {
        Assert.That(SettingsCheshirePreviewGate.BlockedAskKey(false, false, false), Is.Null);
        Assert.That(SettingsCheshirePreviewGate.CanAsk(false, true, true), Is.True);
    }
}
