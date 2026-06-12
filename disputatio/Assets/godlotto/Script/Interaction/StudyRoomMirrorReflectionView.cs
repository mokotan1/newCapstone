using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom CardStackPanel 전용 — FilterCardImage 위에만 런타임 거울 오버레이를 추가한다.
    /// FilterCard.png / FilterCard.asset 은 변경하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StudyRoomMirrorReflectionView : MonoBehaviour
    {
        const string OverlayRootName = "StudyRoomMirrorOverlay";
        const string ViewportName = "MirrorViewport";
        const string ReflectionName = "ReflectedWordImage";
        const string FrameRootName = "MirrorFrame";
        const string GlintName = "MirrorGlint";
        const float HalfWordFillAmount = 0.5f;

        [Header("References")]
        [SerializeField] Image sourceWordImage;
        [SerializeField] RectTransform mirrorCardRect;
        [SerializeField] RectTransform mirrorViewportRect;
        [SerializeField] Image reflectedWordImage;
        [SerializeField] RectTransform overlayRootRect;
        [SerializeField] Image mirrorGlintImage;

        [Header("Half Word")]
        [SerializeField] bool showLeftHalf = true;

        [Header("Mirror Chrome")]
        [SerializeField] float frameThickness = 8f;
        [SerializeField] Color frameColor = new Color(0.74f, 0.78f, 0.84f, 0.95f);
        [SerializeField] Color glintColor = new Color(0.86f, 0.93f, 1f, 0.22f);
        [SerializeField] float glintRotationDegrees = -38f;
        [SerializeField] Vector2 glintSize = new Vector2(220f, 36f);

        RectTransform sourceWordRect;
        static Sprite uiSprite;

        public bool IsOverlayActive => overlayRootRect != null && overlayRootRect.gameObject.activeSelf;

        public void Configure(
            Image wordImage,
            RectTransform mirrorCard,
            RectTransform viewport,
            Image reflectedImage)
        {
            sourceWordImage = wordImage;
            mirrorCardRect = mirrorCard;
            mirrorViewportRect = viewport;
            reflectedWordImage = reflectedImage;
            sourceWordRect = wordImage != null ? wordImage.rectTransform : null;
            ApplyHalfWordDisplay();
            SyncReflectionSprite();
        }

        void Awake()
        {
            if (sourceWordImage != null)
                sourceWordRect = sourceWordImage.rectTransform;
        }

        /// <summary>StudyRoom 거울 퍼즐 활성화 시에만 호출 — 오버레이 UI를 생성·갱신한다.</summary>
        public void EnsureStudyRoomMirrorOverlay()
        {
            if (mirrorCardRect == null)
                return;

            ResolveOverlayRoot();
            ResolveMirrorViewport();
            ResolveReflectedWordImage();
            ResolveMirrorFrame();
            ResolveMirrorGlint();
            SyncReflectionSprite();
            SetMirrorOverlayActive(false);
        }

        /// <summary>거울 퍼즐이 켜질 때만 true. 다른 씬·FilterCard 사용처에는 호출되지 않는다.</summary>
        public void SetMirrorOverlayActive(bool active)
        {
            if (overlayRootRect == null && active)
                EnsureStudyRoomMirrorOverlay();

            if (overlayRootRect != null)
                overlayRootRect.gameObject.SetActive(active);

            if (active)
                SyncReflectionSprite();
        }

        public void ApplyHalfWordDisplay()
        {
            if (sourceWordImage == null)
                return;

            sourceWordImage.type = Image.Type.Filled;
            sourceWordImage.fillMethod = Image.FillMethod.Horizontal;
            sourceWordImage.fillOrigin = showLeftHalf
                ? (int)Image.OriginHorizontal.Left
                : (int)Image.OriginHorizontal.Right;
            sourceWordImage.fillAmount = HalfWordFillAmount;
        }

        public void SyncReflectionSprite()
        {
            if (sourceWordImage == null || reflectedWordImage == null)
                return;

            reflectedWordImage.sprite = sourceWordImage.sprite;
            reflectedWordImage.color = sourceWordImage.color;
            reflectedWordImage.preserveAspect = sourceWordImage.preserveAspect;
            reflectedWordImage.type = Image.Type.Simple;
            reflectedWordImage.fillAmount = 1f;

            RectTransform reflectedRect = reflectedWordImage.rectTransform;
            if (sourceWordRect == null || reflectedRect == null)
                return;

            reflectedRect.anchorMin = new Vector2(0.5f, 0.5f);
            reflectedRect.anchorMax = new Vector2(0.5f, 0.5f);
            reflectedRect.pivot = new Vector2(0.5f, 0.5f);
            reflectedRect.sizeDelta = sourceWordRect.sizeDelta;
            reflectedRect.anchoredPosition = Vector2.zero;
            reflectedRect.localScale = new Vector3(-1f, 1f, 1f);
        }

        void ResolveOverlayRoot()
        {
            if (overlayRootRect != null)
                return;

            Transform existing = mirrorCardRect.Find(OverlayRootName);
            if (existing != null)
            {
                overlayRootRect = existing as RectTransform;
                return;
            }

            var overlayObject = new GameObject(OverlayRootName, typeof(RectTransform));
            overlayRootRect = overlayObject.GetComponent<RectTransform>();
            overlayRootRect.SetParent(mirrorCardRect, false);
            StretchFull(overlayRootRect);
            overlayRootRect.SetAsFirstSibling();
        }

        void ResolveMirrorViewport()
        {
            if (mirrorViewportRect != null)
                return;

            Transform existing = overlayRootRect.Find(ViewportName);
            if (existing != null)
            {
                mirrorViewportRect = existing as RectTransform;
                return;
            }

            var viewportObject = new GameObject(ViewportName, typeof(RectTransform), typeof(RectMask2D));
            mirrorViewportRect = viewportObject.GetComponent<RectTransform>();
            mirrorViewportRect.SetParent(overlayRootRect, false);
            ApplyViewportInsets(mirrorViewportRect);
        }

        void ResolveReflectedWordImage()
        {
            if (reflectedWordImage != null)
                return;

            Transform existing = mirrorViewportRect.Find(ReflectionName);
            if (existing != null)
            {
                reflectedWordImage = existing.GetComponent<Image>();
                return;
            }

            var reflectionObject = new GameObject(
                ReflectionName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            reflectionObject.transform.SetParent(mirrorViewportRect, false);
            reflectedWordImage = reflectionObject.GetComponent<Image>();
            reflectedWordImage.raycastTarget = false;
        }

        void ResolveMirrorFrame()
        {
            Transform frameRoot = overlayRootRect.Find(FrameRootName);
            if (frameRoot == null)
            {
                var frameObject = new GameObject(FrameRootName, typeof(RectTransform));
                frameRoot = frameObject.transform;
                frameRoot.SetParent(overlayRootRect, false);
                StretchFull(frameRoot as RectTransform);
            }

            EnsureFrameBar(frameRoot, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -frameThickness), new Vector2(0f, 0f));
            EnsureFrameBar(frameRoot, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, frameThickness));
            EnsureFrameBar(frameRoot, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(frameThickness, 0f));
            EnsureFrameBar(frameRoot, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-frameThickness, 0f), new Vector2(0f, 0f));
            frameRoot.SetAsLastSibling();
        }

        void ResolveMirrorGlint()
        {
            if (mirrorGlintImage != null)
                return;

            Transform existing = mirrorViewportRect.Find(GlintName);
            if (existing != null)
            {
                mirrorGlintImage = existing.GetComponent<Image>();
                ApplyGlintStyle(mirrorGlintImage.rectTransform, mirrorGlintImage);
                return;
            }

            var glintObject = new GameObject(
                GlintName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            glintObject.transform.SetParent(mirrorViewportRect, false);
            mirrorGlintImage = glintObject.GetComponent<Image>();
            mirrorGlintImage.raycastTarget = false;
            ApplyGlintStyle(mirrorGlintImage.rectTransform, mirrorGlintImage);
            glintObject.transform.SetAsLastSibling();
        }

        void EnsureFrameBar(
            Transform frameRoot,
            string barName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            Transform bar = frameRoot.Find(barName);
            RectTransform barRect;
            Image barImage;

            if (bar == null)
            {
                var barObject = new GameObject(barName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bar = barObject.transform;
                bar.SetParent(frameRoot, false);
                barRect = barObject.GetComponent<RectTransform>();
                barImage = barObject.GetComponent<Image>();
            }
            else
            {
                barRect = bar as RectTransform;
                barImage = bar.GetComponent<Image>();
            }

            barRect.anchorMin = anchorMin;
            barRect.anchorMax = anchorMax;
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.offsetMin = offsetMin;
            barRect.offsetMax = offsetMax;
            barRect.localScale = Vector3.one;
            barRect.localRotation = Quaternion.identity;

            barImage.sprite = GetUiSprite();
            barImage.type = Image.Type.Simple;
            barImage.color = frameColor;
            barImage.raycastTarget = false;
        }

        void ApplyGlintStyle(RectTransform glintRect, Image glintImage)
        {
            glintRect.anchorMin = new Vector2(0.5f, 0.5f);
            glintRect.anchorMax = new Vector2(0.5f, 0.5f);
            glintRect.pivot = new Vector2(0.5f, 0.5f);
            glintRect.sizeDelta = glintSize;
            glintRect.anchoredPosition = new Vector2(24f, 18f);
            glintRect.localRotation = Quaternion.Euler(0f, 0f, glintRotationDegrees);
            glintRect.localScale = Vector3.one;

            glintImage.sprite = GetUiSprite();
            glintImage.type = Image.Type.Simple;
            glintImage.color = glintColor;
        }

        void ApplyViewportInsets(RectTransform viewport)
        {
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(frameThickness, frameThickness);
            viewport.offsetMax = new Vector2(-frameThickness, -frameThickness);
            viewport.localScale = Vector3.one;
            viewport.localRotation = Quaternion.identity;
        }

        static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        static Sprite GetUiSprite()
        {
            if (uiSprite != null)
                return uiSprite;

            uiSprite = CreateFallbackWhiteSprite();
            return uiSprite;
        }

        static Sprite CreateFallbackWhiteSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>레거시 호출명 — EnsureStudyRoomMirrorOverlay 로 위임.</summary>
        public void EnsureViewportOnMirrorCard()
        {
            EnsureStudyRoomMirrorOverlay();
        }
    }
}
