using System.Collections.Generic;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 버튼 자동 가드 설치기 <see cref="ModalHudGuardInstaller"/> 의 부착 판정/멱등성 검증.
/// </summary>
public class ModalHudGuardInstallerTests
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
    public void ShouldGuard_True_ForPlainHudSelectable()
    {
        GameObject go = CreateButton("MoveButton");

        Assert.IsTrue(ModalHudGuardInstaller.ShouldGuard(go.GetComponent<Selectable>()));
    }

    [Test]
    public void ShouldGuard_False_WhenAlreadyGuarded()
    {
        GameObject go = CreateButton("MoveButton");
        go.AddComponent<ModalGuardedButton>();

        Assert.IsFalse(ModalHudGuardInstaller.ShouldGuard(go.GetComponent<Selectable>()));
    }

    [Test]
    public void ShouldGuard_False_WhenInsideModalInputScope()
    {
        GameObject panel = Track(new GameObject("Panel", typeof(RectTransform)));
        panel.AddComponent<ModalInputScope>();

        GameObject go = CreateButton("PanelButton");
        go.transform.SetParent(panel.transform, false);

        Assert.IsFalse(
            ModalHudGuardInstaller.ShouldGuard(go.GetComponent<Selectable>()),
            "모달 패널(ModalInputScope) 내부 버튼은 자동 부착 대상에서 제외되어야 합니다.");
    }

    [Test]
    public void ShouldGuard_False_ForNull()
    {
        Assert.IsFalse(ModalHudGuardInstaller.ShouldGuard(null));
    }

    [Test]
    public void EnsureGuard_AttachesOnce_AndIsIdempotent()
    {
        GameObject go = CreateButton("MoveButton");

        ModalGuardedButton first = ModalHudGuardInstaller.EnsureGuard(go.GetComponent<Selectable>());
        Assert.IsNotNull(first, "HUD Selectable 에는 가드가 부착되어야 합니다.");

        ModalGuardedButton second = ModalHudGuardInstaller.EnsureGuard(go.GetComponent<Selectable>());
        Assert.AreSame(first, second, "이미 부착돼 있으면 중복 부착하지 않아야 합니다.");
        Assert.AreEqual(1, go.GetComponents<ModalGuardedButton>().Length);
    }

    [Test]
    public void EnsureGuard_DoesNotAttach_InsideModalInputScope()
    {
        GameObject panel = Track(new GameObject("Panel", typeof(RectTransform)));
        panel.AddComponent<ModalInputScope>();
        GameObject go = CreateButton("PanelButton");
        go.transform.SetParent(panel.transform, false);

        ModalGuardedButton result = ModalHudGuardInstaller.EnsureGuard(go.GetComponent<Selectable>());

        Assert.IsNull(result);
        Assert.AreEqual(0, go.GetComponents<ModalGuardedButton>().Length);
    }

    private GameObject CreateButton(string name)
    {
        return Track(new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)));
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }
}
