using System.Collections.Generic;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 뒤로가기 버튼처럼 진짜 인터랙티브 UI 위에서의 클릭이 아래 월드 오브젝트로
/// 새지 않도록 하는 Clickable2D.ContainsInteractiveUi 판정 로직을 검증합니다.
/// </summary>
public class Clickable2DInteractiveUiGuardTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        spawned.Clear();
    }

    [Test]
    public void ContainsInteractiveUi_ReturnsTrue_WhenInteractableButtonIsHit()
    {
        Button button = CreateUi<Button>("BackButton");
        var results = new List<RaycastResult> { Hit(button.gameObject) };

        Assert.IsTrue(
            Clickable2D.ContainsInteractiveUi(results),
            "뒤로가기 버튼 같은 상호작용 UI 위에서는 월드 클릭을 막아야 합니다.");
    }

    [Test]
    public void ContainsInteractiveUi_ReturnsFalse_ForNonInteractiveImageBlocker()
    {
        Image blocker = CreateUi<Image>("TransparentBlocker");
        var results = new List<RaycastResult> { Hit(blocker.gameObject) };

        Assert.IsFalse(
            Clickable2D.ContainsInteractiveUi(results),
            "Selectable이 없는 투명 차단 Image는 월드 클릭을 막지 않아야 합니다.");
    }

    [Test]
    public void ContainsInteractiveUi_ReturnsFalse_WhenButtonIsNotInteractable()
    {
        Button button = CreateUi<Button>("DisabledButton");
        button.interactable = false;
        var results = new List<RaycastResult> { Hit(button.gameObject) };

        Assert.IsFalse(
            Clickable2D.ContainsInteractiveUi(results),
            "비활성(interactable=false) 버튼은 어차피 클릭되지 않으므로 막을 필요가 없습니다.");
    }

    [Test]
    public void ContainsInteractiveUi_FindsButtonOnParent_WhenChildGraphicIsHit()
    {
        Button button = CreateUi<Button>("BackButton");
        Image childGraphic = CreateUi<Image>("Label");
        childGraphic.transform.SetParent(button.transform, false);
        var results = new List<RaycastResult> { Hit(childGraphic.gameObject) };

        Assert.IsTrue(
            Clickable2D.ContainsInteractiveUi(results),
            "버튼의 자식 그래픽이 레이캐스트에 잡혀도 부모 버튼을 찾아 막아야 합니다.");
    }

    [Test]
    public void ContainsInteractiveUi_ReturnsFalse_ForNullOrEmptyResults()
    {
        Assert.IsFalse(Clickable2D.ContainsInteractiveUi(null));
        Assert.IsFalse(Clickable2D.ContainsInteractiveUi(new List<RaycastResult>()));
    }

    private T CreateUi<T>(string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        spawned.Add(go);
        return go.AddComponent<T>();
    }

    private static RaycastResult Hit(GameObject go)
    {
        return new RaycastResult { gameObject = go };
    }
}
