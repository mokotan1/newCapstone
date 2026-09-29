using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class HallPlayableControllerTests
{
    GameObject root;
    HallPlayableController controller;

    [SetUp]
    public void SetUp()
    {
        HallPlayableController.ResetStateForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        root = new GameObject("HallPlayableTests");
        controller = root.AddComponent<HallPlayableController>();
    }

    [TearDown]
    public void TearDown()
    {
        HallPlayableController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Right_LoadsHallRight()
    {
        string loaded = null;
        HallPlayableController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };

        controller.OnInteraction(HallPlayableController.InteractionRight);
        Assert.AreEqual(SceneNames.HallRight, loaded);
    }

    [Test]
    public void Left_LoadsHallLeft()
    {
        string loaded = null;
        HallPlayableController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };

        controller.OnInteraction(HallPlayableController.InteractionLeft);
        Assert.AreEqual("Hall_Left", loaded);
    }

    [Test]
    public void Hub_DoesNotLoadHallAnimate()
    {
        Assert.IsFalse(controller.ShouldLoadHallAnimateFromHub());
    }
}
