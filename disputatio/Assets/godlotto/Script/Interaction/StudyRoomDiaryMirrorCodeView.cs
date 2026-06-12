using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom CardStackPanel 전용 — 반쪽 숫자 단서와 BookmarkMirror 거울 뷰포트 안의 완전한 7337 텍스트.
    /// </summary>
    [DisallowMultipleComponent]
    public class StudyRoomDiaryMirrorCodeView : MonoBehaviour
    {
        const string PuzzlePanelName = "DiaryMirrorPuzzlePanel";
        const string HalfCodeMaskName = "HalfCodeMask";
        const string HalfCodeTextName = "HalfCodeText";
        const string OverlayRootName = "StudyRoomDiaryMirrorOverlay";
        const string ViewportName = "MirrorViewport";
        const string FullCodeTextName = "FullCodeText";
        const string FrameRootName = "MirrorFrame";
        const string GlintName = "MirrorGlint";

        [Header("Code")]
        [SerializeField] string diaryCode = "7337";
        [SerializeField] bool showTopHalf = true;

        [Header("Layout")]
        [SerializeField] Vector2 codeAreaSize = new Vector2(420f, 160f);
        [SerializeField] float codeFontSize = 112f;
        [SerializeField] Color codeColor = new Color(0.12f, 0.1f, 0.08f, 1f);
        [SerializeField] Color mirrorCodeColor = new Color(0.92f, 0.96f, 1f, 0.95f);
        [SerializeField] Vector2 clueAnchoredPosition = Vector2.zero;

        [Header("Mirror Chrome")]
        [SerializeField] float frameThickness = 8f;
        [SerializeField] Color frameColor = new Color(0.74f, 0.78f, 0.84f, 0.95f);
        [SerializeField] Color glintColor = new Color(0.86f, 0.93f, 1f, 0.22f);
        [SerializeField] float glintRotationDegrees = -38f;
        [SerializeField] Vector2 glintSize = new Vector2(220f, 36f);

        [Header("References")]
        [SerializeField] RectTransform bookOverlayRect;
        [SerializeField] RectTransform mirrorCardRect;
        [SerializeField] RectTransform puzzlePanelRect;
        [SerializeField] RectTransform halfCodeMaskRect;
        [SerializeField] TextMeshProUGUI halfCodeText;
        [SerializeField] RectTransform overlayRootRect;
        [SerializeField] RectTransform mirrorViewportRect;
        [SerializeField] TextMeshProUGUI fullCodeText;
        [SerializeField] Image mirrorGlintImage;

        static Sprite uiSprite;

        public string DiaryCode => diaryCode;

        public void Configure(RectTransform bookOverlay, RectTransform mirrorCard)
        {
            bookOverlayRect = bookOverlay;
            mirrorCardRect = mirrorCard;
        }

        /// <summary>BookOverlay 위에 반쪽 숫자 단서 UI를 생성·표시한다.</summary>
        public void EnsureHalfCodeClue()
        {
            if (bookOverlayRect == null)
                return;

            ResolvePuzzlePanel();
            ResolveHalfCodeMask();
            ResolveHalfCodeText();
            ApplyHalfCodeMask();
            puzzlePanelRect.gameObject.SetActive(true);
        }

        /// <summary>BookmarkMirror 위에 거울 뷰포트·완전한 숫자 텍스트 오버레이를 생성한다.</summary>
        public void EnsureMirrorOverlay()
        {
            if (mirrorCardRect == null)
                return;

            ResolveOverlayRoot();
            ResolveMirrorViewport();
            ResolveFullCodeText();
            ResolveMirrorFrame();
            ResolveMirrorGlint();
        }

        public void SetMirrorOverlayActive(bool active)
        {
            if (active)
                EnsureMirrorOverlay();

            if (overlayRootRect != null)
                overlayRootRect.gameObject.SetActive(active);
        }

        public void HidePuzzleUi()
        {
            if (puzzlePanelRect != null)
                puzzlePanelRect.gameObject.SetActive(false);

            SetMirrorOverlayActive(false);
        }

        void ResolvePuzzlePanel()
        {
            if (puzzlePanelRect != null)
                return;

            Transform existing = bookOverlayRect.Find(PuzzlePanelName);
            if (existing != null)
            {
                puzzlePanelRect = existing as RectTransform;
                return;
            }

            var panelObject = new GameObject(PuzzlePanelName, typeof(RectTransform));
            puzzlePanelRect = panelObject.GetComponent<RectTransform>();
            puzzlePanelRect.SetParent(bookOverlayRect, false);
            StretchFull(puzzlePanelRect);
            puzzlePanelRect.SetAsFirstSibling();
        }

        void ResolveHalfCodeMask()
        {
            if (halfCodeMaskRect != null)
                return;

            Transform existing = puzzlePanelRect.Find(HalfCodeMaskName);
            if (existing != null)
            {
                halfCodeMaskRect = existing as RectTransform;
                if (halfCodeMaskRect.GetComponent<RectMask2D>() == null)
                    halfCodeMaskRect.gameObject.AddComponent<RectMask2D>();
                return;
            }

            var maskObject = new GameObject(HalfCodeMaskName, typeof(RectTransform), typeof(RectMask2D));
            halfCodeMaskRect = maskObject.GetComponent<RectTransform>();
            halfCodeMaskRect.SetParent(puzzlePanelRect, false);
            halfCodeMaskRect.anchorMin = new Vector2(0.5f, 0.5f);
            halfCodeMaskRect.anchorMax = new Vector2(0.5f, 0.5f);
            halfCodeMaskRect.pivot = new Vector2(0.5f, 0.5f);
            halfCodeMaskRect.sizeDelta = new Vector2(codeAreaSize.x, codeAreaSize.y * 0.5f);
            halfCodeMaskRect.anchoredPosition = clueAnchoredPosition;
        }

        void ResolveHalfCodeText()
        {
            if (halfCodeText != null)
                return;

            Transform existing = halfCodeMaskRect.Find(HalfCodeTextName);
            if (existing != null)
            {
                halfCodeText = existing.GetComponent<TextMeshProUGUI>();
                ApplyCodeTextStyle(halfCodeText);
                return;
            }

            var textObject = new GameObject(
                HalfCodeTextName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(halfCodeMaskRect, false);
            halfCodeText = textObject.GetComponent<TextMeshProUGUI>();
            ApplyCodeTextStyle(halfCodeText);
            StretchFull(halfCodeText.rectTransform);
            halfCodeText.rectTransform.sizeDelta = codeAreaSize;
        }

        void ApplyHalfCodeMask()
        {
            if (halfCodeMaskRect == null || halfCodeText == null)
                return;

            halfCodeMaskRect.sizeDelta = new Vector2(codeAreaSize.x, codeAreaSize.y * 0.5f);
            halfCodeMaskRect.anchoredPosition = clueAnchoredPosition;

            RectTransform textRect = halfCodeText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = codeAreaSize;
            textRect.anchoredPosition = showTopHalf
                ? new Vector2(0f, -codeAreaSize.y * 0.25f)
                : new Vector2(0f, codeAreaSize.y * 0.25f);

            halfCodeText.text = diaryCode;
            ApplyCodeTextStyle(halfCodeText);
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

        void ResolveFullCodeText()
        {
            if (fullCodeText != null)
                return;

            Transform existing = mirrorViewportRect.Find(FullCodeTextName);
            if (existing != null)
            {
                fullCodeText = existing.GetComponent<TextMeshProUGUI>();
                ApplyMirrorCodeTextStyle(fullCodeText);
                fullCodeText.text = diaryCode;
                return;
            }

            var textObject = new GameObject(
                FullCodeTextName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(mirrorViewportRect, false);
            fullCodeText = textObject.GetComponent<TextMeshProUGUI>();
            ApplyMirrorCodeTextStyle(fullCodeText);
            fullCodeText.text = diaryCode;

            RectTransform textRect = fullCodeText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = codeAreaSize;
            textRect.anchoredPosition = Vector2.zero;
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

        void ApplyCodeTextStyle(TextMeshProUGUI text)
        {
            text.text = diaryCode;
            text.font = ResolveDefaultFont();
            text.fontSize = codeFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = codeColor;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
        }

        void ApplyMirrorCodeTextStyle(TextMeshProUGUI text)
        {
            ApplyCodeTextStyle(text);
            text.color = mirrorCodeColor;
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

        static TMP_FontAsset ResolveDefaultFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
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
    }
}
