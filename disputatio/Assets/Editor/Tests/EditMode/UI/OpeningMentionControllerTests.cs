using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OpeningMentionControllerTests
{
    GameObject root;
    OpeningMentionController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        OpeningMentionController.ResetStateForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("OpeningMentionTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<OpeningMentionController>();

        var flowchartField = typeof(OpeningMentionController).GetField(
            "flowchart",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        flowchartField?.SetValue(controller, flowchart);
    }

    [TearDown]
    public void TearDown()
    {
        OpeningMentionController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void OnBellClicked_SecondClickWhileSequenceActive_IsIgnored()
    {
        controller.SimulateBellSequenceStartForTests();

        controller.OnBellClicked();

        Assert.IsTrue(controller.IsBellSequenceActiveForTests);
    }

    [Test]
    public void OnFenceClicked_DuringBellSequence_IsIgnored()
    {
        controller.SimulateBellSequenceStartForTests();

        controller.OnFenceClicked();

        Assert.IsFalse(controller.IsPendingFenceSceneTransitionForTests);
    }

    [Test]
    public void EndBellSequence_ClearsBellGateSoFenceIsNotBlockedByBell()
    {
        controller.SimulateBellSequenceStartForTests();
        Assert.IsTrue(InteractionInputGate.IsBlocked);

        controller.SimulateBellSequenceEndForTests();

        Assert.IsFalse(controller.IsBellSequenceActiveForTests);
        Assert.IsFalse(InteractionInputGate.IsBlocked);
    }

    [Test]
    public void OnBlockEnd_FenceBlockWithPendingTransition_RequestsSceneLoad()
    {
        string requestedScene = null;
        OpeningMentionController.SceneLoadHandlerForTests = sceneName =>
        {
            requestedScene = sceneName;
            return true;
        };

        controller.SimulateFenceTransitionPendingForTests(true);

        var block = root.AddComponent<Block>();
        block.BlockName = "Fance_Clicked";

        controller.InvokeBlockEndForTests(block);

        Assert.AreEqual("Opening_Mention _open", requestedScene);
        Assert.IsFalse(controller.IsPendingFenceSceneTransitionForTests);
    }

    [Test]
    public void OnBlockEnd_BellBlock_ClearsBellSequenceGate()
    {
        controller.SimulateBellSequenceStartForTests();

        var block = root.AddComponent<Block>();
        block.BlockName = "Bell_Clicked";

        controller.InvokeBlockEndForTests(block);

        Assert.IsFalse(controller.IsBellSequenceActiveForTests);
        Assert.IsFalse(InteractionInputGate.IsBlocked);
    }
}
