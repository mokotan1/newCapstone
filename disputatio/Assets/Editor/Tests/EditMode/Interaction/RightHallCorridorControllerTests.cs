using System.Collections;
using System.Collections.Generic;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class RightHallCorridorControllerTests
{
    GameObject root;
    RightHallCorridorController controller;
    string loaded;
    bool wentBack;
    readonly List<string> said = new List<string>();
    readonly List<(float alpha, float duration)> fades = new List<(float, float)>();
    readonly List<float> waits = new List<float>();
    readonly List<(string method, float a, float b)> exposure = new List<(string, float, float)>();
    int footsteps;

    [SetUp]
    public void SetUp()
    {
        RightHallCorridorController.ResetStateForTests();
        HallGlobalStateHost.EnsureInstance();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        loaded = null;
        wentBack = false;
        said.Clear();
        fades.Clear();
        waits.Clear();
        exposure.Clear();
        footsteps = 0;

        RightHallCorridorController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        RightHallCorridorController.BackHandlerForTests = () => wentBack = true;
        RightHallCorridorController.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fades.Add((alpha, duration));
            onComplete?.Invoke();
        };
        RightHallCorridorController.SayHandlerForTests = (line, onComplete) =>
        {
            said.Add(line);
            onComplete?.Invoke();
        };
        RightHallCorridorController.WaitHandlerForTests = seconds => waits.Add(seconds);
        RightHallCorridorController.AudioHandlerForTests = () => footsteps++;
        RightHallCorridorController.ExposureHandlerForTests = (method, a, b) => exposure.Add((method, a, b));
        RightHallCorridorController.SequenceRunnerForTests = RunToEnd;

        root = new GameObject("RightHallTests");
        controller = root.AddComponent<RightHallCorridorController>();
    }

    [TearDown]
    public void TearDown()
    {
        RightHallCorridorController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void HallRight_FrontLoadsHallRight2AfterFade()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight);
        IEnumerator pending = null;
        RightHallCorridorController.SequenceRunnerForTests = sequence => pending = sequence;

        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.IsTrue(controller.IsBusyForTests);
        Assert.IsNull(loaded);

        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.IsNull(loaded);

        RunToEnd(pending);
        Assert.AreEqual(SceneNames.HallRight2, loaded);
        Assert.AreEqual((RightHallCorridorController.ExitFadeTargetAlpha, RightHallCorridorController.ExitFadeDurationSeconds), fades[0]);
        Assert.AreEqual(RightHallCorridorController.PostFadeWaitSeconds, waits[0]);
        Assert.AreEqual(1, footsteps);
        Assert.IsFalse(wentBack);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void HallRight_ConfirmYesUsesBackRoute()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight);
        controller.ConfirmYes();
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
        Assert.AreEqual(1, footsteps);
        Assert.AreEqual(RightHallCorridorController.PostFadeWaitSeconds, waits[0]);
    }

    [Test]
    public void HallRight_ConfirmNoReleasesInput()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight);
        GameObject menu = new GameObject("MenuDialog");
        menu.SetActive(true);
        controller.AssignMenuForTests(menu);

        IEnumerator pending = null;
        RightHallCorridorController.SequenceRunnerForTests = sequence => pending = sequence;
        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.IsTrue(controller.IsBusyForTests);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);

        controller.ConfirmNo();
        Assert.IsFalse(menu.activeSelf);
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsNull(loaded);
        Object.DestroyImmediate(menu);
    }

    [Test]
    public void HallRight_InspectShowsOriginalLines()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight);
        controller.OnInteraction(RightHallCorridorController.InteractionShowcase);
        Assert.AreEqual(RightHallCorridorController.ShowcaseLine, said[0]);
        Assert.AreEqual(RightHallCorridorController.InspectionWaitSeconds, waits[0]);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));

        controller.OnInteraction(RightHallCorridorController.InteractionMedal);
        Assert.AreEqual(RightHallCorridorController.MedalLine, said[1]);
    }

    [Test]
    public void HallRight_ElectricOnAppliesOriginalEntryEffects()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, true);
        controller.RunEnter();
        Assert.AreEqual(("TriggerFlashEffect", 0f, 0f), exposure[0]);
        Assert.AreEqual(("SetVignetteToZero", 0f, 0f), exposure[1]);
        Assert.AreEqual((RightHallCorridorController.EnterFadeTargetAlpha, RightHallCorridorController.EnterFadeDurationSeconds), fades[0]);

        exposure.Clear();
        fades.Clear();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, false);
        controller.RunEnter();
        Assert.AreEqual(("TriggerFlashEffect", RightHallCorridorController.ElectricOffFlash, RightHallCorridorController.ElectricOffFlash), exposure[0]);
        Assert.AreEqual(1, exposure.Count);
    }

    [Test]
    public void HallRight2_FrontLoadsCross()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight2);
        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.AreEqual(SceneNames.HallRightCross, loaded);
        Assert.AreEqual(1, fades.Count);
        Assert.AreEqual(1, footsteps);
    }

    [Test]
    public void HallRight2_ConfirmYesPreservesSelfRoute()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight2);
        controller.ConfirmYes();
        Assert.AreEqual(SceneNames.HallRight2, loaded);
        Assert.IsFalse(wentBack);
    }

    [Test]
    public void HallRight2_MedalShowsOriginalLine()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight2);
        controller.OnInteraction(RightHallCorridorController.InteractionMedal);
        Assert.AreEqual(RightHallCorridorController.MedalLine, said[0]);
        Assert.AreEqual(RightHallCorridorController.InspectionWaitSeconds, waits[0]);
    }

    [Test]
    public void HallRight2_NoReleasesInput()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRight2);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);
        controller.ConfirmNo();
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsNull(loaded);
    }

    [Test]
    public void Cross_RightLoadsStudyEntrance()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRightCross);
        controller.OnInteraction(RightHallCorridorController.InteractionRight);
        Assert.AreEqual(SceneNames.StudyEntrance, loaded);
        Assert.AreEqual(1, fades.Count);
    }

    [Test]
    public void Cross_LeftLoadsMaidEntrance()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRightCross);
        controller.OnInteraction(RightHallCorridorController.InteractionLeft);
        Assert.AreEqual(SceneNames.MaidEntrance, loaded);
    }

    [Test]
    public void Cross_PreviousChoiceLoadsHallwayRight()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRightCross);
        controller.OnPreviousChoice();
        Assert.AreEqual(SceneNames.HallwayRight, loaded);
        Assert.AreEqual(1, footsteps);
    }

    [Test]
    public void Cross_NoReleasesInput()
    {
        controller.AssignRouteForTests(RightHallRoute.HallRightCross);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);
        controller.ConfirmNo();
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsNull(loaded);
    }

    [Test]
    public void HallwayRight_FrontLoadsHallwayRight2()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight);
        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.AreEqual(SceneNames.HallwayRight2, loaded);
        Assert.AreEqual(RightHallCorridorController.ExitFadeDurationSeconds, fades[0].duration);
        Assert.AreEqual(RightHallCorridorController.PostFadeWaitSeconds, waits[0]);
    }

    [Test]
    public void HallwayRight_ConfirmYesUsesBackRoute()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight);
        controller.ConfirmYes();
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
    }

    [Test]
    public void HallwayRight_InspectionsPreserveLines()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight);
        controller.RunInspect(RightHallCorridorController.ShowcaseLine);
        controller.RunInspect(RightHallCorridorController.MedalLine);
        Assert.AreEqual(RightHallCorridorController.ShowcaseLine, said[0]);
        Assert.AreEqual(RightHallCorridorController.MedalLine, said[1]);
    }

    [Test]
    public void HallwayRight_NoReleasesInput()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight);
        controller.ConfirmNo();
        Assert.IsFalse(controller.IsBusyForTests);
    }

    [Test]
    public void HallwayRight2_FrontLoadsHallPlayable()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight2);
        controller.OnInteraction(RightHallCorridorController.InteractionFront);
        Assert.AreEqual(SceneNames.HallPlayable, loaded);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void HallwayRight2_ConfirmYesUsesBackRoute()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight2);
        controller.ConfirmYes();
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
    }

    [Test]
    public void HallwayRight2_InspectionsPreserveLines()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight2);
        controller.OnInteraction(RightHallCorridorController.InteractionShowcase);
        controller.OnInteraction(RightHallCorridorController.InteractionMedal);
        Assert.AreEqual(RightHallCorridorController.ShowcaseLine, said[0]);
        Assert.AreEqual(RightHallCorridorController.MedalLine, said[1]);
    }

    [Test]
    public void HallwayRight2_NoReleasesInput()
    {
        controller.AssignRouteForTests(RightHallRoute.HallwayRight2);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);
        controller.ConfirmNo();
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsFalse(controller.IsBusyForTests);
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
