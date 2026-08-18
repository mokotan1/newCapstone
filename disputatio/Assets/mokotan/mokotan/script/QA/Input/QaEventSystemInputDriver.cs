#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Godlotto.QA.Input
{
    /// <summary>
    /// <see cref="QaEventSystemInputDriver.KeyAsync"/>가 텍스트를 전달할 수 있는, 실제 텍스트
    /// 입력 컴포넌트가 아닌 커스텀 대상을 위한 선택적 확장점. <see cref="UnityEngine.UI.InputField"/>가
    /// 없는 대상(예: 커스텀 다이얼로그 입력 위젯)이 RealInput 키 입력을 받으려면 이 인터페이스를
    /// 구현하면 됩니다.
    /// </summary>
    public interface IQaKeyReceiver
    {
        void OnQaKeyInput(string text);
    }

    /// <summary>
    /// <see cref="QaInteractionMode.RealInput"/> 구현(Task 7). 실제
    /// <c>UnityEngine.EventSystems.EventSystem</c>·<c>GraphicRaycaster</c> 경로를 통해 포인터
    /// 이벤트를 주입하므로, API 모드에서는 보이지 않는 "가려짐"·"비활성"·"입력 게이트 차단"
    /// 같은 화면상 문제를 그대로 재현합니다. 대상 ID → <c>GameObject</c> 해석은 씬 배선 방식에
    /// 대한 지식이 필요하므로 생성자로 주입받는 콜백에 위임합니다(DIP: 이 드라이버는 특정
    /// 어댑터 구현이 아니라 콜백 계약에만 의존). 입력 게이트 스냅샷도 마찬가지로 콜백으로만
    /// 얻으며, 특정 게이트 구현(예: <c>InteractionInputGate</c>)에 직접 링크하지 않습니다.
    /// </summary>
    public sealed class QaEventSystemInputDriver : IQaInputDriver
    {
        private readonly EventSystem eventSystem;
        private readonly Func<QaTargetId, GameObject> resolveTargetGameObject;
        private readonly Func<bool> isInputGateOpenProvider;

        /// <param name="eventSystem">레이캐스트/선택에 사용할 활성 EventSystem.</param>
        /// <param name="resolveTargetGameObject">
        /// 안정적 <see cref="QaTargetId"/>를 실제 씬의 <see cref="GameObject"/>로 해석하는 콜백.
        /// 알 수 없는 대상이면 <c>null</c>을 반환해야 합니다.
        /// </param>
        /// <param name="isInputGateOpenProvider">
        /// 시도 시점에 전역 입력 게이트가 열려 있는지 스냅샷으로 알려주는 콜백. 생략하면
        /// 항상 열려 있는 것으로 취급합니다.
        /// </param>
        public QaEventSystemInputDriver(
            EventSystem eventSystem,
            Func<QaTargetId, GameObject> resolveTargetGameObject,
            Func<bool> isInputGateOpenProvider = null)
        {
            this.eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            this.resolveTargetGameObject = resolveTargetGameObject
                ?? throw new ArgumentNullException(nameof(resolveTargetGameObject));
            this.isInputGateOpenProvider = isInputGateOpenProvider ?? (() => true);
        }

        public QaInteractionMode Mode
        {
            get { return QaInteractionMode.RealInput; }
        }

        public async Task<QaInputResult> ClickAsync(QaTargetId targetId, CancellationToken cancellationToken)
        {
            if (targetId.IsNone)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.InvalidArgument,
                    "targetId must not be QaTargetId.None.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.Cancelled,
                    "Command was cancelled before execution.");
            }

            bool gateOpen = SafeGateSnapshot();
            GameObject target = SafeResolve(targetId, out QaInputResult resolveError);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (target == null)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.UnknownTarget,
                    "Target GameObject could not be resolved.");
            }

            var raycastResults = new List<RaycastResult>();
            PointerEventData pointerEventData = BuildPointerEventData(target);
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            QaInputLayerDiagnostics diagnostics = BuildDiagnostics(target, raycastResults, gateOpen);
            QaInputResult blocked = CheckLayerBlockers(targetId, diagnostics);
            if (blocked != null)
            {
                return blocked;
            }

            RaycastResult targetHit = FindRaycastResultFor(raycastResults, target);
            pointerEventData.pointerPressRaycast = targetHit;
            pointerEventData.pointerCurrentRaycast = targetHit;
            pointerEventData.pointerPress = target;
            pointerEventData.rawPointerPress = target;

            // 조건 기반 완료(디자인 문서 §Step 2): ExecuteEvents.Execute는 동기 호출이며 핸들러
            // 실행이 끝난 뒤에만 반환하므로, "이벤트 수신"은 고정 sleep 없이도 이미 보장됩니다.
            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointerEventData, ExecuteEvents.pointerClickHandler);

            await SettleFrameAsync().ConfigureAwait(true);

            return QaInputResult.Success(targetId, Mode, "Click dispatched via EventSystem.");
        }

        public async Task<QaInputResult> DragAsync(
            QaTargetId sourceTargetId,
            QaTargetId destinationTargetId,
            CancellationToken cancellationToken)
        {
            if (sourceTargetId.IsNone)
            {
                return QaInputResult.Failure(sourceTargetId, Mode, QaInputResultCode.InvalidArgument,
                    "sourceTargetId must not be QaTargetId.None.");
            }

            if (destinationTargetId.IsNone)
            {
                return QaInputResult.Failure(sourceTargetId, Mode, QaInputResultCode.InvalidArgument,
                    "destinationTargetId must not be QaTargetId.None.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return QaInputResult.Failure(sourceTargetId, Mode, QaInputResultCode.Cancelled,
                    "Command was cancelled before execution.");
            }

            bool gateOpen = SafeGateSnapshot();

            GameObject source = SafeResolve(sourceTargetId, out QaInputResult sourceResolveError);
            if (sourceResolveError != null)
            {
                return sourceResolveError;
            }

            if (source == null)
            {
                return QaInputResult.Failure(sourceTargetId, Mode, QaInputResultCode.UnknownTarget,
                    "Source target GameObject could not be resolved.");
            }

            GameObject destination = SafeResolve(destinationTargetId, out QaInputResult destinationResolveError);
            if (destinationResolveError != null)
            {
                return destinationResolveError;
            }

            if (destination == null)
            {
                return QaInputResult.Failure(destinationTargetId, Mode, QaInputResultCode.UnknownTarget,
                    "Destination target '" + destinationTargetId + "' could not be resolved.");
            }

            var sourceRaycastResults = new List<RaycastResult>();
            PointerEventData pointerEventData = BuildPointerEventData(source);
            eventSystem.RaycastAll(pointerEventData, sourceRaycastResults);

            QaInputLayerDiagnostics diagnostics = BuildDiagnostics(source, sourceRaycastResults, gateOpen);
            QaInputResult blocked = CheckLayerBlockers(sourceTargetId, diagnostics);
            if (blocked != null)
            {
                return blocked;
            }

            pointerEventData.pointerPress = source;
            pointerEventData.pointerDrag = source;
            pointerEventData.pointerPressRaycast = FindRaycastResultFor(sourceRaycastResults, source);

            ExecuteEvents.Execute(source, pointerEventData, ExecuteEvents.beginDragHandler);

            pointerEventData.position = GetScreenPoint(destination);
            var destinationRaycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, destinationRaycastResults);
            pointerEventData.pointerCurrentRaycast = FindRaycastResultFor(destinationRaycastResults, destination);

            ExecuteEvents.Execute(source, pointerEventData, ExecuteEvents.dragHandler);

            GameObject dropTarget = pointerEventData.pointerCurrentRaycast.gameObject ?? destination;
            ExecuteEvents.ExecuteHierarchy(dropTarget, pointerEventData, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(source, pointerEventData, ExecuteEvents.endDragHandler);

            await SettleFrameAsync().ConfigureAwait(true);

            return QaInputResult.Success(sourceTargetId, Mode,
                "Drag dispatched via EventSystem to '" + destinationTargetId + "'.");
        }

        public async Task<QaInputResult> KeyAsync(QaTargetId targetId, string text, CancellationToken cancellationToken)
        {
            if (targetId.IsNone)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.InvalidArgument,
                    "targetId must not be QaTargetId.None.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.Cancelled,
                    "Command was cancelled before execution.");
            }

            bool gateOpen = SafeGateSnapshot();
            GameObject target = SafeResolve(targetId, out QaInputResult resolveError);
            if (resolveError != null)
            {
                return resolveError;
            }

            if (target == null)
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.UnknownTarget,
                    "Target GameObject could not be resolved.");
            }

            var raycastResults = new List<RaycastResult>();
            PointerEventData pointerEventData = BuildPointerEventData(target);
            eventSystem.RaycastAll(pointerEventData, raycastResults);

            QaInputLayerDiagnostics diagnostics = BuildDiagnostics(target, raycastResults, gateOpen);
            QaInputResult blocked = CheckLayerBlockers(targetId, diagnostics);
            if (blocked != null)
            {
                return blocked;
            }

            eventSystem.SetSelectedGameObject(target, pointerEventData);

            IQaKeyReceiver keyReceiver = target.GetComponent<IQaKeyReceiver>();
            InputField legacyInputField = target.GetComponent<InputField>();

            if (keyReceiver != null)
            {
                keyReceiver.OnQaKeyInput(text ?? string.Empty);
            }
            else if (legacyInputField != null)
            {
                legacyInputField.text = (legacyInputField.text ?? string.Empty) + (text ?? string.Empty);
                legacyInputField.onValueChanged.Invoke(legacyInputField.text);
            }
            else
            {
                return QaInputResult.Failure(targetId, Mode, QaInputResultCode.UnsupportedInteraction,
                    "Target '" + targetId + "' has no recognized text-input component " +
                    "(IQaKeyReceiver or UnityEngine.UI.InputField).");
            }

            await SettleFrameAsync().ConfigureAwait(true);

            return QaInputResult.Success(targetId, Mode, "Key input dispatched via EventSystem selection.");
        }

        // -----------------------------------------------------------------------------------
        //  Internal helpers
        // -----------------------------------------------------------------------------------

        private GameObject SafeResolve(QaTargetId targetId, out QaInputResult errorResult)
        {
            try
            {
                errorResult = null;
                return resolveTargetGameObject(targetId);
            }
            catch (Exception ex)
            {
                errorResult = QaInputResult.Failure(targetId, Mode, QaInputResultCode.InternalError,
                    SanitizeExceptionMessage(ex));
                return null;
            }
        }

        private bool SafeGateSnapshot()
        {
            try
            {
                return isInputGateOpenProvider();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QaEventSystemInputDriver] Input gate snapshot provider threw: " + ex.GetType().Name);
                return true;
            }
        }

        private QaInputResult CheckLayerBlockers(QaTargetId targetId, QaInputLayerDiagnostics diagnostics)
        {
            if (!diagnostics.InputGateOpen)
            {
                return QaInputResult.LayerFailure(targetId, Mode,
                    "Global input gate is blocked (e.g. dialogue or scene transition in progress).", diagnostics);
            }

            if (!diagnostics.TargetInteractable)
            {
                return QaInputResult.LayerFailure(targetId, Mode,
                    "Target is not interactable (Selectable.interactable == false, or a blocking CanvasGroup).",
                    diagnostics);
            }

            if (!diagnostics.RaycastHitTarget)
            {
                return QaInputResult.LayerFailure(targetId, Mode,
                    "Target is covered by another UI element or not hit by the raycast.", diagnostics);
            }

            return null;
        }

        private PointerEventData BuildPointerEventData(GameObject target)
        {
            return new PointerEventData(eventSystem)
            {
                pointerId = -1,
                position = GetScreenPoint(target),
                button = PointerEventData.InputButton.Left
            };
        }

        private static Vector2 GetScreenPoint(GameObject go)
        {
            var rectTransform = go.transform as RectTransform;
            Vector3 worldPosition = rectTransform != null ? rectTransform.position : go.transform.position;

            Canvas canvas = go.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
        }

        private static QaInputLayerDiagnostics BuildDiagnostics(
            GameObject target, List<RaycastResult> raycastResults, bool gateOpen)
        {
            bool interactable = true;
            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null)
            {
                interactable = selectable.IsInteractable();
            }

            var hitNames = new List<string>(raycastResults.Count);
            int topHitSortingOrder = 0;
            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hitGo = raycastResults[i].gameObject;
                hitNames.Add(hitGo != null ? hitGo.name : "(null)");
                if (i == 0)
                {
                    topHitSortingOrder = raycastResults[i].sortingOrder;
                }
            }

            // 대상이 레이캐스트 결과 목록에 있는지가 아니라, "가장 위" 히트인지가 중요합니다.
            // 목록에는 있지만 맨 위가 아니면 다른 무언가가 대상을 가리고 있다는 뜻입니다.
            bool targetIsTopHit = raycastResults.Count > 0 && raycastResults[0].gameObject == target;

            int targetSortingOrder = 0;
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            if (targetCanvas != null)
            {
                targetSortingOrder = targetCanvas.sortingOrder;
            }

            string details = targetIsTopHit
                ? "Target is the topmost raycast hit."
                : "Target is not the topmost raycast hit; something else may be covering it.";

            return QaInputLayerDiagnostics.Create(
                targetFound: true,
                targetInteractable: interactable,
                raycastHitTarget: targetIsTopHit,
                raycastHitNames: hitNames,
                topHitSortingOrder: topHitSortingOrder,
                targetSortingOrder: targetSortingOrder,
                inputGateOpen: gateOpen,
                details: details);
        }

        private static RaycastResult FindRaycastResultFor(List<RaycastResult> results, GameObject go)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].gameObject == go)
                {
                    return results[i];
                }
            }

            return default;
        }

        private static async Task SettleFrameAsync()
        {
            // 고정 시간 sleep이 아니라 실제 Unity 프레임 펌프에 한 번만 양보합니다. 이벤트
            // 수신 자체는 위의 동기 ExecuteEvents.Execute 호출로 이미 보장되었으므로, 이 양보는
            // 그 이벤트에 반응하는 Update()/코루틴 기반 상태 변화가 반영될 여유만 줍니다.
            await Task.Yield();
        }

        private static string SanitizeExceptionMessage(Exception exception)
        {
            return "Internal QA EventSystem input driver error (" + exception.GetType().Name +
                "). See server logs for details.";
        }
    }
}
#endif
