using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.ModalInput
{
    /// <summary>
    /// 모달 패널 뒤에 투명한 full-screen raycast 차단 Image 를 만들고 제거하는 공용 헬퍼.
    /// 체셔 패널(<see cref="ModalInputScope"/>)과 책/설정/로그 같은 오버레이 패널이 공통으로 사용해
    /// EventSystem 레벨에서 패널 밖 UI 클릭을 소비합니다(중복 구현 제거).
    /// </summary>
    public static class ModalRaycastBlocker
    {
        public const string DefaultName = "ModalInputBlocker (auto)";

        /// <summary>
        /// 패널과 같은 부모의 "패널보다 뒤(아래) sibling" 위치에 투명 raycast Image 를 생성합니다.
        /// 패널 내부 콘텐츠/버튼은 차단막보다 앞이라 정상 동작하고, 화면의 나머지 UI 클릭만 소비됩니다.
        /// </summary>
        public static Image Create(Transform panel, string blockerName = DefaultName)
        {
            if (panel == null)
                return null;

            Transform parent = panel.parent;

            var blockerGo = new GameObject(
                blockerName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            RectTransform rect = blockerGo.GetComponent<RectTransform>();

            if (parent != null)
            {
                rect.SetParent(parent, false);
                rect.SetSiblingIndex(panel.GetSiblingIndex());
            }
            else
            {
                rect.SetParent(panel, false);
                rect.SetAsFirstSibling();
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = blockerGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            return image;
        }

        /// <summary>차단막을 파괴합니다. null 은 안전하게 무시합니다.</summary>
        public static void Remove(Image blocker)
        {
            if (blocker == null)
                return;

            GameObject go = blocker.gameObject;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}
