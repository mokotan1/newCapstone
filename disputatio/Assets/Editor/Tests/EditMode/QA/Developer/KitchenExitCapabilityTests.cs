#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

/// <summary>
/// Kitchen bottle→key exit-contract capabilities (HaveMaidKey / maid-room-key).
/// No ForceSolve and no fake HaveMaidKey for PASS.
/// </summary>
public class KitchenExitCapabilityTests
{
    private static readonly string[] ExpectedExitIds =
    {
        "kitchen.sink.preset.before-bottle-fill",
        KitchenQaAdapter.SinkFillBottleCapabilityId,
        "kitchen.key.probe",
        "kitchen.key.click",
        "kitchen.exit.assert"
    };

    private GameObject _flowchartObject;
    private GameObject _puzzleObject;
    private GameObject _inventoryObject;

    [TearDown]
    public void TearDown()
    {
        if (_flowchartObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_flowchartObject);
            _flowchartObject = null;
        }

        if (_puzzleObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_puzzleObject);
            _puzzleObject = null;
        }

        if (_inventoryObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_inventoryObject);
            _inventoryObject = null;
        }
    }

    [Test]
    public void RegisterCapabilities_ListsExitContractIds()
    {
        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var ids = registry.List().Select(c => c.Id).ToArray();
        foreach (string id in ExpectedExitIds)
        {
            bool found = false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Missing exit capability: " + id + "; have=[" + string.Join(",", ids) + "]");
        }
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
            DeveloperQaCommand.Create("c1", "interaction", "invoke", KitchenQaAdapter.SinkFillBottleCapabilityId),
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

    [Test]
    public async Task Invoke_BeforeBottleFill_WithoutInventory_ReturnsEnvironmentBlockedInEditMode()
    {
        _puzzleObject = new GameObject("KitchenPuzzleState");
        _puzzleObject.AddComponent<KitchenPuzzleState>();
        Assert.IsNull(InventoryManager.Instance);

        var registry = new DeveloperQaCapabilityRegistry();
        KitchenQaAdapter.RegisterCapabilities(registry);
        var service = new DeveloperQaService(registry);
        DeveloperQaResult result = await service.ExecuteAsync(
            DeveloperQaCommand.Create(
                "c1",
                "preset",
                "apply",
                "kitchen.sink.preset.before-bottle-fill"),
            CancellationToken.None);

        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code, result.Message);
        StringAssert.Contains("InventoryManager", result.Message);
    }

    // Note: Play Mode bootstrap of Inventory/Variablemanager is verified via unity-cli
    // against the Kitchen scene (DontDestroyOnLoad is illegal in EditMode).


    private static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
#endif
