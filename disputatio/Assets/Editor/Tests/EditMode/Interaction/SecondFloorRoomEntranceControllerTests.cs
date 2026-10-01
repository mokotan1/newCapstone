using System.Collections;
using System.Collections.Generic;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class SecondFloorRoomEntranceControllerTests
{
    GameObject root;
    SecondFloorRoomEntranceController controller;
    string loaded;
    bool wentBack;
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
        SecondFloorRoomEntranceController.ResetStateForTests();
        HallGlobalStateHost.EnsureInstance();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        SceneInteractionController.DuplicateClickCooldownSeconds = 0f;
        loaded = null;
        wentBack = false;
        said.Clear();
        menus.Clear();
        fades.Clear();
        waits.Clear();
        exposure.Clear();
        footsteps = 0;
        menuChoice = 0;

        SecondFloorRoomEntranceController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        SecondFloorRoomEntranceController.BackHandlerForTests = () => wentBack = true;
        SecondFloorRoomEntranceController.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fades.Add((alpha, duration));
            onComplete?.Invoke();
        };
        SecondFloorRoomEntranceController.SayHandlerForTests = (line, onComplete) =>
        {
            said.Add(line);
            onComplete?.Invoke();
        };
        SecondFloorRoomEntranceController.WaitHandlerForTests = seconds => waits.Add(seconds);
        SecondFloorRoomEntranceController.AudioHandlerForTests = () => footsteps++;
        SecondFloorRoomEntranceController.ExposureHandlerForTests = (method, a, b) => exposure.Add((method, a, b));
        SecondFloorRoomEntranceController.MenuHandlerForTests = (options, onChoice) =>
        {
            menus.Add(options);
            onChoice(menuChoice);
        };
        SecondFloorRoomEntranceController.SequenceRunnerForTests = RunToEnd;

        root = new GameObject("RoomEntranceTests");
        controller = root.AddComponent<SecondFloorRoomEntranceController>();
        said.Clear();
        fades.Clear();
        waits.Clear();
        exposure.Clear();
        footsteps = 0;
    }

    [TearDown]
    public void TearDown()
    {
        SecondFloorRoomEntranceController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Wife_EnterElectricOnFlashesThenFadesIn()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, true);
        controller.RunEnter();
        Assert.AreEqual(("TriggerFlashEffect", 0f, 0f), exposure[0]);
        Assert.AreEqual(("SetVignetteToZero", 0f, 0f), exposure[1]);
        Assert.AreEqual((0f, 1f), fades[0]);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void Bed_EnterElectricOffOnlyFadesIn()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Bed);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.ElectricOn, false);
        controller.RunEnter();
        Assert.AreEqual(0, exposure.Count);
        Assert.AreEqual((0f, 1f), fades[0]);
    }

    [Test]
    public void Wife_LockedDoorUsesWifeHintAndDoesNotSetKey()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        controller.OnDoor();
        Assert.AreEqual("잠겨있다", said[0]);
        Assert.AreEqual("열쇠가 있어야 문을 열 수 있을 것 같다.", said[1]);
        Assert.AreEqual(0.1f, waits[0]);
        Assert.AreEqual(0.1f, waits[1]);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.UsedWifeKey));
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
        Assert.IsNull(loaded);
    }

    [Test]
    public void Bed_LockedDoorUsesBedHint()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Bed);
        controller.OnInteraction(SecondFloorRoomEntranceController.InteractionDoor);
        Assert.AreEqual("잠겨있다", said[0]);
        Assert.AreEqual("열쇠가 있어야 열 수 있을 것 같다.", said[1]);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.UsedBedKey));
    }

    [Test]
    public void Wife_UnlockedDoorYesLoadsWifeRoom()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedWifeKey, true);
        menuChoice = 0;
        controller.OnDoor();
        Assert.AreEqual("들어갈까?\n", said[0]);
        Assert.AreEqual("들어간다", menus[0][0]);
        Assert.AreEqual("들어가지 않는다", menus[0][1]);
        Assert.AreEqual(0.1f, waits[0]);
        AssertExitFade();
        Assert.AreEqual(SceneNames.WifeRoom, loaded);
        Assert.AreNotEqual(SceneNames.HallAnimate, loaded);
    }

    [Test]
    public void Wife_UnlockedDoorNoSaysLookAround()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedWifeKey, true);
        menuChoice = 1;
        controller.OnDoor();
        Assert.AreEqual("주변을 더 둘러봐야겠다.", said[1]);
        Assert.IsNull(loaded);
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void Bed_UnlockYesSetsKeyAndLoadsBedRoom()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Bed);
        menuChoice = 0;
        controller.OnUnlock();
        Assert.IsTrue(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.UsedBedKey));
        Assert.AreEqual("문이 열렸다. 들어갈까?", said[0]);
        Assert.AreEqual(0.1f, waits[0]);
        AssertExitFade();
        Assert.AreEqual(SceneNames.BedRoom, loaded);
    }

    [Test]
    public void Wife_UnlockNoSaysNeedPreparation()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        menuChoice = 1;
        controller.OnInteraction(SecondFloorRoomEntranceController.InteractionUnlock);
        Assert.IsTrue(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.UsedWifeKey));
        Assert.AreEqual("조금 더 준비가 필요하다.", said[1]);
        Assert.IsNull(loaded);
        Assert.IsFalse(controller.IsBusyForTests);
    }

    [Test]
    public void Wife_BackYesUsesBackNavigator()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        menuChoice = 0;
        controller.OnBack();
        Assert.AreEqual("이전 위치로 돌아갈까?", said[0]);
        Assert.AreEqual("돌아간다.", menus[0][0]);
        Assert.AreEqual("돌아가지 않는다.", menus[0][1]);
        AssertExitFade();
        Assert.IsTrue(wentBack);
        Assert.IsNull(loaded);
    }

    [Test]
    public void Bed_BackNoReleasesInput()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Bed);
        menuChoice = 1;
        controller.OnInteraction(SecondFloorRoomEntranceController.InteractionBack);
        Assert.AreEqual("이전 위치로 돌아갈까?", said[0]);
        Assert.IsFalse(wentBack);
        Assert.IsNull(loaded);
        Assert.IsFalse(controller.IsBusyForTests);
        Assert.IsFalse(HallGlobalStateHost.Instance.GetBool(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void BusySequenceRejectsAnotherDoor()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        IEnumerator pending = null;
        SecondFloorRoomEntranceController.SequenceRunnerForTests = sequence => pending = sequence;
        controller.OnDoor();
        Assert.IsTrue(controller.IsBusyForTests);
        IEnumerator first = pending;
        Assert.IsNotNull(first);
        controller.OnDoor();
        Assert.AreSame(first, pending);
        Assert.AreEqual(0, said.Count);
    }

    [Test]
    public void WorldClick_ModalBlocksDispatch()
    {
        controller.AssignRouteForTests(SecondFloorRoomEntrance.Wife);
        GameObject panel = new GameObject("ModalPanel");
        GameObject world = new GameObject("World");
        object owner = new object();
        ModalInputGate.Begin(owner, panel, blocksHud: true, blocksWorld: true);
        try
        {
            Assert.IsFalse(controller.TryDispatchForTests(world, Vector2.zero, SecondFloorRoomEntranceController.InteractionDoor));
            Assert.AreEqual(0, said.Count);
        }
        finally
        {
            ModalInputGate.End(owner);
        }

        Assert.IsTrue(controller.TryDispatchForTests(world, Vector2.zero, SecondFloorRoomEntranceController.InteractionDoor));
        Assert.AreEqual("잠겨있다", said[0]);
        Object.DestroyImmediate(world);
        Object.DestroyImmediate(panel);
    }

    void AssertExitFade()
    {
        Assert.AreEqual((1f, 1f), fades[fades.Count - 1]);
        Assert.AreEqual(2f, waits[waits.Count - 1]);
        Assert.AreEqual(1, footsteps);
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
