#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fungus;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Kitchen bottle→key exit-contract capabilities (HaveMaidKey / maid-room-key).
/// No ForceSolve and no fake HaveMaidKey for PASS.
/// </summary>
public class KitchenExitCapabilityTests
{
    private static readonly string[] ExpectedExitIds =
    {
        "kitchen.sink.preset.before-bottle-fill",
        "kitchen.sink.fill-bottle",
        "kitchen.key.probe",
        "kitchen.key.click",
        "kitchen.exit.assert"
    };

    private GameObject _flowchartObject;

    [TearDown]
    public void TearDown()
    {
        if (_flowchartObject != null)
        {
            Object.DestroyImmediate(_flowchartObject);
            _flowchartObject = null;
        }
    }

    [Test]
    public void RegisterCapabilities_ListsExitContractIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        CollectionAssert.IsSubsetOf(ExpectedExitIds, ids);
    }

    [Test]
    public async Task Describe_UnknownKitchenExitCap_ReturnsMissingCapability()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "kitchen.exit.missing"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.MissingCapability, result.Code);
    }

    [Test]
    public async Task Invoke_FillBottle_WithoutKitchenScene_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "kitchen.sink.fill-bottle"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_KeyClick_WithoutMaidKey_ReturnsEnvironmentBlocked()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "kitchen.key.click"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
    }

    [Test]
    public async Task Invoke_ExitAssert_WhenHaveMaidKeyFalse_ReturnsAssertionFailed()
    {
        _flowchartObject = new GameObject("Variablemanager");
        Flowchart flowchart = _flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, FungusVariableKeys.HaveMaidKey, false);

        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "interaction", "invoke", "kitchen.exit.assert"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.AssertionFailed, result.Code, result.Message);
        Assert.IsNotNull(result.Data);
        Assert.AreEqual("False", result.Data["haveMaidKey"]);
    }

    [Test]
    public async Task Describe_ExitAssert_ReturnsOk()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create("c1", "capability", "describe", "kitchen.exit.assert"),
            CancellationToken.None);
        Assert.AreEqual(DeveloperQaResultCode.Ok, result.Code, result.Message);
    }

    private static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
#endif
