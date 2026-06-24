using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom CardStackPanel 전용 — 반쪽 숫자 단서와 BookmarkMirror 거울 뷰포트 안의
    /// 7·3·3·7 개별 숫자 조각. 성공 전에는 조각이 흩어져 보이고, 성공 시 7337로 정렬·점등된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StudyRoomDiaryMirrorCodeView : MonoBehaviour
    {
        const string PuzzlePanelName = "DiaryMirrorPuzzlePanel";
        const string HalfCodeMaskName = "HalfCodeMask";
        const string HalfCodeTextName = "HalfCodeText";
        const string BookDigitFieldName = "BookDigitField";
        const string BookDigitPieceNamePrefix = "BookDigitPiece";
        const string LightSourceName = "LightSource";
        const string ReflectionTargetName = "ReflectionTarget";
        const string IncomingLightBeamName = "IncomingLightBeam";
        const string ReflectedLightBeamName = "ReflectedLightBeam";
        const string OverlayRootName = "StudyRoomDiaryMirrorOverlay";
        const string ViewportName = "MirrorViewport";
        const string DigitFieldName = "DigitField";
        const string DigitPieceNamePrefix = "DigitPiece";
        const string ReflectionBeamName = "ReflectionBeam";
        const string FrameRootName = "MirrorFrame";
        const string GlintName = "MirrorGlint";

        [Header("Code")]
        [SerializeField] string diaryCode = "7337";
        [SerializeField] bool showTopHalf = true;

        [Header("Layout")]
        [SerializeField] Vector2 codeAreaSize = new Vector2(420f, 220f);
        [SerializeField] float codeFontSize = 52f;
        [SerializeField] Color codeColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        [SerializeField] Vector2 clueAnchoredPosition = Vector2.zero;

        [Header("Mirror Chrome")]
        [SerializeField] float frameThickness = 8f;
        [SerializeField] float bookmarkFrameThickness = 4f;
        [SerializeField] Color frameColor = new Color(0.74f, 0.78f, 0.84f, 0.95f);
        [SerializeField] Color glintColor = new Color(0.86f, 0.93f, 1f, 0.22f);
        [SerializeField] float glintRotationDegrees = -38f;
        [SerializeField] Vector2 glintSize = new Vector2(220f, 36f);

        [Header("Digit Pieces (7-3-3-7)")]
        [Tooltip("비워 두면 7,3,3,7 기본 조각을 코드로 생성한다. 채우면 인스펙터 배치를 그대로 쓴다.")]
        [SerializeField] List<StudyRoomDiaryDigitPiece> digitPieces = new List<StudyRoomDiaryDigitPiece>();
        [SerializeField] float digitSpacing = 86f;
        [SerializeField] float digitFontSize = 52f;
        [SerializeField] bool randomizeDigitScatterOnStart = true;
        [SerializeField] Vector2 digitScatterXRange = new Vector2(-140f, 140f);
        [SerializeField] Vector2 digitScatterYRange = new Vector2(-200f, -110f);
        [SerializeField] float digitScatterRotationRange = 15f;
        [SerializeField] float digitScatterMinDistance = 52f;
        [SerializeField] Color digitScatterColor = new Color(0.96f, 0.92f, 0.84f, 0.82f);
        [SerializeField] Color digitSolvedColor = new Color(1f, 0.91f, 0.65f, 1f);
        [SerializeField, Range(0f, 1f)] float digitScatterMinAlpha = 0f;
        [Tooltip("거울 밝기 강도가 이 값 미만이면 거울 안 숫자를 거의 보이지 않게 한다(책 단서와 중복 방지).")]
        [SerializeField, Range(0f, 1f)] float mirrorDigitRevealThreshold = 0.75f;
        [Tooltip("책 표면 숫자는 이 밝기 이상부터 서서히 나타난다.")]
        [SerializeField, Range(0f, 1f)] float bookDigitRevealThreshold = 0.75f;

        [Header("Book Half Clue")]
        [SerializeField, Range(0f, 1f)] float halfCodeClueAlpha = 0f;
        [SerializeField, Range(0f, 1f)] float halfCodeSolvedAlpha = 0.1f;

        [Header("HTML Prototype Markers")]
        [SerializeField] Color lightSourceColor = new Color(1f, 0.96f, 0.81f, 1f);
        [SerializeField] Color targetMarkerColor = new Color(0.74f, 0.76f, 0.76f, 0.85f);
        [SerializeField] Color targetLitColor = new Color(0.56f, 0.84f, 0.63f, 1f);
        [SerializeField] Vector2 lightSourceSize = new Vector2(22f, 22f);
        [SerializeField] Vector2 targetMarkerSize = new Vector2(30f, 30f);

        [Header("Reflection Beam")]
        [SerializeField] Color reflectionBeamColor = new Color(1f, 0.91f, 0.65f, 1f);
        [SerializeField] float incidentBeamThickness = 2f;
        [SerializeField] float reflectedBeamMinThickness = 2f;
        [SerializeField] float reflectedBeamMaxThickness = 5f;
        [SerializeField, Range(0f, 1f)] float incidentBeamAlpha = 0.5f;
        [SerializeField, Range(0f, 1f)] float reflectedBeamMinAlpha = 0.25f;
        [SerializeField] float digitBeamRevealRadius = 34f;

        [Header("References")]
        [SerializeField] RectTransform bookOverlayRect;
        [SerializeField] RectTransform mirrorCardRect;
        [SerializeField] RectTransform puzzlePanelRect;
        [SerializeField] RectTransform halfCodeMaskRect;
        [SerializeField] TextMeshProUGUI halfCodeText;
        [SerializeField] RectTransform bookDigitFieldRect;
        [SerializeField] RectTransform overlayRootRect;
        [SerializeField] RectTransform mirrorViewportRect;
        [SerializeField] RectTransform digitFieldRect;
        [SerializeField] Image lightSourceImage;
        [SerializeField] Image reflectionTargetImage;
        [SerializeField] Image incomingLightBeamImage;
        [SerializeField] Image reflectedLightBeamImage;
        [SerializeField] Image reflectionBeamImage;
        [SerializeField] Image mirrorGlintImage;

        readonly List<TextMeshProUGUI> digitTexts = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> bookDigitTexts = new List<TextMeshProUGUI>();
        float currentSolveProgress;
        bool hasReflectionPath;
        bool currentPathSolved;
        bool digitPiecesGeneratedByCode;
        Vector2 reflectedBeamStart;
        Vector2 reflectedBeamEnd;

        static Sprite uiSprite;

        public string DiaryCode => diaryCode;

        public int DigitPieceCount => ResolveDigitPieces().Count;

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
            ResolveBookDigitField();
            ResolveHtmlPrototypeMarkers();
            ResolveBookReflectionBeams();
            puzzlePanelRect.gameObject.SetActive(true);
            ApplyDigitProgress(currentSolveProgress);
            ApplyBookDigitColors(currentSolveProgress);
            ApplyMirrorDigitColors(currentSolveProgress);
            ApplyBookBeamProgress(currentSolveProgress);
        }

        /// <summary>BookmarkMirror 위에 거울 뷰포트·완전한 숫자 텍스트 오버레이를 생성한다.</summary>
        public void EnsureMirrorOverlay()
        {
            if (mirrorCardRect == null)
                return;

            ResolveOverlayRoot();
            ResolveMirrorViewport();
            ResolveDigitField();
            ResolveReflectionBeam();
            ResolveMirrorFrame();
            ResolveMirrorGlint();
            ApplyDigitProgress(currentSolveProgress);
        }

        public void SetMirrorOverlayActive(bool active)
        {
            if (active)
                EnsureMirrorOverlay();

            if (overlayRootRect != null)
                overlayRootRect.gameObject.SetActive(active);
        }

        /// <summary>반사 근접도(0~1)에 따라 반사광·숫자 조각 정렬/밝기를 갱신한다.</summary>
        public void SetReflectionIntensity(float intensity01)
        {
            float intensity = Mathf.Clamp01(intensity01);
            currentSolveProgress = intensity;
            ApplyDigitProgress(intensity);

            if (reflectionBeamImage != null)
            {
                Color beam = reflectionBeamColor;
                beam.a = Mathf.Lerp(reflectedBeamMinAlpha, reflectionBeamColor.a, intensity);
                reflectionBeamImage.color = beam;
            }

            ApplyBookBeamProgress(intensity);
            ApplyMirrorDigitColors(intensity);
            ApplyBookDigitColors(intensity);
        }

        public void SetReflectionPath(
            Vector2 lightSource,
            Vector2 mirrorPosition,
            Vector2 reflectedDirection,
            Vector2 targetMarker,
            bool solved)
        {
            EnsureHalfCodeClue();

            if (lightSourceImage != null)
                lightSourceImage.rectTransform.anchoredPosition = lightSource;

            if (reflectionTargetImage != null)
            {
                reflectionTargetImage.rectTransform.anchoredPosition = targetMarker;
                reflectionTargetImage.color = solved ? targetLitColor : targetMarkerColor;
            }

            SetBeamBetween(incomingLightBeamImage, lightSource, mirrorPosition, incidentBeamThickness);

            Vector2 reflectedEnd = mirrorPosition + reflectedDirection.normalized * 600f;
            float reflectedThickness = Mathf.Lerp(
                reflectedBeamMinThickness,
                reflectedBeamMaxThickness,
                currentSolveProgress);
            SetBeamBetween(reflectedLightBeamImage, mirrorPosition, reflectedEnd, reflectedThickness);
            reflectedBeamStart = mirrorPosition;
            reflectedBeamEnd = reflectedEnd;
            hasReflectionPath = true;
            currentPathSolved = solved;
            ApplyBookDigitColors(currentSolveProgress);
        }

        void ApplyMirrorDigitColors(float intensity01)
        {
            float reveal = Mathf.InverseLerp(mirrorDigitRevealThreshold, 1f, intensity01);
            float alpha = Mathf.Lerp(digitScatterMinAlpha, digitSolvedColor.a, intensity01) * reveal;

            for (int i = 0; i < digitTexts.Count; i++)
            {
                if (digitTexts[i] == null)
                    continue;

                Color color = Color.Lerp(digitScatterColor, digitSolvedColor, intensity01);
                color.a = alpha;
                digitTexts[i].color = color;
            }
        }

        void ApplyBookDigitColors(float intensity01)
        {
            for (int i = 0; i < bookDigitTexts.Count; i++)
            {
                if (bookDigitTexts[i] == null)
                    continue;

                float beamReveal = ResolveBookDigitBeamReveal(bookDigitTexts[i].rectTransform);
                float progressReveal = Mathf.InverseLerp(bookDigitRevealThreshold, 1f, intensity01);
                float reveal = currentPathSolved ? 1f : Mathf.Max(beamReveal, progressReveal * beamReveal);
                float alpha = Mathf.Lerp(digitScatterMinAlpha, digitSolvedColor.a, reveal);
                Color color = Color.Lerp(digitScatterColor, digitSolvedColor, intensity01);
                color.a = alpha;
                bookDigitTexts[i].color = color;
            }
        }

        float ResolveBookDigitBeamReveal(RectTransform digitRect)
        {
            if (digitRect == null || !hasReflectionPath)
                return 0f;

            float distance = DistancePointToSegment(
                digitRect.anchoredPosition,
                reflectedBeamStart,
                reflectedBeamEnd);

            return 1f - Mathf.Clamp01(distance / Mathf.Max(1f, digitBeamRevealRadius));
        }

        /// <summary>성공 시: 조각을 7337로 즉시 스냅 정렬하고 최대 밝기로 점등한다.</summary>
        public void ShowSolved()
        {
            EnsureMirrorOverlay();
            currentSolveProgress = 1f;
            ApplyDigitProgress(1f);
            SetReflectionIntensity(1f);
            ApplyHalfCodeMask(showFullCode: true);
        }

        /// <summary>성공 전: 조각을 흩어진 초기 상태로 되돌린다.</summary>
        public void ShowScattered()
        {
            if (randomizeDigitScatterOnStart && digitPiecesGeneratedByCode)
                RerollGeneratedDigitPieces();

            currentSolveProgress = 0f;
            hasReflectionPath = false;
            currentPathSolved = false;
            ApplyDigitProgress(0f);
            SetReflectionIntensity(0f);
            ApplyHalfCodeMask(showFullCode: false);
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

        void ResolveBookDigitField()
        {
            List<StudyRoomDiaryDigitPiece> pieces = ResolveDigitPieces();

            Transform existing = puzzlePanelRect.Find(BookDigitFieldName);
            if (existing != null)
            {
                bookDigitFieldRect = existing as RectTransform;
            }
            else
            {
                var fieldObject = new GameObject(BookDigitFieldName, typeof(RectTransform));
                bookDigitFieldRect = fieldObject.GetComponent<RectTransform>();
                bookDigitFieldRect.SetParent(puzzlePanelRect, false);
            }

            bookDigitFieldRect.anchorMin = new Vector2(0.5f, 0.5f);
            bookDigitFieldRect.anchorMax = new Vector2(0.5f, 0.5f);
            bookDigitFieldRect.pivot = new Vector2(0.5f, 0.5f);
            bookDigitFieldRect.sizeDelta = codeAreaSize;
            bookDigitFieldRect.anchoredPosition = clueAnchoredPosition;
            bookDigitFieldRect.localRotation = Quaternion.identity;
            bookDigitFieldRect.localScale = Vector3.one;

            bookDigitTexts.Clear();
            for (int i = 0; i < pieces.Count; i++)
                bookDigitTexts.Add(ResolveBookDigitPiece(i, pieces[i]));
        }

        TextMeshProUGUI ResolveBookDigitPiece(int index, StudyRoomDiaryDigitPiece piece)
        {
            string pieceName = BookDigitPieceNamePrefix + index;
            Transform existing = bookDigitFieldRect.Find(pieceName);

            TextMeshProUGUI text;
            if (existing != null)
            {
                text = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var pieceObject = new GameObject(
                    pieceName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                pieceObject.transform.SetParent(bookDigitFieldRect, false);
                text = pieceObject.GetComponent<TextMeshProUGUI>();
            }

            ApplyDigitTextStyle(text);
            text.text = piece.glyph;
            text.fontSize = codeFontSize;

            RectTransform pieceRect = text.rectTransform;
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.sizeDelta = new Vector2(digitSpacing, codeAreaSize.y);
            return text;
        }

        void ApplyHalfCodeMask(bool showFullCode = false)
        {
            if (halfCodeMaskRect == null || halfCodeText == null)
                return;

            halfCodeMaskRect.sizeDelta = showFullCode
                ? codeAreaSize
                : new Vector2(codeAreaSize.x, codeAreaSize.y * 0.5f);
            halfCodeMaskRect.anchoredPosition = clueAnchoredPosition;

            RectTransform textRect = halfCodeText.rectTransform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = codeAreaSize;

            if (showFullCode)
            {
                textRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                textRect.anchoredPosition = showTopHalf
                    ? new Vector2(0f, -codeAreaSize.y * 0.25f)
                    : new Vector2(0f, codeAreaSize.y * 0.25f);
            }

            halfCodeText.text = diaryCode;
            ApplyCodeTextStyle(halfCodeText, showFullCode);
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

        void ResolveDigitField()
        {
            List<StudyRoomDiaryDigitPiece> pieces = ResolveDigitPieces();

            Transform existing = mirrorViewportRect.Find(DigitFieldName);
            if (existing != null)
            {
                digitFieldRect = existing as RectTransform;
            }
            else
            {
                var fieldObject = new GameObject(DigitFieldName, typeof(RectTransform));
                digitFieldRect = fieldObject.GetComponent<RectTransform>();
                digitFieldRect.SetParent(mirrorViewportRect, false);
                StretchFull(digitFieldRect);
            }

            digitTexts.Clear();
            for (int i = 0; i < pieces.Count; i++)
                digitTexts.Add(ResolveDigitPiece(i, pieces[i]));
        }

        TextMeshProUGUI ResolveDigitPiece(int index, StudyRoomDiaryDigitPiece piece)
        {
            string pieceName = DigitPieceNamePrefix + index;
            Transform existing = digitFieldRect.Find(pieceName);

            TextMeshProUGUI text;
            if (existing != null)
            {
                text = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var pieceObject = new GameObject(
                    pieceName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                pieceObject.transform.SetParent(digitFieldRect, false);
                text = pieceObject.GetComponent<TextMeshProUGUI>();
            }

            ApplyDigitTextStyle(text);
            text.text = piece.glyph;

            RectTransform pieceRect = text.rectTransform;
            pieceRect.anchorMin = new Vector2(0.5f, 0.5f);
            pieceRect.anchorMax = new Vector2(0.5f, 0.5f);
            pieceRect.pivot = new Vector2(0.5f, 0.5f);
            pieceRect.sizeDelta = new Vector2(digitSpacing, codeAreaSize.y);
            return text;
        }

        void ResolveHtmlPrototypeMarkers()
        {
            lightSourceImage = ResolvePrototypeMarker(LightSourceName, lightSourceImage, lightSourceSize, lightSourceColor);
            reflectionTargetImage = ResolvePrototypeMarker(
                ReflectionTargetName,
                reflectionTargetImage,
                targetMarkerSize,
                targetMarkerColor);
        }

        Image ResolvePrototypeMarker(string markerName, Image cachedImage, Vector2 size, Color color)
        {
            if (cachedImage != null)
            {
                ApplyPrototypeMarkerStyle(cachedImage.rectTransform, cachedImage, size, color);
                return cachedImage;
            }

            Transform existing = puzzlePanelRect.Find(markerName);
            if (existing != null)
            {
                Image existingImage = existing.GetComponent<Image>();
                ApplyPrototypeMarkerStyle(existingImage.rectTransform, existingImage, size, color);
                return existingImage;
            }

            var markerObject = new GameObject(markerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObject.transform.SetParent(puzzlePanelRect, false);
            Image image = markerObject.GetComponent<Image>();
            image.raycastTarget = false;
            ApplyPrototypeMarkerStyle(image.rectTransform, image, size, color);
            return image;
        }

        void ApplyPrototypeMarkerStyle(RectTransform markerRect, Image markerImage, Vector2 size, Color color)
        {
            markerRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = size;
            markerRect.localRotation = Quaternion.identity;
            markerRect.localScale = Vector3.one;

            markerImage.sprite = GetUiSprite();
            markerImage.type = Image.Type.Simple;
            markerImage.color = color;
            markerImage.raycastTarget = false;
        }

        void ResolveReflectionBeam()
        {
            if (reflectionBeamImage != null)
            {
                ApplyReflectionBeamStyle(reflectionBeamImage.rectTransform, reflectionBeamImage);
                return;
            }

            Transform existing = mirrorViewportRect.Find(ReflectionBeamName);
            if (existing != null)
            {
                reflectionBeamImage = existing.GetComponent<Image>();
                ApplyReflectionBeamStyle(reflectionBeamImage.rectTransform, reflectionBeamImage);
                return;
            }

            var beamObject = new GameObject(
                ReflectionBeamName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            beamObject.transform.SetParent(mirrorViewportRect, false);
            reflectionBeamImage = beamObject.GetComponent<Image>();
            reflectionBeamImage.raycastTarget = false;
            ApplyReflectionBeamStyle(reflectionBeamImage.rectTransform, reflectionBeamImage);
        }

        void ResolveBookReflectionBeams()
        {
            incomingLightBeamImage = ResolveBookBeam(IncomingLightBeamName, incomingLightBeamImage);
            reflectedLightBeamImage = ResolveBookBeam(ReflectedLightBeamName, reflectedLightBeamImage);
            ApplyBookBeamProgress(currentSolveProgress);
        }

        Image ResolveBookBeam(string beamName, Image cachedImage)
        {
            if (cachedImage != null)
            {
                ApplyBookBeamStyle(cachedImage.rectTransform, cachedImage, beamName == IncomingLightBeamName);
                return cachedImage;
            }

            Transform existing = puzzlePanelRect.Find(beamName);
            if (existing != null)
            {
                Image existingImage = existing.GetComponent<Image>();
                ApplyBookBeamStyle(existingImage.rectTransform, existingImage, beamName == IncomingLightBeamName);
                return existingImage;
            }

            var beamObject = new GameObject(
                beamName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            beamObject.transform.SetParent(puzzlePanelRect, false);
            Image image = beamObject.GetComponent<Image>();
            image.raycastTarget = false;
            ApplyBookBeamStyle(image.rectTransform, image, beamName == IncomingLightBeamName);
            beamObject.transform.SetAsFirstSibling();
            return image;
        }

        void ApplyDigitProgress(float progress01)
        {
            List<StudyRoomDiaryDigitPiece> pieces = ResolveDigitPieces();
            int count = Mathf.Min(digitTexts.Count, pieces.Count);

            for (int i = 0; i < count; i++)
            {
                TextMeshProUGUI text = digitTexts[i];
                if (text == null)
                    continue;

                RectTransform pieceRect = text.rectTransform;
                pieceRect.anchoredPosition = pieces[i].ResolvePosition(progress01);
                pieceRect.localRotation = Quaternion.Euler(0f, 0f, pieces[i].ResolveRotation(progress01));
            }

            count = Mathf.Min(bookDigitTexts.Count, pieces.Count);
            for (int i = 0; i < count; i++)
            {
                TextMeshProUGUI text = bookDigitTexts[i];
                if (text == null)
                    continue;

                RectTransform pieceRect = text.rectTransform;
                pieceRect.anchoredPosition = pieces[i].ResolvePosition(progress01);
                pieceRect.localRotation = Quaternion.Euler(0f, 0f, pieces[i].ResolveRotation(progress01));
            }
        }

        List<StudyRoomDiaryDigitPiece> ResolveDigitPieces()
        {
            if (digitPieces != null && digitPieces.Count > 0)
                return digitPieces;

            digitPieces = BuildDefaultDigitPieces();
            digitPiecesGeneratedByCode = true;
            return digitPieces;
        }

        List<StudyRoomDiaryDigitPiece> BuildDefaultDigitPieces()
        {
            string[] glyphs = ResolveGlyphs();
            int count = glyphs.Length;

            var pieces = new List<StudyRoomDiaryDigitPiece>(count);
            for (int i = 0; i < count; i++)
            {
                float solvedX = (i - (count - 1) * 0.5f) * digitSpacing;
                Vector2 solvedPosition = new Vector2(solvedX, -150f);
                Vector2 scatterPosition = SampleScatterPosition(pieces);

                pieces.Add(new StudyRoomDiaryDigitPiece(
                    glyphs[i],
                    scatterPosition,
                    Random.Range(-digitScatterRotationRange, digitScatterRotationRange),
                    solvedPosition,
                    0f));
            }

            return pieces;
        }

        void RerollGeneratedDigitPieces()
        {
            List<StudyRoomDiaryDigitPiece> pieces = ResolveDigitPieces();
            for (int i = 0; i < pieces.Count; i++)
            {
                List<StudyRoomDiaryDigitPiece> previousPieces = pieces.GetRange(0, i);
                pieces[i].scatterPosition = SampleScatterPosition(previousPieces);
                pieces[i].scatterRotation = Random.Range(-digitScatterRotationRange, digitScatterRotationRange);
            }
        }

        Vector2 SampleScatterPosition(List<StudyRoomDiaryDigitPiece> previousPieces)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(digitScatterXRange.x, digitScatterXRange.y),
                    Random.Range(digitScatterYRange.x, digitScatterYRange.y));

                bool tooClose = false;
                for (int i = 0; i < previousPieces.Count; i++)
                {
                    if (Vector2.Distance(candidate, previousPieces[i].scatterPosition) < digitScatterMinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    return candidate;
            }

            return new Vector2(
                Random.Range(digitScatterXRange.x, digitScatterXRange.y),
                Random.Range(digitScatterYRange.x, digitScatterYRange.y));
        }

        string[] ResolveGlyphs()
        {
            if (string.IsNullOrEmpty(diaryCode))
                return new[] { "7", "3", "3", "7" };

            var glyphs = new string[diaryCode.Length];
            for (int i = 0; i < diaryCode.Length; i++)
                glyphs[i] = diaryCode[i].ToString();

            return glyphs;
        }

        void ApplyDigitTextStyle(TextMeshProUGUI text)
        {
            text.font = ResolveDefaultFont();
            text.fontSize = digitFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = digitScatterColor;
            text.raycastTarget = false;
        }

        void ApplyReflectionBeamStyle(RectTransform beamRect, Image beamImage)
        {
            beamRect.anchorMin = new Vector2(0.5f, 0.5f);
            beamRect.anchorMax = new Vector2(0.5f, 0.5f);
            beamRect.pivot = new Vector2(0.5f, 0.5f);
            beamRect.sizeDelta = new Vector2(120f, incidentBeamThickness);
            beamRect.anchoredPosition = Vector2.zero;
            beamRect.localRotation = Quaternion.identity;
            beamRect.localScale = Vector3.one;

            beamImage.sprite = GetUiSprite();
            beamImage.type = Image.Type.Simple;
            Color beam = reflectionBeamColor;
            beam.a = reflectedBeamMinAlpha;
            beamImage.color = beam;
            beamImage.raycastTarget = false;
        }

        void ApplyBookBeamStyle(RectTransform beamRect, Image beamImage, bool incoming)
        {
            beamRect.anchorMin = new Vector2(0.5f, 0.5f);
            beamRect.anchorMax = new Vector2(0.5f, 0.5f);
            beamRect.pivot = new Vector2(0f, 0.5f);
            beamRect.sizeDelta = new Vector2(120f, incoming ? incidentBeamThickness : reflectedBeamMinThickness);
            beamRect.localRotation = Quaternion.identity;
            beamRect.localScale = Vector3.one;

            beamImage.sprite = GetUiSprite();
            beamImage.type = Image.Type.Simple;
            Color beam = reflectionBeamColor;
            beam.a = incoming ? incidentBeamAlpha : reflectedBeamMinAlpha;
            beamImage.color = beam;
            beamImage.raycastTarget = false;
        }

        void ApplyBookBeamProgress(float intensity01)
        {
            Vector2 origin = mirrorCardRect != null ? mirrorCardRect.anchoredPosition : clueAnchoredPosition;

            ApplyBookBeamProgress(incomingLightBeamImage, origin, intensity01);
            ApplyBookBeamProgress(reflectedLightBeamImage, origin, intensity01);
        }

        void ApplyBookBeamProgress(Image beamImage, Vector2 origin, float intensity01)
        {
            if (beamImage == null)
                return;

            RectTransform beamRect = beamImage.rectTransform;
            beamRect.anchoredPosition = origin;

            Color beam = reflectionBeamColor;
            beam.a = Mathf.Lerp(reflectedBeamMinAlpha, reflectionBeamColor.a, intensity01);
            beamImage.color = beam;
        }

        static void SetBeamBetween(Image beamImage, Vector2 start, Vector2 end, float thickness)
        {
            if (beamImage == null)
                return;

            Vector2 delta = end - start;
            RectTransform beamRect = beamImage.rectTransform;
            beamRect.anchoredPosition = start;
            beamRect.sizeDelta = new Vector2(delta.magnitude, thickness);
            beamRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
                return Vector2.Distance(point, segmentStart);

            float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
            Vector2 closest = segmentStart + segment * t;
            return Vector2.Distance(point, closest);
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

        void ApplyCodeTextStyle(TextMeshProUGUI text, bool showFullCode = false)
        {
            text.text = diaryCode;
            text.font = ResolveDefaultFont();
            text.fontSize = codeFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            Color ink = codeColor;
            ink.a = showFullCode ? halfCodeSolvedAlpha : halfCodeClueAlpha;
            text.color = ink;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
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
            float thickness = ResolveFrameThickness();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = new Vector2(thickness, thickness);
            viewport.offsetMax = new Vector2(-thickness, -thickness);
            viewport.localScale = Vector3.one;
            viewport.localRotation = Quaternion.identity;
        }

        float ResolveFrameThickness()
        {
            if (mirrorCardRect == null)
                return frameThickness;

            return mirrorCardRect.rect.height <= 360f ? bookmarkFrameThickness : frameThickness;
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
