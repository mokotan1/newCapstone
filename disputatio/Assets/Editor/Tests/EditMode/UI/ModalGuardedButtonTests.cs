using System.Collections.Generic;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 버튼(이동/지도/뒤로가기 등)에 붙는 <see cref="ModalGuardedButton"/> 가
/// 모달 게이트 상태에 따라 클릭을 막고/허용하는지 검증합니다.
/// </summary>
public class ModalGuardedButtonTests
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
    public void ShouldBlock_False_WhenNoModalActive()
    {
        ModalGuardedButton guard = CreateButton("MoveButton");

        Assert.IsFalse(guard.ShouldBlock(), "모달이 없으면 HUD 버튼은 막히지 않아야 합니다.");
    }

    [Test]
    public void ShouldBlock_True_WhenModalBlocksHud_AndButtonOutsideAllowedRoot()
    {
        GameObject panel = Track(new GameObject("Panel"));
        ModalGuardedButton guard = CreateButton("MoveButton");

        ModalInputGate.Begin(new object(), panel, blocksHud: true, blocksWorld: true);

        Assert.IsTrue(guard.ShouldBlock(), "모달 허용 루트 밖의 HUD 버튼은 막혀야 합니다.");
    }

    [Test]
    public void ShouldBlock_False_WhenButtonInsideAllowedRoot()
    {
        GameObject panel = Track(new GameObject("Panel", typeof(RectTransform)));
        ModalGuardedButton guard = CreateButton("PanelButton");
        guard.transform.SetParent(panel.transform, false);

        ModalInputGate.Begin(new object(), panel, blocksHud: true, blocksWorld: true);

        Assert.IsFalse(guard.ShouldBlock(), "모달 내부 버튼은 허용되어야 합니다.");
    }

    [Test]
    public void Refresh_DisablesInteractableWhileBlocked_AndRestoresAfterClose()
    {
        GameObject panel = Track(new GameObject("Panel"));
        ModalGuardedButton guard = CreateButton("MoveButton");
        var button = guard.GetComponent<Button>();
        button.interactable = true;

        ModalInputGate.Begin(new object(), panel, blocksHud: true, blocksWorld: true);
        guard.Refresh();
        Assert.IsFalse(button.interactable, "모달이 HUD를 막는 동안 버튼은 비활성화되어야 합니다.");

        ModalInputGate.ResetForTests();
        guard.Refresh();
        Assert.IsTrue(button.interactable, "모달이 닫히면 원래 interactable 상태로 복구되어야 합니다.");
    }

    private ModalGuardedButton CreateButton(string name)
    {
        var go = Track(new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)));
        return go.AddComponent<ModalGuardedButton>();
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }
}
