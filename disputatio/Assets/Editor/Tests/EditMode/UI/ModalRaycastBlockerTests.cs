using System.Collections.Generic;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 모달 패널 뒤 투명 raycast 차단막을 만드는 공용 헬퍼 <see cref="ModalRaycastBlocker"/> 검증.
/// </summary>
public class ModalRaycastBlockerTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Object.DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
    }

    [Test]
    public void Create_AddsTransparentRaycastImage_AsSiblingBehindPanel()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        Image blocker = ModalRaycastBlocker.Create(panel.transform);

        Assert.IsNotNull(blocker);
        Assert.IsTrue(blocker.raycastTarget, "차단막은 raycastTarget=true 여야 UI 클릭을 소비합니다.");
        Assert.AreEqual(0f, blocker.color.a, "차단막은 완전 투명해야 합니다.");
        Assert.AreSame(panel.transform.parent, blocker.transform.parent, "차단막은 패널과 같은 부모여야 합니다.");
        Assert.Less(
            blocker.transform.GetSiblingIndex(),
            panel.transform.GetSiblingIndex(),
            "차단막은 패널보다 뒤(아래) sibling 이어야 내부 버튼이 동작합니다.");

        RectTransform rect = blocker.rectTransform;
        Assert.AreEqual(Vector2.zero, rect.anchorMin);
        Assert.AreEqual(Vector2.one, rect.anchorMax);
    }

    [Test]
    public void Create_WithNoParent_ParentsUnderPanelAsFirstSibling()
    {
        var panel = Track(new GameObject("Panel", typeof(RectTransform)));
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);

        Image blocker = ModalRaycastBlocker.Create(panel.transform);

        Assert.IsNotNull(blocker);
        Assert.AreSame(panel.transform, blocker.transform.parent, "부모가 없으면 패널 아래로 들어가야 합니다.");
        Assert.AreEqual(0, blocker.transform.GetSiblingIndex(), "패널 아래 첫 sibling 으로 들어가야 합니다.");
    }

    [Test]
    public void Create_NullPanel_ReturnsNull()
    {
        Assert.IsNull(ModalRaycastBlocker.Create(null));
    }

    [Test]
    public void Remove_DestroysBlocker()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        Image blocker = ModalRaycastBlocker.Create(panel.transform);
        Assert.IsNotNull(blocker);
        GameObject go = blocker.gameObject;

        ModalRaycastBlocker.Remove(blocker);

        Assert.IsTrue(go == null, "Remove 는 차단막 GameObject 를 파괴해야 합니다.");
    }

    [Test]
    public void Remove_Null_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => ModalRaycastBlocker.Remove(null));
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }
}
