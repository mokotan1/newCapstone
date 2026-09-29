using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class OpeningMentionControllerTests
{
    GameObject root;
    OpeningMentionController controller;

    [SetUp]
    public void SetUp()
    {
        OpeningMentionController.ResetStateForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("OpeningMentionTestRoot");
        controller = root.AddComponent<OpeningMentionController>();
    }

    [TearDown]
    public void TearDown()
    {
        OpeningMentionController.ResetStateForTests();
        foreach (var runner in Object.FindObjectsByType<DeferredClickCleanup>(FindObjectsSortMode.None))
            Object.DestroyImmediate(runner.gameObject);
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
        string loaded = null;
        OpeningMentionController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        controller.SimulateBellSequenceStartForTests();
        controller.SimulateIsCallForTests(true);

        controller.OnFenceClicked();

        Assert.IsNull(loaded);
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
    public void CompleteBell_MarksCallSoFenceCanTransition()
    {
        string requestedScene = null;
        OpeningMentionController.SceneLoadHandlerForTests = sceneName =>
        {
            requestedScene = sceneName;
            return true;
        };

        controller.CompleteBellMarkingCallForTests();
        Assert.IsTrue(controller.IsCallForTests);

        controller.RequestOpenSceneTransitionForTests();

        Assert.AreEqual("Opening_Mention _open", requestedScene);
    }

    [Test]
    public void CompleteBell_ClearsBellSequenceGate()
    {
        controller.SimulateBellSequenceStartForTests();
        controller.CompleteBellMarkingCallForTests();

        Assert.IsFalse(controller.IsBellSequenceActiveForTests);
        Assert.IsFalse(InteractionInputGate.IsBlocked);
        Assert.IsTrue(controller.IsCallForTests);
    }
}
