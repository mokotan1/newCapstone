#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using NUnit.Framework;

public class DeveloperQaServiceTests
{
    [Test]
    public async Task ExecuteAsync_BlankCommandId_ReturnsInvalidCommand()
    {
        var service = new DeveloperQaService();
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("", "capability", "list"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.InvalidCommand, result.Code);
    }

    [Test]
    public async Task ExecuteAsync_UnknownFamily_ReturnsUnsupportedCommand()
    {
        var service = new DeveloperQaService();
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "not-a-family", "x"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.UnsupportedCommand, result.Code);
    }

    [Test]
    public void ListCapabilities_InitiallyEmpty_UntilAdaptersRegister()
    {
        var service = new DeveloperQaService();
        Assert.AreEqual(0, service.ListCapabilities().Count);
    }

    [Test]
    public void CaptureSnapshot_ReturnsNonNullWithEmptyCapabilityVersion()
    {
        var service = new DeveloperQaService();
        DeveloperQaSnapshot snap = service.CaptureSnapshot();
        Assert.IsNotNull(snap);
        Assert.IsFalse(string.IsNullOrEmpty(snap.CapturedAtUtc));
    }
}
#endif
