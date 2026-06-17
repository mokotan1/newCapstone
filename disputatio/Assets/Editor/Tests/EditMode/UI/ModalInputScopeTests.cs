using System.Collections.Generic;
using System.Reflection;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패널 루트에 붙는 <see cref="ModalInputScope"/> 컴포넌트가 활성/비활성 시점에
/// <see cref="ModalInputGate"/> 잠금을 정확히 열고 닫는지 검증합니다.
///
/// EditMode 테스트에서는 Unity 가 MonoBehaviour 의 OnEnable/OnDisable 을
/// 자동 호출하지 않으므로, 리플렉션으로 직접 해당 라이프사이클 메서드를 호출해
/// 실제 OnEnable/OnDisable 로직을 결정적으로 검증합니다.
/// </summary>
public class ModalInputScopeTests
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
    public void OnEnable_BeginsGate_OnDisable_EndsGate()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject world = Track(new GameObject("WorldObject"));

        var scope = panel.AddComponent<ModalInputScope>();

        InvokeLifecycle(scope, "OnEnable");
        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput, "스코프가 활성화되면 월드 입력이 차단되어야 합니다.");
        Assert.IsFalse(ModalInputGate.CanWorldClick(world));

        InvokeLifecycle(scope, "OnDisable");
        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput, "스코프가 비활성화되면 잠금이 해제되어야 합니다.");
        Assert.IsTrue(ModalInputGate.CanWorldClick(world));
    }

    [Test]
    public void AllowedRoot_DefaultsToOwnGameObject_AndAllowsChildren()
    {
        GameObject panel = Track(new GameObject("Panel"));
        GameObject child = Track(new GameObject("Child"));
        child.transform.SetParent(panel.transform, false);
        GameObject world = Track(new GameObject("WorldObject"));

        var scope = panel.AddComponent<ModalInputScope>();
        InvokeLifecycle(scope, "OnEnable");

        Assert.AreSame(panel, scope.AllowedRoot);
        Assert.IsTrue(ModalInputGate.IsAllowed(child), "허용 루트 하위는 입력이 허용되어야 합니다.");
        Assert.IsFalse(ModalInputGate.IsAllowed(world), "허용 루트 밖 월드 오브젝트는 막혀야 합니다.");
    }

    [Test]
    public void OnDestroy_EndsGate()
    {
        GameObject panel = Track(new GameObject("Panel"));
        var scope = panel.AddComponent<ModalInputScope>();
        InvokeLifecycle(scope, "OnEnable");

        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);

        // DestroyImmediate 는 EditMode 에서도 OnDestroy 를 호출합니다.
        Object.DestroyImmediate(panel);

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);
    }

    [Test]
    public void CreateRaycastBlocker_AddsRaycastTargetImageBehindContent()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        var content = new GameObject("Content", typeof(RectTransform), typeof(Image));
        content.transform.SetParent(canvasRoot.transform, false);

        var scope = panel.AddComponent<ModalInputScope>();
        scope.ConfigureForTests(blocksWorld: true, blocksHud: true, createRaycastBlocker: true);
        scope.RebuildBlockerForTests();

        Image blocker = scope.RaycastBlockerForTests;
        Assert.IsNotNull(blocker, "createRaycastBlocker가 true면 투명 차단 Image를 생성해야 합니다.");
        Assert.IsTrue(blocker.raycastTarget, "차단 Image는 raycastTarget=true여야 UI 클릭을 소비합니다.");
        Assert.AreSame(panel.transform.parent, blocker.transform.parent, "차단 Image는 패널과 같은 부모에 있어야 합니다.");
        Assert.Less(
            blocker.transform.GetSiblingIndex(),
            panel.transform.GetSiblingIndex(),
            "차단 Image는 패널 콘텐츠보다 뒤(아래) sibling 이어야 내부 버튼이 동작합니다.");
    }

    [Test]
    public void OnDisable_RemovesRaycastBlocker()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        var scope = panel.AddComponent<ModalInputScope>();
        scope.ConfigureForTests(blocksWorld: true, blocksHud: true, createRaycastBlocker: true);
        scope.RebuildBlockerForTests();
        Assert.IsNotNull(scope.RaycastBlockerForTests);

        InvokeLifecycle(scope, "OnDisable");

        Assert.IsNull(scope.RaycastBlockerForTests, "비활성화 시 차단 Image도 제거되어야 합니다.");
    }

    [Test]
    public void RaycastBlocker_EnabledByDefault()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        var scope = panel.AddComponent<ModalInputScope>();

        // 기본값 createRaycastBlocker=true 이면 활성화 시 차단막을 생성해야 합니다.
        InvokeLifecycle(scope, "OnEnable");

        Assert.IsNotNull(
            scope.RaycastBlockerForTests,
            "ModalInputScope 는 기본적으로 투명 차단막을 켜고(secure-by-default) 활성화 시 생성해야 합니다.");
    }

    [Test]
    public void SetCreateRaycastBlocker_DrivesBlockerCreation()
    {
        var canvasRoot = Track(new GameObject("Canvas", typeof(RectTransform)));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasRoot.transform, false);

        var scope = panel.AddComponent<ModalInputScope>();

        scope.SetCreateRaycastBlocker(false);
        scope.RebuildBlockerForTests();
        Assert.IsNull(scope.RaycastBlockerForTests, "끄면 차단막이 생성되지 않아야 합니다.");

        scope.SetCreateRaycastBlocker(true);
        scope.RebuildBlockerForTests();
        Assert.IsNotNull(scope.RaycastBlockerForTests, "다시 켜면 차단막이 생성되어야 합니다.");
    }

    private static void InvokeLifecycle(ModalInputScope scope, string methodName)
    {
        MethodInfo method = typeof(ModalInputScope).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Lifecycle method not found: {methodName}");
        method.Invoke(scope, null);
    }

    private GameObject Track(GameObject go)
    {
        spawned.Add(go);
        return go;
    }
}
