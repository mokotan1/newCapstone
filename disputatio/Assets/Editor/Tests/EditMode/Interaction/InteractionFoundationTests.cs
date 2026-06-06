using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class InteractionFoundationTests
{
    [SetUp]
    public void SetUp()
    {
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        FungusDialogueBridge.ResetForTests();
        SceneTransitionService.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        FungusDialogueBridge.ResetForTests();
        SceneTransitionService.ResetForTests();
    }

    [Test]
    public void InteractionInputGate_BlockAndUnblock_TogglesIsBlocked()
    {
        Assert.IsFalse(InteractionInputGate.IsBlocked);

        InteractionInputGate.Block("dialogue");
        Assert.IsTrue(InteractionInputGate.IsBlocked);
        Assert.AreEqual(1, InteractionInputGate.ActiveBlockCount);

        InteractionInputGate.Unblock("dialogue");
        Assert.IsFalse(InteractionInputGate.IsBlocked);
    }

    [Test]
    public void InteractionInputGate_ForceClear_RemovesAllReasons()
    {
        InteractionInputGate.Block("a");
        InteractionInputGate.Block("b");

        InteractionInputGate.ForceClear();

        Assert.IsFalse(InteractionInputGate.IsBlocked);
        Assert.AreEqual(0, InteractionInputGate.ActiveBlockCount);
    }

    [Test]
    public void SceneInteractionController_TryInteract_ReturnsFalse_WhenGateBlocked()
    {
        InteractionInputGate.Block("modal");

        Assert.IsFalse(SceneInteractionController.TryInteract("door_click"));
    }

    [Test]
    public void SceneInteractionController_TryInteract_PreventsDuplicateClicksWithinCooldown()
    {
        SceneInteractionController.DuplicateClickCooldownSeconds = 1f;
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        Assert.IsTrue(SceneInteractionController.TryInteract("pot_click"));
        Assert.IsFalse(SceneInteractionController.TryInteract("pot_click"));
    }

    [Test]
    public void SceneInteractionController_TryInteract_ReturnsFalse_WhenSceneTransitionPending()
    {
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;

        SceneTransitionService.SetTransitionPendingForTests(true, "Kitchen");

        Assert.IsFalse(SceneInteractionController.TryInteract("hall_door"));
    }

    [Test]
    public void FungusDialogueBridge_ExecuteBlockSafely_ReturnsFalse_WhenFlowchartNull()
    {
        Assert.IsFalse(FungusDialogueBridge.ExecuteBlockSafely(null, "Start"));
    }

    [Test]
    public void FungusDialogueBridge_ExecuteBlockSafely_ReturnsFalse_WhenBlockMissing()
    {
        GameObject go = new GameObject("TestFlowchart");
        Flowchart flowchart = go.AddComponent<Flowchart>();

        Assert.IsFalse(FungusDialogueBridge.ExecuteBlockSafely(flowchart, "MissingBlock"));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void FungusDialogueBridge_ExecuteBlockSafely_ReturnsTrue_ForExistingIdleBlock()
    {
        GameObject go = new GameObject("TestFlowchart");
        Flowchart flowchart = go.AddComponent<Flowchart>();
        Block block = go.AddComponent<Block>();
        block.BlockName = "Start";

        Assert.IsTrue(FungusDialogueBridge.ExecuteBlockSafely(flowchart, "Start"));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void SceneTransitionService_LoadSceneSafely_ReturnsFalse_ForEmptySceneName()
    {
        Assert.IsFalse(SceneTransitionService.LoadSceneSafely(string.Empty));
        Assert.IsFalse(SceneTransitionService.IsTransitionPending);
    }

    [Test]
    public void SceneTransitionService_LoadSceneSafely_RejectsSecondCallWhilePending()
    {
        SceneTransitionService.SetTransitionPendingForTests(true, "Kitchen");

        Assert.IsFalse(SceneTransitionService.LoadSceneSafely("Hall_playerble"));
        Assert.IsTrue(SceneTransitionService.IsTransitionPending);
        Assert.AreEqual("Kitchen", SceneTransitionService.PendingSceneName);
    }
}
