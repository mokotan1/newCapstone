using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Task 7 하이브리드 QA 입력 드라이버의 PlayMode 검증. 실제 <see cref="Canvas"/> +
/// <see cref="EventSystem"/> + <see cref="GraphicRaycaster"/>를 만들어, RealInput 경로가 실제
/// 레이캐스트·인터랙터블 상태를 통해 클릭·드래그·키 입력을 검증하는지, 그리고 가려짐/비활성
/// 대상이 API 모드에서는 성공해도 RealInput에서는 명시적 <c>InputLayerFailure</c> 진단과 함께
/// 실패하는지를 확인합니다(디자인 문서 Task 7 §Step 1~3: 클릭/드래그/비활성/가려짐 픽스처,
/// 조건 기반 완료, API-pass/RealInput-fail 분류).
/// </summary>
public sealed class QaInputDriverPlayModeTests
{
    private GameObject canvasGo;
    private GameObject eventSystemGo;
    private EventSystem eventSystem;
    private readonly Dictionary<QaTargetId, GameObject> targetsById = new Dictionary<QaTargetId, GameObject>();

    [SetUp]
    public void SetUp()
    {
        targetsById.Clear();

        eventSystemGo = new GameObject("QaTestEventSystem");
        eventSystem = eventSystemGo.AddComponent<EventSystem>();

        canvasGo = new GameObject("QaTestCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<GraphicRaycaster>();
    }

    [TearDown]
    public void TearDown()
    {
        if (canvasGo != null)
        {
            UnityEngine.Object.Destroy(canvasGo);
        }

        if (eventSystemGo != null)
        {
            UnityEngine.Object.Destroy(eventSystemGo);
        }
    }

    // ---------------------------------------------------------------
    //  Fixtures
    // ---------------------------------------------------------------

    /// <summary>
    /// 새 <see cref="RectTransform"/>의 기본 앵커는 부모에 꽉 차게 늘어나는 stretch(0,0)-(1,1)라서
    /// <c>sizeDelta</c>가 절대 크기가 아니라 "부모 크기 + sizeDelta"가 되어버립니다(batchmode처럼
    /// 캔버스 실제 픽셀 크기가 작거나 불확실한 환경에서 특히 위험). 앵커를 중앙 점(0.5,0.5)으로
    /// 고정해 <c>sizeDelta</c>/<c>anchoredPosition</c>이 캔버스 크기와 무관하게 항상 같은 절대
    /// 픽셀 크기·오프셋을 의미하도록 만듭니다.
    /// </summary>
    private static void ConfigureFixedSizeRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    private Button CreateButton(string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        ConfigureFixedSizeRect((RectTransform)go.transform, anchoredPosition, sizeDelta);
        go.AddComponent<Image>();
        return go.AddComponent<Button>();
    }

    private GameObject CreateCoveringImage(string name, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        ConfigureFixedSizeRect((RectTransform)go.transform, anchoredPosition, sizeDelta);
        go.AddComponent<Image>();
        go.transform.SetAsLastSibling(); // 마지막 sibling이 같은 캔버스에서 가장 위에 렌더링됩니다.
        return go;
    }

    private QaTargetId RegisterTarget(string rawId, GameObject go)
    {
        QaTargetId id = QaTargetId.Create(rawId);
        targetsById[id] = go;
        return id;
    }

    private GameObject Resolve(QaTargetId id)
    {
        return targetsById.TryGetValue(id, out GameObject go) ? go : null;
    }

    private QaEventSystemInputDriver CreateRealInputDriver(Func<bool> gateProvider = null)
    {
        return new QaEventSystemInputDriver(eventSystem, Resolve, gateProvider);
    }

    private static IEnumerator ToCoroutine(Task task)
    {
        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            throw task.Exception.GetBaseException();
        }
    }

    // ---------------------------------------------------------------
    //  Step 1: click / disabled / covered fixtures (RealInput)
    // ---------------------------------------------------------------

    /// <summary>
    /// 같은 프레임에 생성된 <see cref="Graphic"/>은 <see cref="CanvasRenderer"/>가 아직 첫 렌더 패스를
    /// 거치지 않아 "culled" 상태로 남아 있을 수 있고, 이 경우 <see cref="GraphicRaycaster"/>가 그
    /// Graphic을 완전히 건너뛰어 항상 레이캐스트가 비어 있는 결과를 냅니다(실제 플레이 중인 UI는
    /// 이미 여러 프레임 렌더링되어 있어 발생하지 않는, 순수한 테스트 픽스처 타이밍 문제). RealInput
    /// 드라이버를 호출하기 전에 최소 한 프레임 + 프레임 종료를 기다려 캔버스가 실제로 렌더되고
    /// culling 상태가 갱신되도록 합니다.
    /// </summary>
    private static IEnumerator SettleCanvasAsync()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
    }

    [UnityTest]
    public IEnumerator ClickAsync_VisibleInteractableTarget_Succeeds()
    {
        Button button = CreateButton("Target", Vector2.zero, new Vector2(100f, 40f));
        bool wasClicked = false;
        button.onClick.AddListener(() => wasClicked = true);
        QaTargetId targetId = RegisterTarget("kitchen.button", button.gameObject);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess, task.Result.Message);
        Assert.AreEqual(QaInteractionMode.RealInput, task.Result.Mode);
        Assert.IsTrue(wasClicked, "Button.onClick should have fired via ExecuteEvents.");
    }

    [UnityTest]
    public IEnumerator ClickAsync_DisabledTarget_ReturnsInputLayerFailureAndDoesNotFireClick()
    {
        Button button = CreateButton("DisabledTarget", Vector2.zero, new Vector2(100f, 40f));
        button.interactable = false;
        bool wasClicked = false;
        button.onClick.AddListener(() => wasClicked = true);
        QaTargetId targetId = RegisterTarget("kitchen.disabled-button", button.gameObject);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        QaInputResult result = task.Result;
        Assert.AreEqual(QaInputResultCode.InputLayerFailure, result.Code);
        Assert.IsNotNull(result.Diagnostics);
        Assert.IsFalse(result.Diagnostics.TargetInteractable);
        Assert.IsFalse(wasClicked, "A disabled target must never report a successful RealInput click.");
    }

    [UnityTest]
    public IEnumerator ClickAsync_CoveredTarget_ReturnsInputLayerFailureWithTopHitDiagnosticsAndDoesNotFireClick()
    {
        Button button = CreateButton("CoveredTarget", Vector2.zero, new Vector2(100f, 40f));
        bool wasClicked = false;
        button.onClick.AddListener(() => wasClicked = true);
        QaTargetId targetId = RegisterTarget("kitchen.covered-button", button.gameObject);

        CreateCoveringImage("BlockingOverlay", Vector2.zero, new Vector2(200f, 200f));
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        QaInputResult result = task.Result;
        Assert.AreEqual(QaInputResultCode.InputLayerFailure, result.Code);
        Assert.IsNotNull(result.Diagnostics);
        Assert.IsFalse(result.Diagnostics.RaycastHitTarget);
        CollectionAssert.Contains(result.Diagnostics.RaycastHitNames, "BlockingOverlay");
        Assert.IsFalse(wasClicked, "A covered target must never report a successful RealInput click.");
    }

    [UnityTest]
    public IEnumerator ClickAsync_UnknownTarget_ReturnsUnknownTargetWithoutDiagnostics()
    {
        QaTargetId targetId = QaTargetId.Create("never.registered");
        var driver = CreateRealInputDriver();

        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.UnknownTarget, task.Result.Code);
        Assert.IsNull(task.Result.Diagnostics);
    }

    [UnityTest]
    public IEnumerator ClickAsync_InputGateClosed_ReturnsInputLayerFailure()
    {
        Button button = CreateButton("GatedTarget", Vector2.zero, new Vector2(100f, 40f));
        QaTargetId targetId = RegisterTarget("kitchen.gated-button", button.gameObject);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver(gateProvider: () => false);
        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        QaInputResult result = task.Result;
        Assert.AreEqual(QaInputResultCode.InputLayerFailure, result.Code);
        Assert.IsNotNull(result.Diagnostics);
        Assert.IsFalse(result.Diagnostics.InputGateOpen);
    }

    [UnityTest]
    public IEnumerator ClickAsync_AlreadyCancelledToken_ReturnsCancelled()
    {
        Button button = CreateButton("CancelTarget", Vector2.zero, new Vector2(100f, 40f));
        QaTargetId targetId = RegisterTarget("kitchen.cancel-button", button.gameObject);
        var driver = CreateRealInputDriver();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task<QaInputResult> task = driver.ClickAsync(targetId, cts.Token);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.Cancelled, task.Result.Code);
    }

    // ---------------------------------------------------------------
    //  Drag fixtures (RealInput)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator DragAsync_VisibleSourceAndDestination_Succeeds()
    {
        Button source = CreateButton("DragSource", new Vector2(-150f, 0f), new Vector2(80f, 80f));
        DragRecorder dragRecorder = source.gameObject.AddComponent<DragRecorder>();

        GameObject destinationGo = new GameObject("DragDestination", typeof(RectTransform));
        destinationGo.transform.SetParent(canvasGo.transform, false);
        ConfigureFixedSizeRect((RectTransform)destinationGo.transform, new Vector2(150f, 0f), new Vector2(80f, 80f));
        destinationGo.AddComponent<Image>();
        DropRecorder dropRecorder = destinationGo.AddComponent<DropRecorder>();

        QaTargetId sourceId = RegisterTarget("study.drag-source", source.gameObject);
        QaTargetId destinationId = RegisterTarget("study.drag-destination", destinationGo);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.DragAsync(sourceId, destinationId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess, task.Result.Message);
        Assert.IsTrue(dragRecorder.BeganDrag, "IBeginDragHandler should have fired.");
        Assert.IsTrue(dragRecorder.Dragged, "IDragHandler should have fired.");
        Assert.IsTrue(dragRecorder.EndedDrag, "IEndDragHandler should have fired.");
        Assert.IsTrue(dropRecorder.Dropped, "IDropHandler on the destination should have fired.");
    }

    [UnityTest]
    public IEnumerator DragAsync_CoveredSource_ReturnsInputLayerFailureAndNeverBeginsDrag()
    {
        Button source = CreateButton("CoveredDragSource", Vector2.zero, new Vector2(80f, 80f));
        DragRecorder dragRecorder = source.gameObject.AddComponent<DragRecorder>();
        CreateCoveringImage("DragBlockingOverlay", Vector2.zero, new Vector2(200f, 200f));

        GameObject destinationGo = new GameObject("DragDestination2", typeof(RectTransform));
        destinationGo.transform.SetParent(canvasGo.transform, false);
        ConfigureFixedSizeRect((RectTransform)destinationGo.transform, new Vector2(150f, 0f), new Vector2(80f, 80f));
        destinationGo.AddComponent<Image>();

        QaTargetId sourceId = RegisterTarget("study.covered-drag-source", source.gameObject);
        QaTargetId destinationId = RegisterTarget("study.drag-destination-2", destinationGo);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.DragAsync(sourceId, destinationId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.InputLayerFailure, task.Result.Code);
        Assert.IsFalse(dragRecorder.BeganDrag, "A covered source must never begin a RealInput drag.");
    }

    [UnityTest]
    public IEnumerator DragAsync_UnknownDestination_ReturnsUnknownTargetForDestination()
    {
        Button source = CreateButton("DragSourceForUnknownDest", Vector2.zero, new Vector2(80f, 80f));
        QaTargetId sourceId = RegisterTarget("study.drag-source-unknown-dest", source.gameObject);
        QaTargetId unknownDestinationId = QaTargetId.Create("study.never-registered-destination");

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.DragAsync(sourceId, unknownDestinationId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.UnknownTarget, task.Result.Code);
        Assert.AreEqual(unknownDestinationId, task.Result.TargetId);
    }

    // ---------------------------------------------------------------
    //  Key fixtures (RealInput)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator KeyAsync_TargetWithKeyReceiver_DeliversText()
    {
        GameObject go = CreateButton("KeyTarget", Vector2.zero, new Vector2(100f, 40f)).gameObject;
        KeyReceiverRecorder receiver = go.AddComponent<KeyReceiverRecorder>();
        QaTargetId targetId = RegisterTarget("tutor.answer-field", go);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.KeyAsync(targetId, "hello", CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess, task.Result.Message);
        Assert.AreEqual("hello", receiver.ReceivedText);
    }

    [UnityTest]
    public IEnumerator KeyAsync_TargetWithoutTextInputComponent_ReturnsUnsupportedInteraction()
    {
        GameObject go = CreateButton("KeyTargetPlain", Vector2.zero, new Vector2(100f, 40f)).gameObject;
        QaTargetId targetId = RegisterTarget("tutor.no-input", go);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.KeyAsync(targetId, "hello", CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.UnsupportedInteraction, task.Result.Code);
    }

    [UnityTest]
    public IEnumerator KeyAsync_DisabledTarget_ReturnsInputLayerFailure()
    {
        Button button = CreateButton("DisabledKeyTarget", Vector2.zero, new Vector2(100f, 40f));
        button.interactable = false;
        button.gameObject.AddComponent<KeyReceiverRecorder>();
        QaTargetId targetId = RegisterTarget("tutor.disabled-input", button.gameObject);
        yield return SettleCanvasAsync();

        var driver = CreateRealInputDriver();
        Task<QaInputResult> task = driver.KeyAsync(targetId, "hello", CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.InputLayerFailure, task.Result.Code);
    }

    // ---------------------------------------------------------------
    //  Api driver (no EventSystem involved)
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator ApiDriver_ClickAsync_KnownTarget_Succeeds()
    {
        var fake = new FakeApiInteractable();
        QaTargetId targetId = QaTargetId.Create("kitchen.sink");
        var driver = new QaApiInputDriver(id => id == targetId ? fake : null);

        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess);
        Assert.AreEqual(QaInteractionMode.Api, task.Result.Mode);
        Assert.AreEqual(1, fake.ClickCount);
    }

    [UnityTest]
    public IEnumerator ApiDriver_ClickAsync_UnknownTarget_ReturnsUnknownTarget()
    {
        var driver = new QaApiInputDriver(_ => null);
        QaTargetId targetId = QaTargetId.Create("never.registered.api");

        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.UnknownTarget, task.Result.Code);
    }

    [UnityTest]
    public IEnumerator ApiDriver_ClickAsync_AdapterReportsFailure_ReturnsApiInteractionFailed()
    {
        var fake = new FakeApiInteractable { ClickShouldSucceed = false, FailureReason = "puzzle locked" };
        QaTargetId targetId = QaTargetId.Create("kitchen.locked");
        var driver = new QaApiInputDriver(_ => fake);

        Task<QaInputResult> task = driver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.ApiInteractionFailed, task.Result.Code);
        Assert.AreEqual("puzzle locked", task.Result.Message);
    }

    [UnityTest]
    public IEnumerator ApiDriver_DragAsync_DelegatesSourceAndDestination()
    {
        var fake = new FakeApiInteractable();
        QaTargetId sourceId = QaTargetId.Create("inventory.key");
        QaTargetId destinationId = QaTargetId.Create("study.lock");
        var driver = new QaApiInputDriver(id => id == sourceId ? fake : null);

        Task<QaInputResult> task = driver.DragAsync(sourceId, destinationId, CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess);
        Assert.AreEqual(destinationId, fake.LastDragDestination);
    }

    [UnityTest]
    public IEnumerator ApiDriver_KeyAsync_DelegatesText()
    {
        var fake = new FakeApiInteractable();
        QaTargetId targetId = QaTargetId.Create("tutor.answer-field-api");
        var driver = new QaApiInputDriver(_ => fake);

        Task<QaInputResult> task = driver.KeyAsync(targetId, "42", CancellationToken.None);
        yield return ToCoroutine(task);

        Assert.IsTrue(task.Result.IsSuccess);
        Assert.AreEqual("42", fake.LastKeyText);
    }

    [UnityTest]
    public IEnumerator ApiDriver_ClickAsync_AlreadyCancelledToken_ReturnsCancelled()
    {
        var fake = new FakeApiInteractable();
        QaTargetId targetId = QaTargetId.Create("kitchen.cancel-api");
        var driver = new QaApiInputDriver(_ => fake);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task<QaInputResult> task = driver.ClickAsync(targetId, cts.Token);
        yield return ToCoroutine(task);

        Assert.AreEqual(QaInputResultCode.Cancelled, task.Result.Code);
        Assert.AreEqual(0, fake.ClickCount);
    }

    // ---------------------------------------------------------------
    //  Classification: API-pass / RealInput-fail on the same covered target
    //  (design doc Task 7 §Step 3 "Classify API-pass/RealInput-fail").
    // ---------------------------------------------------------------

    [UnityTest]
    public IEnumerator SameCoveredTarget_ApiSucceedsButRealInputFailsWithInputLayerFailure()
    {
        Button button = CreateButton("ClassificationTarget", Vector2.zero, new Vector2(100f, 40f));
        QaTargetId targetId = RegisterTarget("kitchen.classification-target", button.gameObject);
        CreateCoveringImage("ClassificationOverlay", Vector2.zero, new Vector2(200f, 200f));
        yield return SettleCanvasAsync();

        var fake = new FakeApiInteractable();
        var apiDriver = new QaApiInputDriver(_ => fake);
        var realInputDriver = CreateRealInputDriver();

        Task<QaInputResult> apiTask = apiDriver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(apiTask);
        Task<QaInputResult> realInputTask = realInputDriver.ClickAsync(targetId, CancellationToken.None);
        yield return ToCoroutine(realInputTask);

        Assert.IsTrue(apiTask.Result.IsSuccess, "API mode bypasses the input layer and should still succeed.");
        Assert.AreEqual(QaInputResultCode.InputLayerFailure, realInputTask.Result.Code,
            "RealInput must fail for a covered target even though API mode succeeded.");
    }

    // ---------------------------------------------------------------
    //  Test doubles
    // ---------------------------------------------------------------

    private sealed class DragRecorder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public bool BeganDrag { get; private set; }
        public bool Dragged { get; private set; }
        public bool EndedDrag { get; private set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeganDrag = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dragged = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndedDrag = true;
        }
    }

    private sealed class DropRecorder : MonoBehaviour, IDropHandler
    {
        public bool Dropped { get; private set; }

        public void OnDrop(PointerEventData eventData)
        {
            Dropped = true;
        }
    }

    private sealed class KeyReceiverRecorder : MonoBehaviour, IQaKeyReceiver
    {
        public string ReceivedText { get; private set; }

        public void OnQaKeyInput(string text)
        {
            ReceivedText = text;
        }
    }

    private sealed class FakeApiInteractable : IQaApiInteractable
    {
        public int ClickCount { get; private set; }
        public bool ClickShouldSucceed { get; set; } = true;
        public string FailureReason { get; set; }
        public QaTargetId LastDragDestination { get; private set; }
        public string LastKeyText { get; private set; }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            ClickCount++;
            error = ClickShouldSucceed ? null : FailureReason;
            return ClickShouldSucceed;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            LastDragDestination = destinationTargetId;
            error = null;
            return true;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            LastKeyText = text;
            error = null;
            return true;
        }
    }
}
