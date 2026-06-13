using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

[TestFixture]
public class FaucetKeyReleaseControllerTests
{
    GameObject root;
    Flowchart flowchart;
    FaucetKeyReleaseController controller;
    string executedBlockName;

    [SetUp]
    public void SetUp()
    {
        executedBlockName = null;
        FaucetKeyReleaseController.ExecuteBlockHandlerForTests = blockName => executedBlockName = blockName;

        root = new GameObject("FaucetKeyReleaseTest");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<FaucetKeyReleaseController>();

        AddBooleanVariable(flowchart, "FaucetClicked", false);

        SetPrivateField(controller, "targetFlowchart", flowchart);
        SetPrivateField(controller, "faucetBoolName", "FaucetClicked");
        SetPrivateField(controller, "keySpawnBlockName", "addKey");
        SetPrivateField(controller, "delaySeconds", 0f);
    }

    [TearDown]
    public void TearDown()
    {
        FaucetKeyReleaseController.ExecuteBlockHandlerForTests = null;
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void TryTriggerKeySpawn_WhenFaucetClickedFalse_DoesNotExecuteAddKey()
    {
        controller.TryTriggerKeySpawnForTests();

        Assert.IsNull(executedBlockName);
        Assert.IsFalse(controller.HasTriggeredForTests);
    }

    [Test]
    public void TryTriggerKeySpawn_WhenFaucetClickedTrue_ExecutesAddKey()
    {
        flowchart.SetBooleanVariable("FaucetClicked", true);

        controller.TryTriggerKeySpawnForTests();

        Assert.AreEqual("addKey", executedBlockName);
        Assert.IsTrue(controller.HasTriggeredForTests);
    }

    [Test]
    public void TryTriggerKeySpawn_WhenDirectKeyTargetExists_ActivatesKeyWithoutExecutingAddKey()
    {
        var keyObject = new GameObject("MaidRoomKey");
        keyObject.SetActive(false);
        SetPrivateField(controller, "keyObject", keyObject);
        flowchart.SetBooleanVariable("FaucetClicked", true);

        controller.TryTriggerKeySpawnForTests();

        Assert.IsTrue(keyObject.activeSelf);
        Assert.IsNull(executedBlockName);
        Assert.IsTrue(controller.HasTriggeredForTests);

        Object.DestroyImmediate(keyObject);
    }

    [Test]
    public void TryTriggerKeySpawn_WhenDirectKeyReferenceMissing_FindsInactiveKeyByName()
    {
        var keyObject = new GameObject("MaidRoomKey");
        keyObject.SetActive(false);
        SetPrivateField(controller, "keyObject", null);
        SetPrivateField(controller, "keyObjectName", "MaidRoomKey");
        flowchart.SetBooleanVariable("FaucetClicked", true);

        controller.TryTriggerKeySpawnForTests();

        Assert.IsTrue(keyObject.activeSelf);
        Assert.IsNull(executedBlockName);
        Assert.IsTrue(controller.HasTriggeredForTests);

        Object.DestroyImmediate(keyObject);
    }

    [Test]
    public void TryTriggerKeySpawn_WhenHaveMaidKeyAlreadyTrue_LogsPreSpawnWarning()
    {
        AddBooleanVariable(flowchart, FungusVariableKeys.HaveMaidKey, true);
        flowchart.SetBooleanVariable("FaucetClicked", true);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
            @"\[FaucetKeyReleaseController\] addKey will SetActive MaidRoomKey but ItemPickup\.Start may suppress"));

        controller.TryTriggerKeySpawnForTests();

        Assert.AreEqual("addKey", executedBlockName);
    }

    [Test]
    public void KitchenPuzzleState_ApplyBlockCompletion_Faucet_SetsFaucetClickedForController()
    {
        var puzzleRoot = new GameObject("PuzzleState");
        var puzzleState = puzzleRoot.AddComponent<KitchenPuzzleState>();
        puzzleState.SetFlowchartForTests(flowchart);
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: false, bottleDragged: false);

        puzzleState.ApplyBlockCompletion(KitchenSinkInteractionGate.FaucetBlockName);

        Assert.IsTrue(puzzleState.FaucetClicked);
        Assert.IsTrue(flowchart.GetBooleanVariable("FaucetClicked"));

        controller.TryTriggerKeySpawnForTests();
        Assert.AreEqual("addKey", executedBlockName);

        Object.DestroyImmediate(puzzleRoot);
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
