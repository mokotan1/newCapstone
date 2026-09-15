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

    [Test]
    public void IsServerReachable_OnlyHttpSuccessFamily()
    {
        Assert.That(SettingsCheshirePreviewGate.IsServerReachable(0), Is.False);
        Assert.That(SettingsCheshirePreviewGate.IsServerReachable(200), Is.True);
        Assert.That(SettingsCheshirePreviewGate.IsServerReachable(503), Is.False);
    }

    [Test]
    public void ShouldApplyDefaultDevice_AfterReachableLoopbackHealth_AndNotYetApplied()
    {
        Assert.That(
            SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(
                playerDisabled: false,
                requiresLoopback: true,
                alreadyApplied: false,
                serverReachable: true),
            Is.True);
    }

    [Test]
    public void ShouldApplyDefaultDevice_SkipsWhenRemoteOrAlreadyAppliedOrUnreachable()
    {
        Assert.That(
            SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(false, false, false, true),
            Is.False);
        Assert.That(
            SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(false, true, true, true),
            Is.False);
        Assert.That(
            SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(false, true, false, false),
            Is.False);
        Assert.That(
            SettingsCheshirePreviewGate.ShouldApplyDefaultDevice(true, true, false, true),
            Is.False);
    }

    [Test]
    public void ShouldKeepPolling_UntilLoopbackModelIsReady()
    {
        Assert.That(SettingsCheshirePreviewGate.ShouldKeepPolling(false, true, false), Is.True);
        Assert.That(SettingsCheshirePreviewGate.ShouldKeepPolling(false, true, true), Is.False);
        Assert.That(SettingsCheshirePreviewGate.ShouldKeepPolling(false, false, false), Is.False);
        Assert.That(SettingsCheshirePreviewGate.ShouldKeepPolling(true, true, false), Is.False);
    }

    [Test]
    public void PanelOwnsIndependentStatusPoll_EmbeddedDefersToPreview()
    {
        Assert.That(SettingsCheshirePreviewGate.PanelOwnsIndependentStatusPoll(true), Is.False);
        Assert.That(SettingsCheshirePreviewGate.PanelOwnsIndependentStatusPoll(false), Is.True);
    }
}
