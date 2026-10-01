using System.Collections;
using System.Collections.Generic;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class SecondFloorRightControllerTests
{
    GameObject root;
    SecondFloorRightController controller;
    string loaded;
    bool wentBack;
    int bgmIndex = -1;
    readonly List<string> said = new List<string>();
    readonly List<string[]> menus = new List<string[]>();
    readonly List<(float alpha, float duration)> fades = new List<(float, float)>();
    readonly List<float> waits = new List<float>();
    readonly List<(string method, float a, float b)> exposure = new List<(string, float, float)>();
    int footsteps;
    int menuChoice;

    [SetUp]
    public void SetUp()
    {
        SecondFloorRightController.ResetStateForTests();
        HallGlobalStateHost.EnsureInstance();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        loaded = null;
        wentBack = false;
        bgmIndex = -1;
        said.Clear();
        menus.Clear();
        fades.Clear();
        waits.Clear();
        exposure.Clear();
        footsteps = 0;
        menuChoice = 0;

        SecondFloorRightController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        SecondFloorRightController.BackHandlerForTests = () => wentBack = true;
        SecondFloorRightController.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fades.Add((alpha, duration));
            onComplete?.Invoke();
        };
        SecondFloorRightController.SayHandlerForTests = (line, onComplete) =>
        {
            said.Add(line);
            onComplete?.Invoke();
        };
        SecondFloorRightController.WaitHandlerForTests = seconds => waits.Add(seconds);
        SecondFloorRightController.AudioHandlerForTests = () => footsteps++;
        SecondFloorRightController.BgmHandlerForTests = index => bgmIndex = index;
        SecondFloorRightController.ExposureHandlerForTests = (method, a, b) => exposure.Add((method, a, b));
        SecondFloorRightController.MenuHandlerForTests = (options, onChoice) =>
        {
            menus.Add(options);
            onChoice(menuChoice);
        };
        SecondFloorRightController.SequenceRunnerForTests = RunToEnd;

        root = new GameObject("SecondFloorRightTests");
        controller = root.AddComponent<SecondFloorRightController>();
    }

    [TearDown]
    public void TearDown()
    {
        SecondFloorRightController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void MainHall_EnterPlaysBgmAndElectricEffects()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, true);
        controller.RunEnter();
        Assert.AreEqual(SecondFloorRightController.MainHallBgmIndex, bgmIndex);
        Assert.AreEqual(("TriggerFlashEffect", 0f, 0f), exposure[0]);
        Assert.AreEqual(("SetVignetteToZero", 0f, 0f), exposure[1]);
        Assert.AreEqual((SecondFloorRightController.EnterFadeTargetAlpha, SecondFloorRightController.EnterFadeDurationSeconds), fades[0]);

        exposure.Clear();
        fades.Clear();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, false);
        controller.RunEnter();
        Assert.AreEqual(0, exposure.Count);
        Assert.AreEqual((SecondFloorRightController.EnterFadeTargetAlpha, SecondFloorRightController.EnterFadeDurationSeconds), fades[0]);
    }

    [Test]
    public void MainHall_LeftLoadsSecondFloorLeft()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        controller.OnInteraction(SecondFloorRightController.InteractionLeft);
        Assert.AreEqual(SceneNames.SecondFloorLeft, loaded);
        Assert.AreEqual(1, footsteps);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void MainHall_RightLoadsSecondFloorRight()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        controller.OnInteraction(SecondFloorRightController.InteractionRight);
        Assert.AreEqual(SceneNames.SecondFloorRight, loaded);
    }

    [Test]
    public void MainHall_JesusShowsOriginalLine()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        controller.OnInteraction(SecondFloorRightController.InteractionJesus);
        Assert.AreEqual(SecondFloorRightController.JesusLine, said[0]);
        Assert.AreEqual(SecondFloorRightController.InspectionWaitSeconds, waits[0]);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void MainHall_BackYesLoadsHallPlayable()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        menuChoice = 0;
        controller.OnBack();
        Assert.AreEqual(SecondFloorRightController.DownstairsPrompt, said[0]);
        Assert.AreEqual(SecondFloorRightController.DownstairsYes, menus[0][0]);
        Assert.AreEqual(SecondFloorRightController.DownstairsNo, menus[0][1]);
        Assert.AreEqual(SceneNames.HallPlayable, loaded);
        Assert.IsFalse(wentBack);
        Assert.AreEqual(1, footsteps);
    }

    [Test]
    public void MainHall_BackNoReleasesInput()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        menuChoice = 1;
        controller.OnBack();
        Assert.IsNull(loaded);
        Assert.IsFalse(wentBack);
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void Right_FrontLoadsCross()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.Right);
        controller.OnInteraction(SecondFloorRightController.InteractionFront);
        Assert.AreEqual(SceneNames.SecondFloorRightCross, loaded);
        Assert.AreEqual(SecondFloorRightController.PostFadeWaitSeconds, waits[0]);
    }

    [Test]
    public void Right_PhotosShowOriginalLines()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.Right);
        controller.OnInteraction(SecondFloorRightController.InteractionPhoto1);
        controller.OnInteraction(SecondFloorRightController.InteractionPhoto2);
        Assert.AreEqual(SecondFloorRightController.WeddingPhotoLine, said[0]);
        Assert.AreEqual(SecondFloorRightController.LastSupperLine, said[1]);
    }

    [Test]
    public void Right_BackYesUsesBackRoute()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.Right);
        menuChoice = 0;
        controller.OnBack();
        Assert.AreEqual(SecondFloorRightController.ReturnPrompt, said[0]);
        Assert.AreEqual(SecondFloorRightController.ReturnYes, menus[0][0]);
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
    }

    [Test]
    public void Right_BackNoReleasesInput()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.Right);
        GameObject menu = new GameObject("MenuDialog");
        menu.SetActive(true);
        controller.AssignMenuForTests(menu);
        IEnumerator pending = null;
        SecondFloorRightController.SequenceRunnerForTests = sequence => pending = sequence;
        controller.OnBack();
        Assert.IsNotNull(pending);
        Assert.IsTrue(controller.IsBusyForTests);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);
        controller.ConfirmNo();
        Assert.IsFalse(menu.activeSelf);
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Object.DestroyImmediate(menu);
    }

    [Test]
    public void Cross_RightLoadsWifeEntrance()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.RightCross);
        controller.OnInteraction(SecondFloorRightController.InteractionRight);
        Assert.AreEqual(SceneNames.WifeEntrance, loaded);
    }

    [Test]
    public void Cross_LeftLoadsBedEntrance()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.RightCross);
        controller.OnInteraction(SecondFloorRightController.InteractionLeft);
        Assert.AreEqual(SceneNames.BedEntrance, loaded);
    }

    [Test]
    public void Cross_PreviousChoiceUsesBackRoute()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.RightCross);
        controller.OnPreviousChoice();
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
        Assert.AreEqual(1, footsteps);
    }

    [Test]
    public void Cross_NoReleasesInput()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.RightCross);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);
        controller.ConfirmNo();
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsFalse(controller.IsBusyForTests);
    }

    [Test]
    public void Cross_HallwayChoiceLoadsHallway()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.RightCross);
        controller.OnHallwayChoice();
        Assert.AreEqual(SceneNames.SecondFloorHallwayRight, loaded);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void Hallway_FrontLoadsMainHall()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.HallwayRight);
        controller.OnInteraction(SecondFloorRightController.InteractionFront);
        Assert.AreEqual(SceneNames.SecondFloorMainHall, loaded);
    }

    [Test]
    public void Hallway_PhotosShowOriginalLines()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.HallwayRight);
        controller.OnInteraction(SecondFloorRightController.InteractionPhoto1);
        controller.OnInteraction(SecondFloorRightController.InteractionPhoto2);
        Assert.AreEqual(SecondFloorRightController.WeddingPhotoLine, said[0]);
        Assert.AreEqual(SecondFloorRightController.LastSupperLine, said[1]);
    }

    [Test]
    public void Hallway_BackYesUsesBackRoute()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.HallwayRight);
        menuChoice = 0;
        controller.OnBack();
        Assert.IsTrue(wentBack);
        Assert.AreEqual(SecondFloorRightController.ReturnPrompt, said[0]);
    }

    [Test]
    public void Hallway_DoesNotLoadHallAnimate()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.HallwayRight);
        controller.OnInteraction(SecondFloorRightController.InteractionFront);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void WorldClick_ModalBlocksDispatch()
    {
        controller.AssignRouteForTests(SecondFloorRightRoute.MainHall);
        GameObject panel = new GameObject("ModalPanel");
        GameObject world = new GameObject("World");
        object owner = new object();
        ModalInputGate.Begin(owner, panel, blocksHud: true, blocksWorld: true);
        try
        {
            Assert.IsFalse(controller.TryDispatchForTests(world, Vector2.zero, SecondFloorRightController.InteractionLeft));
            Assert.IsNull(loaded);
        }
        finally
        {
            ModalInputGate.End(owner);
        }

        Assert.IsTrue(controller.TryDispatchForTests(world, Vector2.zero, SecondFloorRightController.InteractionLeft));
        Assert.AreEqual(SceneNames.SecondFloorLeft, loaded);
        Object.DestroyImmediate(world);
        Object.DestroyImmediate(panel);
    }

    static void RunToEnd(IEnumerator sequence)
    {
        if (sequence == null)
            return;

        while (sequence.MoveNext())
        {
            if (sequence.Current is IEnumerator nested)
                RunToEnd(nested);
        }
    }
}
