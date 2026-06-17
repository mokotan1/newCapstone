using System.Collections.Generic;
using Fungus;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 모달 UI가 열려 있는 동안 월드/HUD 입력을 공통으로 차단하는 <see cref="ModalInputGate"/> 검증.
/// 여러 모달이 stack 으로 겹치는 경우, owner 가 사라진 경우의 방어 동작을 포함합니다.
/// </summary>
public class ModalInputGateTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        ModalInputGate.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        ModalInputGate.ResetForTests();

        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Object.DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
    }

    [Test]
    public void NoActiveScope_AllowsEverything()
    {
        GameObject world = Track(new GameObject("WorldObject"));

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);
        Assert.IsFalse(ModalInputGate.IsBlockingHudInput);
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
        Assert.IsTrue(ModalInputGate.IsAllowed(world));
    }

    [Test]
    public void ModalOpen_BlocksWorldClickOutsideAllowedRoot()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject world = Track(new GameObject("WorldObject"));

        ModalInputGate.Begin(new object(), panel);

        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);
        Assert.IsFalse(ModalInputGate.CanWorldClick(world));
        Assert.IsTrue(Clickable2D.ShouldBlockWorldClick(world));
    }

    [Test]
    public void ModalOpen_AllowsInputInsideAllowedRoot()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject closeButton = Track(new GameObject("CloseButton"));
        closeButton.transform.SetParent(panel.transform, false);

        ModalInputGate.Begin(new object(), panel);

        Assert.IsTrue(ModalInputGate.IsAllowed(closeButton));
        Assert.IsTrue(ModalInputGate.IsAllowed(panel));
        Assert.IsTrue(ModalInputGate.CanWorldClick(closeButton));
        Assert.IsFalse(Clickable2D.ShouldBlockWorldClick(closeButton));
    }

    [Test]
    public void AfterEnd_WorldClickAllowedAgain()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject world = Track(new GameObject("WorldObject"));
        var owner = new object();

        ModalInputGate.Begin(owner, panel);
        ModalInputGate.End(owner);

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
    }

    [Test]
    public void MultipleOwners_RemainBlocked_WhenOnlyOneEnds()
    {
        GameObject panelA = Track(new GameObject("PanelA"));
        GameObject panelB = Track(new GameObject("PanelB"));
        GameObject world = Track(new GameObject("WorldObject"));
        var ownerA = new object();
        var ownerB = new object();

        ModalInputGate.Begin(ownerA, panelA);
        ModalInputGate.Begin(ownerB, panelB);

        ModalInputGate.End(ownerA);

        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput, "아직 모달 B가 열려 있으므로 차단이 유지되어야 합니다.");
        Assert.IsFalse(ModalInputGate.CanWorldClick(world));

        ModalInputGate.End(ownerB);
        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);
    }

    [Test]
    public void BlocksHudButton_OutsideAllowedRoot_ButAllowsInside()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject panelButton = Track(new GameObject("PanelButton"));
        panelButton.transform.SetParent(panel.transform, false);
        GameObject hudButton = Track(new GameObject("MoveButton"));

        ModalInputGate.Begin(new object(), panel, blocksHud: true, blocksWorld: true);

        Assert.IsTrue(ModalInputGate.IsBlockingHudInput);
        Assert.IsFalse(ModalInputGate.CanUseHudButton(hudButton));
        Assert.IsTrue(ModalInputGate.CanUseHudButton(panelButton));
    }

    [Test]
    public void NonWorldBlockingScope_DoesNotBlockWorld()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject world = Track(new GameObject("WorldObject"));

        ModalInputGate.Begin(new object(), panel, blocksHud: true, blocksWorld: false);

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
        Assert.IsTrue(ModalInputGate.IsBlockingHudInput);
    }

    [Test]
    public void DestroyedAllowedRootOwner_IsPrunedSoLockDoesNotStick()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject world = Track(new GameObject("WorldObject"));

        // owner 가 UnityEngine.Object 이고 파괴되면 영구 잠금이 남지 않아야 합니다.
        var ownerComponent = panel.AddComponent<BoxCollider2D>();
        ModalInputGate.Begin(ownerComponent, panel);
        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);

        Object.DestroyImmediate(panel);

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput, "owner가 파괴되면 자동으로 차단이 해제되어야 합니다.");
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
    }

    [Test]
    public void Begin_NullTarget_IsNotAllowedWhileBlocking()
    {
        GameObject panel = Track(new GameObject("Panel"));
        ModalInputGate.Begin(new object(), panel);

        Assert.IsFalse(ModalInputGate.IsAllowed(null));
    }

    [Test]
    public void StackedModals_OnlyTopmostScopeAllowsInput()
    {
        GameObject panelA = Track(new GameObject("PanelA"));
        GameObject childA = Track(new GameObject("ChildA"));
        childA.transform.SetParent(panelA.transform, false);

        GameObject panelB = Track(new GameObject("PanelB"));
        GameObject childB = Track(new GameObject("ChildB"));
        childB.transform.SetParent(panelB.transform, false);

        ModalInputGate.Begin(new object(), panelA);
        ModalInputGate.Begin(new object(), panelB); // B 가 최상단

        Assert.IsTrue(ModalInputGate.IsAllowed(childB), "최상단 모달 내부 버튼은 허용되어야 합니다.");
        Assert.IsTrue(ModalInputGate.IsAllowed(panelB));
        Assert.IsFalse(ModalInputGate.IsAllowed(childA), "뒤쪽 모달 내부 버튼은 막혀야 합니다.");
        Assert.IsFalse(ModalInputGate.IsAllowed(panelA), "뒤쪽 모달 루트도 막혀야 합니다.");
    }

    [Test]
    public void RebegunOwner_BecomesTopmost()
    {
        GameObject panelA = Track(new GameObject("PanelA"));
        GameObject childA = Track(new GameObject("ChildA"));
        childA.transform.SetParent(panelA.transform, false);

        GameObject panelB = Track(new GameObject("PanelB"));
        GameObject childB = Track(new GameObject("ChildB"));
        childB.transform.SetParent(panelB.transform, false);

        var ownerA = new object();
        var ownerB = new object();

        ModalInputGate.Begin(ownerA, panelA);
        ModalInputGate.Begin(ownerB, panelB);
        Assert.IsFalse(ModalInputGate.IsAllowed(childA), "B 가 최상단이면 A 내부는 막혀야 합니다.");

        // A 를 다시 열면 최상단이 되어 A 내부가 허용되고 B 는 막혀야 합니다.
        ModalInputGate.Begin(ownerA, panelA);
        Assert.IsTrue(ModalInputGate.IsAllowed(childA), "재진입한 모달이 최상단이 되어야 합니다.");
        Assert.IsFalse(ModalInputGate.IsAllowed(childB));
    }

    [Test]
    public void DestroyedAllowedRootWithLiveOwner_IsPruned_NoPermanentBlock()
    {
        GameObject ownerGo = Track(new GameObject("Owner"));
        var owner = ownerGo.AddComponent<BoxCollider2D>();

        // owner 와 분리된 별도 GameObject 를 allowedRoot 로 사용합니다.
        GameObject allowedRoot = new GameObject("AllowedRoot");
        GameObject world = Track(new GameObject("WorldObject"));

        ModalInputGate.Begin(owner, allowedRoot);
        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);

        // owner 는 살아 있고 allowedRoot 만 파괴된 경우에도 영구 차단이 남지 않아야 합니다.
        Object.DestroyImmediate(allowedRoot);

        Assert.IsFalse(
            ModalInputGate.IsBlockingWorldInput,
            "allowedRoot 가 파괴되면 스코프가 정리되어 차단이 해제되어야 합니다.");
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }
}
