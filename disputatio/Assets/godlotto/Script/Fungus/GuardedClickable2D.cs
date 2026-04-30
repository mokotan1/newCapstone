using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Fungus;

namespace Godlotto.FungusIntegration
{
    /// <summary>
    /// 연타·중복 포인터 이벤트로 ObjectClicked가 여러 번 큐에 쌓이는 것을 줄입니다.
    /// Fungus <see cref="Clickable2D"/>는 레거시 OnMouseDown에서
    /// <see cref="EventSystem.IsPointerOverGameObject"/>가 true면 무조건 막는데,
    /// 전체 화면 UI(투명 Image 등)만 있어도 true가 되어 문 클릭이 전부 씹힐 수 있습니다.
    /// 마우스 아래 2D 물리에 이 오브젝트의 콜라이더가 있으면 그때는 클릭을 허용합니다.
    /// </summary>
    public class GuardedClickable2D : Clickable2D
    {
        const float GameplayPlaneZ = 0f;
        const float OverlapSlopWorld = 0.12f;

        [SerializeField] float cooldownSeconds = 0.35f;

        [Tooltip("true면 포인터가 UI 위라고 나와도, 월드 2D 콜라이더가 마우스 아래에 있으면 클릭을 받습니다.")]
        [SerializeField] bool allowClickWhenWorldColliderUnderPointer = true;

        float lastClickUnscaledTime = float.NegativeInfinity;

        Collider2D[] cachedChildColliders;
        readonly Dictionary<Collider2D, Clickable2D> clickableInParentByCollider = new Dictionary<Collider2D, Clickable2D>();
        bool colliderHierarchyDirty = true;

        /// <summary>
        /// Unity <see cref="OnMouseDown"/>는 투명 스프라이트·UI 오버레이·멀티 카메라 조합에서 누락되는 경우가 있어
        /// <see cref="Update"/>에서 처리합니다. (Input System + 레거시 입력 모두 지원)
        /// </summary>
        protected override void OnMouseDown()
        {
            // 의도적으로 비움 — 클릭 로직은 Update에서만 처리합니다.
        }

        void OnEnable()
        {
            colliderHierarchyDirty = true;
        }

        void OnTransformChildrenChanged()
        {
            colliderHierarchyDirty = true;
        }

        void Update()
        {
            if (useEventSystem)
            {
                return;
            }

            if (!clickEnabled)
            {
                return;
            }

            if (!TryGetPrimaryPressAndScreenPoint(out int pointerId, out Vector2 screenPosition))
            {
                return;
            }

            TryProcessClickFromScreen(pointerId, screenPosition);
        }

        static bool TryGetPrimaryPressAndScreenPoint(out int pointerId, out Vector2 screenPosition)
        {
            pointerId = -1;
            screenPosition = default;

#if ENABLE_INPUT_SYSTEM
            var inputSysMouse = UnityEngine.InputSystem.Mouse.current;
            if (inputSysMouse != null && inputSysMouse.leftButton.wasPressedThisFrame)
            {
                pointerId = -1;
                screenPosition = inputSysMouse.position.ReadValue();
                return true;
            }

            var inputSysTouch = UnityEngine.InputSystem.Touchscreen.current;
            if (inputSysTouch != null && inputSysTouch.primaryTouch.press.wasPressedThisFrame)
            {
                pointerId = inputSysTouch.primaryTouch.touchId.ReadValue();
                screenPosition = inputSysTouch.primaryTouch.position.ReadValue();
                return true;
            }
#endif
            // Legacy 마우스를 touchCount 블록보다 먼저: touch가 잡히는데 phase가 Began이 아닐 때
            // (터치 노트북·잔여 포인터 등) 기존 코드가 return false로 마우스 검사를 건너뛰던 문제 방지.
            if (Input.GetMouseButtonDown(0))
            {
                pointerId = -1;
                screenPosition = Input.mousePosition;
                return true;
            }

            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began)
                {
                    pointerId = t.fingerId;
                    screenPosition = t.position;
                    return true;
                }
            }

            return false;
        }

        void TryProcessClickFromScreen(int pointerId, Vector2 screenPosition)
        {
            if (!IsOurCollider2DUnderPointer(screenPosition))
            {
                return;
            }

            if (EventSystem.current != null && IsPointerOverUGUI(pointerId))
            {
                if (!allowClickWhenWorldColliderUnderPointer)
                {
                    return;
                }
            }

            DoPointerClick();
        }

        static bool IsPointerOverUGUI(int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                return false;
            }

            if (pointerId >= 0)
            {
                return es.IsPointerOverGameObject(pointerId);
            }

            return es.IsPointerOverGameObject();
        }

        void EnsureColliderAndClickableCache()
        {
            if (!colliderHierarchyDirty)
            {
                return;
            }

            cachedChildColliders = GetComponentsInChildren<Collider2D>(true);
            clickableInParentByCollider.Clear();
            colliderHierarchyDirty = false;
        }

        Clickable2D GetCachedClickableInParent(Collider2D c)
        {
            if (clickableInParentByCollider.TryGetValue(c, out var cached) && cached != null)
            {
                return cached;
            }

            var resolved = c.GetComponentInParent<Clickable2D>();
            clickableInParentByCollider[c] = resolved;
            return resolved;
        }

        bool IsOurCollider2DUnderPointer(Vector2 screenPosition)
        {
            EnsureColliderAndClickableCache();
            Vector2 world = ScreenToWorldOnGameplayPlane(screenPosition);
            if (cachedChildColliders == null)
            {
                return false;
            }

            foreach (Collider2D c in cachedChildColliders)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!ColliderContainsWorldPoint(c, world))
                {
                    continue;
                }

                if (GetCachedClickableInParent(c) == this)
                {
                    return true;
                }
            }

            return false;
        }

        static bool ColliderContainsWorldPoint(Collider2D c, Vector2 world)
        {
            if (c.OverlapPoint(world))
            {
                return true;
            }

            foreach (Collider2D hit in Physics2D.OverlapCircleAll(world, OverlapSlopWorld, ~0))
            {
                if (hit == c)
                {
                    return true;
                }
            }

            return false;
        }

        static Vector2 ScreenToWorldOnGameplayPlane(Vector2 screenPosition)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return Vector2.zero;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (Mathf.Abs(ray.direction.z) > 1e-5f)
            {
                float t = (GameplayPlaneZ - ray.origin.z) / ray.direction.z;
                Vector3 p = ray.GetPoint(t);
                return new Vector2(p.x, p.y);
            }

            Vector3 fallback = cam.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                Mathf.Abs(cam.transform.position.z)));
            return fallback;
        }

        protected override void DoPointerClick()
        {
            // 전역 잠금을 먼저 확인해 쿨다운 타이머를 소모하지 않음
            if (InteractionLock.IsLocked)
                return;

            float t = Time.unscaledTime;
            if (t - lastClickUnscaledTime < cooldownSeconds)
                return;

            lastClickUnscaledTime = t;
            base.DoPointerClick();
        }
    }
}
