using System.Collections;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class HallPlayableControllerTests
{
    GameObject root;
    HallPlayableController controller;
    string loaded;

    [SetUp]
    public void SetUp()
    {
        HallPlayableController.ResetStateForTests();
        HallGlobalStateHost.EnsureInstance();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        loaded = null;

        HallPlayableController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        HallPlayableController.FadeThenHandlerForTests = (_, __, onComplete) => onComplete?.Invoke();
        HallPlayableController.SayHandlerForTests = (_, onComplete) => onComplete?.Invoke();
        HallPlayableController.SequenceRunnerForTests = sequence =>
        {
            RunToEnd(sequence);
        };

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
        controller.OnInteraction(HallPlayableController.InteractionRight);
        Assert.AreEqual(SceneNames.HallRight, loaded);
    }

    [Test]
    public void Left_LoadsHallLeft()
    {
        controller.OnInteraction(HallPlayableController.InteractionLeft);
        Assert.AreEqual(SceneNames.HallLeft, loaded);
    }

    [Test]
    public void Hub_DoesNotLoadHallAnimate()
    {
        Assert.IsFalse(controller.ShouldLoadHallAnimateFromHub());
    }

    [Test]
    public void StairYes_LoadsSecondFloorMainHall()
    {
        HallPlayableController.MenuHandlerForTests = (_, onChoice) => onChoice(0);
        controller.OnInteraction(HallPlayableController.InteractionStair);
        Assert.AreEqual(SceneNames.SecondFloorMainHall, loaded);
    }

    [Test]
    public void BasementWithoutKey_DoesNotLoad()
    {
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedBasementKey, false);
        HallPlayableController.MenuHandlerForTests = (_, onChoice) => onChoice(0);
        controller.OnInteraction(HallPlayableController.InteractionBasement);
        Assert.IsNull(loaded);
    }

    [Test]
    public void BasementWithKeyYes_LoadsBetaEnd()
    {
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedBasementKey, true);
        HallPlayableController.MenuHandlerForTests = (_, onChoice) => onChoice(0);
        controller.OnInteraction(HallPlayableController.InteractionBasement);
        Assert.AreEqual(SceneNames.BetaEnd, loaded);
    }

    [Test]
    public void UnlockYes_LoadsBetaEnd()
    {
        HallPlayableController.MenuHandlerForTests = (_, onChoice) => onChoice(0);
        controller.OnInteraction(HallPlayableController.InteractionUnlock);
        Assert.AreEqual(SceneNames.BetaEnd, loaded);
    }

    [Test]
    public void StairNo_DoesNotLoad()
    {
        HallPlayableController.MenuHandlerForTests = (_, onChoice) => onChoice(1);
        controller.OnInteraction(HallPlayableController.InteractionStair);
        Assert.IsNull(loaded);
        Assert.IsFalse(controller.IsBusyForTests);
    }

    static void RunToEnd(IEnumerator sequence)
    {
        while (sequence.MoveNext())
        {
            if (sequence.Current is IEnumerator nested)
                RunToEnd(nested);
        }
    }
}
