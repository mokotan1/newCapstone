#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Developer;
using NUnit.Framework;

public class DeveloperQaCapabilityRegistryTests
{
    [Test]
    public void Register_ThenList_ReturnsCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        registry.Register(new DeveloperQaCapability(
            "studyroom.mirror.probe",
            "StudyRoom",
            DeveloperQaCapabilityKind.Probe,
            "{}",
            "{hasBookmarkMirror:bool}"));
        Assert.AreEqual(1, registry.List().Count);
        Assert.AreEqual("1", registry.Version);
    }

    [Test]
    public async Task ExecuteAsync_UnknownCapabilityInvoke_ReturnsMissingCapability()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry());
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c1",
                "interaction",
                "invoke",
                "studyroom.mirror.place-bookmark"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
        Assert.AreEqual("studyroom.mirror.place-bookmark", result.MissingCapabilityId);
        Assert.IsFalse(string.IsNullOrEmpty(result.CheckpointId));
        Assert.IsTrue(result.Data.ContainsKey("current_capabilities"));
    }

    [Test]
    public async Task ExecuteAsync_DescribeKnownCapability_ReturnsOkWithData()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        registry.Register(new DeveloperQaCapability(
            "studyroom.mirror.probe",
            "StudyRoom",
            DeveloperQaCapabilityKind.Probe,
            "{}",
            "{hasBookmarkMirror:bool}"));
        var service = new DeveloperQaService(registry);

        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "studyroom.mirror.probe"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code);
        Assert.AreEqual("StudyRoom", result.Data["scene_id"]);
        Assert.AreEqual("{}", result.Data["input_schema"]);
    }

    [Test]
    public async Task ExecuteAsync_DescribeUnknownCapability_ReturnsMissingCapability()
    {
        var service = new DeveloperQaService(new DeveloperQaCapabilityRegistry());
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "missing.cap"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
        Assert.AreEqual("missing.cap", result.MissingCapabilityId);
        Assert.IsFalse(string.IsNullOrEmpty(result.CheckpointId));
    }
}
#endif
