// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fungus
{
    /// <summary>
    /// Detects mouse clicks and touches on a Game Object, and sends an event to all Flowchart event handlers in the scene.
    /// The Game Object must have a Collider or Collider2D component attached.
    /// Use in conjunction with the ObjectClicked Flowchart event handler.
    /// </summary>
    public class Clickable2D : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Is object clicking enabled")]
        [SerializeField] protected bool clickEnabled = true;

        [Tooltip("Mouse texture to use when hovering mouse over object")]
        [SerializeField] protected Texture2D hoverCursor;

        [Tooltip("Use the UI Event System to check for clicks. Clicks that hit an overlapping UI object will be ignored. Camera must have a PhysicsRaycaster component, or a Physics2DRaycaster for 2D colliders.")]
        [SerializeField] protected bool useEventSystem;

        protected virtual void ChangeCursor(Texture2D cursorTexture)
        {
            if (!clickEnabled)
            {
                return;
            }

            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        }

        protected virtual void DoPointerClick()
        {
            if (!clickEnabled)
                return;

            // 모달 UI(설정·로그·체셔/튜터 패널·책·지도 등)가 열려 있고
            // 이 오브젝트가 허용 루트 밖이면, FungusManager 접근 전에 즉시 무시합니다.
            if (ShouldBlockWorldClick(gameObject))
                return;

            if (IsModalSayDialogOpen())
                return;

            if (InteractionLock.IsLocked)
                return;

            InteractionLock.AcquireForClick(this);

            var eventDispatcher = FungusManager.Instance.EventDispatcher;
            eventDispatcher.Raise(new ObjectClicked.ObjectClickedEvent(this));
            InteractionLock.OnRaiseCompletedForWorldClick();
        }

        protected virtual void DoPointerEnter()
        {
            ChangeCursor(hoverCursor);
        }

        protected virtual void DoPointerExit()
        {
            // Always reset the mouse cursor to be on the safe side
            SetMouseCursor.ResetMouseCursor();
        }

        #region Legacy OnMouseX methods

        protected virtual void OnMouseDown()
        {
            if (useEventSystem) return;

            // UI 레이어가 포인터를 가로막아도, 이 오브젝트의 2D 콜라이더가
            // 포인터 아래에 있으면 클릭을 허용합니다.
            // (전체화면 투명 UI에 의한 클릭 차단 문제 방지)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (!IsOurColliderUnderPointer())
                    return;

                // 콜라이더가 포인터 아래 있더라도, 그 위에 진짜 인터랙티브 UI
                // (뒤로가기 버튼 등 Selectable)가 있으면 클릭은 UI 차지입니다.
                // 투명 차단 UI에는 Selectable이 없으므로 위 통과 로직은 그대로 유지됩니다.
                if (IsInteractiveUiUnderPointer(Input.mousePosition))
                    return;
            }

            DoPointerClick();
        }

        protected virtual bool IsOurColliderUnderPointer()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);
            var colliders = GetComponentsInChildren<Collider2D>(false);
            foreach (var c in colliders)
                if (c != null && c.enabled && c.OverlapPoint(world))
                    return true;
            return false;
        }

        static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        /// <summary>
        /// 지정한 화면 좌표 위에 '실제로 상호작용하는 UI 컨트롤'(Button 등 Selectable)이
        /// 올라와 있는지 검사합니다. Selectable이 없는 투명 차단용 UI는 false로 처리해
        /// 월드 클릭이 그대로 통과하도록 둡니다.
        /// 뒤로가기 버튼처럼 진짜 UI 버튼 위에서의 클릭이 아래 월드 오브젝트로 새는 것을 막는 용도입니다.
        /// </summary>
        public static bool IsInteractiveUiUnderPointer(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
            uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, uiRaycastResults);
            bool blocked = ContainsInteractiveUi(uiRaycastResults);
            uiRaycastResults.Clear();
            return blocked;
        }

        /// <summary>
        /// UI 레이캐스트 결과 중 상호작용 가능한 Selectable(버튼 등)이 하나라도 있으면 true.
        /// </summary>
        public static bool ContainsInteractiveUi(List<RaycastResult> raycastResults)
        {
            if (raycastResults == null)
                return false;

            for (int i = 0; i < raycastResults.Count; i++)
            {
                GameObject hit = raycastResults[i].gameObject;
                if (hit == null)
                    continue;

                Selectable selectable = hit.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable())
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 모달 UI가 열려 있고, 클릭 대상이 그 모달의 허용 루트(allowedRoot) 밖이면 true.
        /// 방마다 콜라이더를 끄는 대신 <see cref="ModalInputGate"/> 한 곳에서 판정합니다.
        /// </summary>
        public static bool ShouldBlockWorldClick(GameObject target)
        {
            return ModalInputGate.IsBlockingWorldInput && !ModalInputGate.IsAllowed(target);
        }

        /// <summary>
        /// SayDialog is modal while it is visible or waiting for player input, so world clicks
        /// behind it must not trigger ObjectClicked handlers.
        /// </summary>
        public static bool IsModalSayDialogOpen()
        {
            if (IsBlockingSayDialog(SayDialog.ActiveSayDialog))
                return true;

#if UNITY_2023_1_OR_NEWER
            var dialogs = Object.FindObjectsByType<SayDialog>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            var dialogs = Object.FindObjectsOfType<SayDialog>();
#endif
            for (int i = 0; i < dialogs.Length; i++)
            {
                if (IsBlockingSayDialog(dialogs[i]))
                    return true;
            }

            return false;
        }

        static bool IsBlockingSayDialog(SayDialog dialog)
        {
            if (dialog == null || !dialog.gameObject.activeInHierarchy)
                return false;

            Writer writer = dialog.GetComponent<Writer>();
            if (writer != null && (writer.IsWriting || writer.IsWaitingForInput))
                return true;

            CanvasGroup group = dialog.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.01f;
        }

        protected virtual void OnMouseEnter()
        {
            if (!useEventSystem)
            {
                DoPointerEnter();
            }
        }

        protected virtual void OnMouseExit()
        {
            if (!useEventSystem)
            {
                DoPointerExit();
            }
        }

        #endregion

        #region Public members

        /// <summary>
        /// Is object clicking enabled.
        /// </summary>
        public bool ClickEnabled { set { clickEnabled = value; } }

        #endregion

        #region IPointerClickHandler implementation

        public void OnPointerClick(PointerEventData eventData)
        {
            if (useEventSystem)
            {
                DoPointerClick();
            }
        }

        #endregion

        #region IPointerEnterHandler implementation

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (useEventSystem)
            {
                DoPointerEnter();
            }
        }

        #endregion

        #region IPointerExitHandler implementation

        public void OnPointerExit(PointerEventData eventData)
        {
            if (useEventSystem)
            {
                DoPointerExit();
            }
        }

        #endregion
    }
}
