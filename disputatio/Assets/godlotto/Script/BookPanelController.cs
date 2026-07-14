using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BookPanelController : MonoBehaviour
{
    // ── 인벤토리 북 기준 기본값 ─────────────────────────────────────
    // 스프레드 전체 기준(0~1):
    //   좌 페이지 내부: x 0.04~0.47  →  중앙 x≈0.255
    //   우 페이지 내부: x 0.53~0.96  →  중앙 x≈0.745
    //   상하 여백 제외 y: 0.10~0.88
    // ──────────────────────────────────────────────────────────────

    [Serializable]
    public class PageLayoutSettings
    {
        [Header("음식 일러스트 (좌 페이지 중앙)")]
        [Tooltip("정규화 앵커 고정점. 피벗은 항상 (0.5, 0.5).")]
        public Vector2 illustrationAnchor   = new Vector2(0.255f, 0.63f);
        [Tooltip("이미지 크기(픽셀). SizeDelta로 적용됩니다.")]
        public Vector2 illustrationSize     = new Vector2(260f, 175f);
        [Tooltip("앵커 기준 추가 오프셋.")]
        public Vector2 illustrationOffset   = Vector2.zero;

        [Header("레시피 텍스트 (좌 페이지 하단)")]
        public Vector2 recipeAnchorMin  = new Vector2(0.06f, 0.12f);
        public Vector2 recipeAnchorMax  = new Vector2(0.44f, 0.50f);
        [Tooltip("TMP margin (Left, Top, Right, Bottom).")]
        public Vector4 recipeMargin     = new Vector4(8f, 4f, 8f, 6f);
        public float   recipeFontSize   = 26f;

        [Header("메모 텍스트 (우 페이지 전체)")]
        public Vector2 memoAnchorMin    = new Vector2(0.55f, 0.12f);
        public Vector2 memoAnchorMax    = new Vector2(0.92f, 0.82f);
        [Tooltip("TMP margin (Left, Top, Right, Bottom).")]
        public Vector4 memoMargin       = new Vector4(12f, 14f, 12f, 14f);
        public float   memoFontSize     = 28f;
    }

    /// <summary>인스펙터에서 패널별 TMP 스타일을 조정합니다. 글자 크기 상한은 PageLayoutSettings의 memo/recipe 글자 크기입니다.</summary>
    [Serializable]
    public class BookOverlayTextStyle
    {
        public HorizontalAlignmentOptions horizontalAlignment = HorizontalAlignmentOptions.Center;
        public VerticalAlignmentOptions verticalAlignment   = VerticalAlignmentOptions.Middle;
        public bool enableAutoSizing = false;
        [Tooltip("오토 사이즈 사용 시 최소 글자 크기 (최대는 해당 페이지 PageLayoutSettings 글자 크기)")]
        public float autoSizeMinFont = 10f;
    }

    [Tooltip("요리책(가정부 스크랩북) 전용 레이아웃·자동 리소스 로드에 사용합니다.")]
    [SerializeField] private bool isCookbookPanel;

    [Header("퍼즐북 TXT (선택)")]
    [Tooltip("PuzzleBookLoader 포맷의 TXT. 지정 시 scrapbookContentJson 보다 우선 적용됩니다.")]
    [SerializeField] private TextAsset puzzleBookTxt;

    [Header("퍼즐북 자동 매핑")]
    [Tooltip("활성화 시 Awake에서 TXT·페이지·텍스트 컴포넌트를 자동으로 연결합니다.")]
    [SerializeField] private bool autoMapPuzzleBook;
    [Tooltip("비우면 GameObject 이름을 Resources 키로 사용합니다. (예: MaidRoomPuzzleBook)")]
    [SerializeField] private string puzzleBookResourceKey;
    [Tooltip("동적 생성 TMP 오버레이에 사용할 폰트. 비우면 TMP 기본 폰트(LiberationSans) 사용.")]
    [SerializeField] private TMP_FontAsset overlayFont;

    [Header("퍼즐북 텍스트 피벗 (autoMapPuzzleBook 활성 시 적용)")]
    [Tooltip("좌 텍스트(Left 페이지) 피벗. x: 좌0/우1, y: 하0/상1")]
    [SerializeField] private Vector2 puzzleBookLeftPivot  = new Vector2(0.5f, 1f);
    [Tooltip("우 텍스트(Right 페이지) 피벗. x: 좌0/우1, y: 하0/상1")]
    [SerializeField] private Vector2 puzzleBookRightPivot = new Vector2(0.5f, 0.5f);

    [Header("퍼즐 패널 — 텍스트 스타일")]
    [Tooltip("autoMapPuzzleBook 이거나 이름이 PuzzleBookMemoText 일 때")]
    [SerializeField] private BookOverlayTextStyle puzzlePanelMemoTextStyle = new BookOverlayTextStyle
    {
        horizontalAlignment = HorizontalAlignmentOptions.Left,
        verticalAlignment   = VerticalAlignmentOptions.Middle,
        enableAutoSizing    = true,
        autoSizeMinFont     = 10f
    };
    [Tooltip("autoMapPuzzleBook 이거나 이름이 PuzzleBookRecipeText 일 때")]
    [SerializeField] private BookOverlayTextStyle puzzlePanelRecipeTextStyle = new BookOverlayTextStyle
    {
        horizontalAlignment = HorizontalAlignmentOptions.Right,
        verticalAlignment   = VerticalAlignmentOptions.Top,
        enableAutoSizing    = true,
        autoSizeMinFont     = 10f
    };

    [Header("레시피북 패널 — 텍스트 스타일")]
    [Tooltip("퍼즐 패널이 아닐 때 (CookBookScrapText, CookBookRecipeText 등)")]
    [SerializeField] private BookOverlayTextStyle recipeBookPanelMemoTextStyle = new BookOverlayTextStyle
    {
        horizontalAlignment = HorizontalAlignmentOptions.Center,
        verticalAlignment   = VerticalAlignmentOptions.Middle,
        enableAutoSizing    = false,
        autoSizeMinFont     = 10f
    };
    [SerializeField] private BookOverlayTextStyle recipeBookPanelRecipeTextStyle = new BookOverlayTextStyle
    {
        horizontalAlignment = HorizontalAlignmentOptions.Center,
        verticalAlignment   = VerticalAlignmentOptions.Top,
        enableAutoSizing    = false,
        autoSizeMinFont     = 10f
    };

    [Header("레시피북 텍스트 피벗 (isCookbookPanel 활성 시 적용)")]
    [Tooltip("좌 텍스트(레시피) 피벗. x: 좌0/우1, y: 하0/상1")]
    [SerializeField] private Vector2 cookbookLeftPivot  = new Vector2(0.5f, 1f);
    [Tooltip("우 텍스트(메모) 피벗. x: 좌0/우1, y: 하0/상1")]
    [SerializeField] private Vector2 cookbookRightPivot = new Vector2(0.5f, 0.5f);

    [Header("페이지 오브젝트 목록")]
    public GameObject[] pages;

    [Header("페이지별 레이아웃 (비우면 글로벌 기본값 사용)")]
    [Tooltip("pages[] 배열과 같은 인덱스로 맞춥니다. 부족하면 마지막 항목을 재사용합니다.")]
    [SerializeField] private PageLayoutSettings[] pageLayouts;

    [Header("글로벌 기본 레이아웃 (pageLayouts 미지정 시 사용)")]
    [SerializeField] private PageLayoutSettings defaultLayout = new PageLayoutSettings();

    [Header("가정부 방 스크랩북 (선택)")]
    [Tooltip("비어 있으면 CookBook_Panel에서 Resources/MaidRoomCookbookScrapbook 를 자동 로드")]
    [SerializeField] private TextAsset scrapbookContentJson;
    [Tooltip("오른쪽 페이지 — 가정부 메모")]
    [SerializeField] private TextMeshProUGUI scrapbookPageTextOverlay;
    [Tooltip("왼쪽 페이지 하단 — 레시피 본문")]
    [SerializeField] private TextMeshProUGUI scrapbookRecipeTextOverlay;

    public const string RecipeIllustrationChildName = "RecipeIllustration";

    [Header("페이지별 배경 (선택)")]
    [SerializeField] private Sprite[] pageBackgroundSprites;
    [Header("왼쪽 레시피 일러 (선택, 자식 RecipeIllustration)")]
    [SerializeField] private Sprite[] pageRecipeIllustrationSprites;

    [Header("페이지 넘기기 애니메이션")]
    [Tooltip("넘기기 프레임 순서 (InventoryBook_01 → 02 → 03 → 04). 비우면 애니메이션 없음.")]
    [SerializeField] private Sprite[] turnForwardFrames;
    [Tooltip("애니메이션 오버레이 Image. 책 배경 위에 올라갈 Image 컴포넌트를 연결하세요.")]
    [SerializeField] private Image turnAnimationImage;
    [Tooltip("프레임 하나당 표시 시간 (초). 낮을수록 빠릅니다.")]
    [SerializeField] private float frameDuration = 0.07f;
    [Tooltip("페이지 내용이 교체되는 프레임 인덱스 (0부터). 보통 절반 지점.")]
    [SerializeField] private int contentSwapFrame = 2;

    [Header("페이지 넘기기 클릭 영역")]
    [Tooltip("다음 페이지 클릭 영역. 마지막 페이지에서는 비활성화됩니다.")]
    [SerializeField] private GameObject nextPageClickArea;
    [Tooltip("이전 페이지 클릭 영역. 첫 페이지에서는 비활성화됩니다.")]
    [SerializeField] private GameObject previousPageClickArea;

    /// <summary>퍼즐 패널 전용 텍스트 스타일(좌·우 정렬, 오토 사이즈). 레시피북과 분리합니다.</summary>
    private bool UsePuzzlePanelMemoTextStyle(TextMeshProUGUI tmp)
    {
        if (tmp == null) return false;
        if (autoMapPuzzleBook) return true;
        return string.Equals(tmp.name, "PuzzleBookMemoText", StringComparison.Ordinal);
    }

    /// <summary>퍼즐 패널 전용 텍스트 스타일(좌·우 정렬, 오토 사이즈). 레시피북과 분리합니다.</summary>
    private bool UsePuzzlePanelRecipeTextStyle(TextMeshProUGUI tmp)
    {
        if (tmp == null) return false;
        if (autoMapPuzzleBook) return true;
        return string.Equals(tmp.name, "PuzzleBookRecipeText", StringComparison.Ordinal);
    }

    /// <summary>TMP 오토 사이즈: min &lt; max 가 되도록 보정 (둘이 같으면 오토가 비활성처럼 보일 수 있음).</summary>
    private static void ApplyTmpAutoSizingRange(TextMeshProUGUI tmp, float maxFontSize, float inspectorMinFont)
    {
        float maxFs = Mathf.Max(1f, maxFontSize);
        float userMin = Mathf.Max(1f, inspectorMinFont);
        float minFs   = Mathf.Min(userMin, maxFs - 0.01f);
        if (minFs >= maxFs) minFs = Mathf.Max(1f, maxFs * 0.5f);
        tmp.fontSizeMax = maxFs;
        tmp.fontSizeMin = minFs;
        tmp.fontSize    = maxFs;
    }

    private void ApplyOverlayTextStyle(TextMeshProUGUI tmp, float layoutFontSize, BookOverlayTextStyle style)
    {
        tmp.horizontalAlignment = style.horizontalAlignment;
        tmp.verticalAlignment   = style.verticalAlignment;
        if (style.enableAutoSizing)
        {
            tmp.enableAutoSizing = true;
            ApplyTmpAutoSizingRange(tmp, layoutFontSize, style.autoSizeMinFont);
        }
        else
        {
            tmp.enableAutoSizing = false;
            tmp.fontSize         = layoutFontSize;
        }
    }

    private int currentPageIndex = 0;
    private bool _isTurning = false;

    /// <summary>0-based index of the page currently shown in the puzzle book overlay.</summary>
    public int CurrentPageIndex => currentPageIndex;

    private int PageCount
    {
        get
        {
            int visualPages = pages != null ? pages.Length : 0;
            int textPages = _cookbookSplitPages != null ? _cookbookSplitPages.Length : 0;
            return Mathf.Max(visualPages, textPages, 1);
        }
    }
    private string PREF_KEY;
    private CookbookPagePair[] _cookbookSplitPages;
    private string[] _scrapbookLegacyBodies;

    [Serializable]
    private class CookbookPagePair { public string recipe; public string memo; }
    [Serializable]
    private class CookbookSplitRoot { public CookbookPagePair[] pages; }
    [Serializable]
    private class ScrapbookPagesJson { public string[] pages; }

    void Awake()
    {
        if (turnAnimationImage != null)
            turnAnimationImage.raycastTarget = false;

        PREF_KEY = "LastBookPage_" + gameObject.name;

        if (autoMapPuzzleBook)
            RunAutoMap();

        if (scrapbookContentJson == null && isCookbookPanel)
            scrapbookContentJson = Resources.Load<TextAsset>("MaidRoomCookbookScrapbook");
        if (scrapbookPageTextOverlay == null)
        {
            var t = transform.Find("CookBookScrapText");
            if (t != null) scrapbookPageTextOverlay = t.GetComponent<TextMeshProUGUI>();
        }
        DeactivateOrphanPageChildren();
        TryLoadScrapbookJson();
        TryAssignRecipeSpritesFromResources();
        TryAssignPageBackgroundSpritesFromResources();
        ApplyPageBackgroundSprites();
        ApplyRecipeIllustrationSprites();

        // 레시피 TMP는 메모보다 늦게 만들어질 수 있음 — 레이아웃(오토 사이즈·정렬) 적용 전에 존재해야 함
        if (_cookbookSplitPages != null && _cookbookSplitPages.Length > 0)
            EnsureCookBookRecipeTextUi();

        // 초기 전체 레이아웃 적용 (페이지 0 기준)
        ApplyLayoutForPage(0);
    }

    // ── 페이지별 레이아웃 ─────────────────────────────────────────

    private PageLayoutSettings GetLayout(int pageIndex)
    {
        if (pageLayouts != null && pageLayouts.Length > 0)
        {
            int idx = Mathf.Clamp(pageIndex, 0, pageLayouts.Length - 1);
            if (pageLayouts[idx] != null) return pageLayouts[idx];
        }
        return defaultLayout ?? new PageLayoutSettings();
    }

    /// <summary>지정 페이지의 레이아웃(일러스트·레시피·메모 앵커)을 즉시 적용합니다.</summary>
    private void ApplyLayoutForPage(int pageIndex)
    {
        var layout = GetLayout(pageIndex);

        // 일러스트 — 해당 페이지 오브젝트에만 적용 (pages 없어도 레시피·메모 TMP 스타일은 적용)
        if (pages != null && pageIndex >= 0 && pageIndex < pages.Length && pages[pageIndex] != null)
            ApplyIllustrationLayout(pages[pageIndex], layout);

        // 레시피·메모 텍스트 오버레이 — 전체 공유 오브젝트에 적용
        ApplyRecipeTextLayout(layout);
        ApplyMemoTextLayout(layout);
    }

    private void ApplyIllustrationLayout(GameObject page, PageLayoutSettings layout)
    {
        var tr = page.transform.Find(RecipeIllustrationChildName) as RectTransform;
        if (tr == null) return;
        tr.anchorMin        = layout.illustrationAnchor;
        tr.anchorMax        = layout.illustrationAnchor;
        tr.pivot            = new Vector2(0.5f, 0.5f);
        tr.anchoredPosition = layout.illustrationOffset;
        tr.sizeDelta        = layout.illustrationSize;
    }

    private void ApplyRecipeTextLayout(PageLayoutSettings layout)
    {
        if (scrapbookRecipeTextOverlay == null) return;
        var rt = scrapbookRecipeTextOverlay.rectTransform;
        rt.anchorMin        = layout.recipeAnchorMin;
        rt.anchorMax        = layout.recipeAnchorMax;
        rt.pivot            = autoMapPuzzleBook ? puzzleBookLeftPivot
                            : isCookbookPanel   ? cookbookLeftPivot
                            : new Vector2(0.5f, 1f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        var tmp = scrapbookRecipeTextOverlay;
        var style = UsePuzzlePanelRecipeTextStyle(tmp) ? puzzlePanelRecipeTextStyle : recipeBookPanelRecipeTextStyle;
        ApplyOverlayTextStyle(tmp, layout.recipeFontSize, style);
        tmp.margin                = layout.recipeMargin;
        tmp.textWrappingMode      = TextWrappingModes.Normal;
        tmp.overflowMode          = TextOverflowModes.Overflow;
        tmp.wordWrappingRatios    = 0.5f;
        tmp.lineSpacing           = 2f;
        tmp.paragraphSpacing      = 8f;
    }

    private void ApplyMemoTextLayout(PageLayoutSettings layout)
    {
        if (scrapbookPageTextOverlay == null) return;
        var rt = scrapbookPageTextOverlay.rectTransform;
        rt.anchorMin        = layout.memoAnchorMin;
        rt.anchorMax        = layout.memoAnchorMax;
        rt.pivot            = autoMapPuzzleBook ? puzzleBookRightPivot
                            : isCookbookPanel   ? cookbookRightPivot
                            : new Vector2(0.5f, 0.5f);
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        var tmp = scrapbookPageTextOverlay;
        var style = UsePuzzlePanelMemoTextStyle(tmp) ? puzzlePanelMemoTextStyle : recipeBookPanelMemoTextStyle;
        ApplyOverlayTextStyle(tmp, layout.memoFontSize, style);
        tmp.margin                = layout.memoMargin;
        tmp.textWrappingMode      = TextWrappingModes.Normal;
        tmp.overflowMode          = TextOverflowModes.Overflow;
        tmp.wordWrappingRatios    = 0.5f;
    }

    // ── 페이지 이동 ──────────────────────────────────────────────

    void OnEnable()
    {
        if (string.IsNullOrEmpty(PREF_KEY))
            PREF_KEY = "LastBookPage_" + gameObject.name;

        currentPageIndex = PlayerPrefs.GetInt(PREF_KEY, 0);
        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, PageCount - 1);
        ShowPage(currentPageIndex);
    }

    public void NextPage()
    {
        if (_isTurning || currentPageIndex >= PageCount - 1) return;
        int next = currentPageIndex + 1;
        if (!HasTurnAnimation())
        {
            ApplyPageIndexImmediate(next);
            return;
        }

        StartCoroutine(TurnPage(next, forward: true));
    }

    public void PreviousPage()
    {
        if (_isTurning || currentPageIndex <= 0) return;
        int next = currentPageIndex - 1;
        if (!HasTurnAnimation())
        {
            ApplyPageIndexImmediate(next);
            return;
        }

        StartCoroutine(TurnPage(next, forward: false));
    }

    private bool HasTurnAnimation()
    {
        return turnForwardFrames != null
            && turnForwardFrames.Length > 0
            && turnAnimationImage != null;
    }

    private void ApplyPageIndexImmediate(int nextIndex)
    {
        currentPageIndex = nextIndex;
        ShowPageImmediate(currentPageIndex);
        SaveCurrentPage();
    }

    private IEnumerator TurnPage(int nextIndex, bool forward)
    {
        _isTurning = true;

        // 애니메이션 스프라이트가 없으면 즉시 전환
        if (!HasTurnAnimation())
        {
            ApplyPageIndexImmediate(nextIndex);
            _isTurning = false;
            yield break;
        }

        // 앞방향: 01→02→03→04, 뒷방향: 04→03→02→01
        int frameCount = turnForwardFrames.Length;
        turnAnimationImage.gameObject.SetActive(true);
        var col = turnAnimationImage.color;
        col.a = 1f;
        turnAnimationImage.color = col;
        bool contentSwapped = false;

        for (int i = 0; i < frameCount; i++)
        {
            int fi = forward ? i : (frameCount - 1 - i);
            var sprite = turnForwardFrames[fi];
            if (sprite != null) turnAnimationImage.sprite = sprite;

            // 지정 프레임에서 페이지 내용 교체 (오버레이 뒤에서 조용히)
            if (!contentSwapped && i >= contentSwapFrame)
            {
                currentPageIndex = nextIndex;
                ShowPageImmediate(currentPageIndex);
                contentSwapped = true;
            }

            yield return new WaitForSeconds(frameDuration);
        }

        // 혹시 교체가 안 됐으면 마지막에 교체
        if (!contentSwapped)
        {
            currentPageIndex = nextIndex;
            ShowPageImmediate(currentPageIndex);
        }

        col = turnAnimationImage.color;
        col.a = 0f;
        turnAnimationImage.color = col;
        turnAnimationImage.gameObject.SetActive(false);
        SaveCurrentPage();
        _isTurning = false;
    }

    private void ShowPageImmediate(int index)
    {
        index = Mathf.Clamp(index, 0, PageCount - 1);
        currentPageIndex = index;
        if (pages != null)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null) continue;
                // 자기 자신이 pages[]에 들어간 단일 페이지 모드면 SetActive 스킵
                if (pages[i] == gameObject) continue;
                pages[i].SetActive(i == index);
            }
        }
        ApplyLayoutForPage(index);
        UpdateScrapbookOverlay(index);
        RefreshNavigationAreas();
    }

    /// <summary>
    /// 현재 페이지 경계에 맞춰 이전/다음 클릭 영역 활성 상태를 갱신합니다.
    /// 첫 페이지: previous off / next on(페이지가 2장 이상일 때). 마지막: next off.
    /// </summary>
    private void RefreshNavigationAreas()
    {
        if (previousPageClickArea != null)
            previousPageClickArea.SetActive(currentPageIndex > 0);

        if (nextPageClickArea != null)
            nextPageClickArea.SetActive(currentPageIndex < PageCount - 1);
    }

    // 기존 ShowPage는 OnEnable 전용으로 유지
    private void ShowPage(int index) => ShowPageImmediate(index);

    // ── 스크랩북 텍스트 ───────────────────────────────────────────

    private void UpdateScrapbookOverlay(int index)
    {
        if (scrapbookPageTextOverlay == null) return;

        // split-page 데이터(JSON recipe/memo 또는 TXT left/right) 우선 렌더링
        if (_cookbookSplitPages != null && _cookbookSplitPages.Length > 0)
        {
            EnsureCookBookRecipeTextUi();
            // EnsureCookBookRecipeTextUi 가 처음으로 레시피 TMP를 만든 직후, 오토 사이즈·정렬을 다시 적용
            ApplyLayoutForPage(index);
            string memo   = string.Empty;
            string recipe = string.Empty;
            if (index >= 0 && index < _cookbookSplitPages.Length)
            {
                memo   = _cookbookSplitPages[index].memo   ?? string.Empty;
                recipe = _cookbookSplitPages[index].recipe ?? string.Empty;
            }
            scrapbookPageTextOverlay.text = memo;
            if (scrapbookRecipeTextOverlay != null) scrapbookRecipeTextOverlay.text = WrapAtPeriod(recipe);
            ForceRebuild(scrapbookPageTextOverlay);
            ForceRebuild(scrapbookRecipeTextOverlay);
            return;
        }

        // 레거시 단일 본문 렌더링
        if (_scrapbookLegacyBodies == null || _scrapbookLegacyBodies.Length == 0) return;
        if (scrapbookRecipeTextOverlay != null) scrapbookRecipeTextOverlay.text = string.Empty;
        scrapbookPageTextOverlay.text =
            (index >= 0 && index < _scrapbookLegacyBodies.Length && !string.IsNullOrEmpty(_scrapbookLegacyBodies[index]))
            ? _scrapbookLegacyBodies[index] : string.Empty;
        ForceRebuild(scrapbookPageTextOverlay);
    }

    /// <summary>온점(. 및 。) 뒤에 줄바꿈을 삽입합니다.</summary>
    private static string WrapAtPeriod(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // ". " 또는 "." 뒤에 줄바꿈 — 이미 줄바꿈이 있으면 중복 삽입하지 않음
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            sb.Append(c);
            if ((c == '.' || c == '。' || c == ',' || c == '，') && i + 1 < text.Length && text[i + 1] != '\n')
            {
                // 온점 바로 뒤 공백은 건너뜀
                if (i + 1 < text.Length && text[i + 1] == ' ') i++;
                sb.Append('\n');
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static void ForceRebuild(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(tmp.rectTransform);
        tmp.ForceMeshUpdate(true);
    }

    // ── 초기화 헬퍼 ──────────────────────────────────────────────

    private void TryLoadScrapbookJson()
    {
        _cookbookSplitPages    = null;
        _scrapbookLegacyBodies = null;

        // TXT 포맷 우선 (puzzleBookTxt 지정 시)
        if (puzzleBookTxt != null)
        {
            var parsed = PuzzleBookLoader.Parse(puzzleBookTxt.text);
            if (parsed != null && parsed.Length > 0)
            {
                _cookbookSplitPages = System.Array.ConvertAll(
                    parsed,
                    p => new CookbookPagePair { recipe = p.left, memo = p.right }
                );
            }
            return;
        }

        if (scrapbookContentJson == null) return;
        var raw = scrapbookContentJson.text;
        if (raw.IndexOf("\"recipe\"", StringComparison.Ordinal) >= 0)
        {
            var root = JsonUtility.FromJson<CookbookSplitRoot>(raw);
            if (root?.pages != null && root.pages.Length > 0) _cookbookSplitPages = root.pages;
            return;
        }
        _scrapbookLegacyBodies = JsonUtility.FromJson<ScrapbookPagesJson>(raw)?.pages;
    }

    private void EnsureCookBookRecipeTextUi()
    {
        if (scrapbookPageTextOverlay == null) return;
        if (scrapbookRecipeTextOverlay != null) return;
        var tr = transform.Find("CookBookRecipeText");
        if (tr != null)
        {
            scrapbookRecipeTextOverlay = tr.GetComponent<TextMeshProUGUI>();
            if (scrapbookRecipeTextOverlay != null) return;
        }
        var go  = new GameObject("CookBookRecipeText", typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        var src = scrapbookPageTextOverlay;
        tmp.font               = src.font;
        tmp.fontSharedMaterial = src.fontSharedMaterial;
        tmp.color              = src.color;
        tmp.raycastTarget      = false;
        tmp.richText           = true;
        tmp.text               = string.Empty;
        var rt = tmp.rectTransform;
        rt.SetParent(transform, false);
        rt.SetSiblingIndex(src.transform.GetSiblingIndex());
        scrapbookRecipeTextOverlay = tmp;
    }

    // ── 자동 매핑 ────────────────────────────────────────────────

    /// <summary>
    /// ① TXT 자동 발견  — puzzleBookResourceKey(없으면 GameObject 이름)로 Resources 탐색
    /// ② Pages[] 자동 연결 — 자식 중 Image 를 가진 "Page" 패턴 오브젝트 수집
    /// ③ 텍스트 오버레이 자동 연결 — 자식에서 TMP 컴포넌트를 이름 패턴으로 탐색
    /// </summary>
    private void RunAutoMap()
    {
        AutoDiscoverTxt();
        AutoDiscoverPages();
        AutoDiscoverTextOverlays();
    }

    private void AutoDiscoverTxt()
    {
        if (puzzleBookTxt != null) return;

        string key = string.IsNullOrEmpty(puzzleBookResourceKey)
            ? gameObject.name
            : puzzleBookResourceKey;

        puzzleBookTxt = Resources.Load<TextAsset>(key);

        // 이름 기반 실패 시 "MaidRoomPuzzleBook" 고정 키로 재시도
        if (puzzleBookTxt == null)
            puzzleBookTxt = Resources.Load<TextAsset>("MaidRoomPuzzleBook");

        if (puzzleBookTxt != null)
            Debug.Log($"[BookPanelController] TXT 자동 연결: {puzzleBookTxt.name}");
        else
            Debug.LogWarning($"[BookPanelController] TXT 자동 발견 실패 (key: {key})");
    }

    private static readonly string[] PageNameKeywords = { "page", "책장", "pg" };

    private void AutoDiscoverPages()
    {
        if (pages != null && pages.Length > 0) return;

        var found = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Image>() == null) continue;
            string lower = child.name.ToLowerInvariant();
            foreach (var kw in PageNameKeywords)
            {
                if (lower.Contains(kw)) { found.Add(child.gameObject); break; }
            }
        }

        // 인덱스 순 정렬 (Page_0, Page_1 … 또는 끝 숫자 기준)
        found.Sort((a, b) =>
        {
            int ia = ExtractTrailingInt(a.name);
            int ib = ExtractTrailingInt(b.name);
            return ia.CompareTo(ib);
        });

        if (found.Count > 0)
        {
            pages = found.ToArray();
            Debug.Log($"[BookPanelController] Pages 자동 연결: {found.Count}개");
        }
        else
        {
            // 폴백: 자신을 단일 페이지로 사용 (단일 페이지 북 패널)
            pages = new GameObject[] { gameObject };
            Debug.Log("[BookPanelController] Pages 자동 연결 폴백: 패널 자신을 단일 페이지로 사용합니다.");
        }
    }

    private static readonly string[] MemoNameKeywords  = { "memo", "scrap", "right", "right" };
    private static readonly string[] RecipeNameKeywords = { "recipe", "left" };
    private static readonly string[] TextChromeAncestorNames = { "BackspaceCornerFold" };

    private void AutoDiscoverTextOverlays()
    {
        var allTmp = GetContentTextCandidates();

        // 1차: 이름 패턴으로 분류
        foreach (var tmp in allTmp)
        {
            string lower = tmp.name.ToLowerInvariant();

            if (scrapbookPageTextOverlay == null)
                foreach (var kw in MemoNameKeywords)
                    if (lower.Contains(kw)) { scrapbookPageTextOverlay = tmp; break; }

            if (scrapbookRecipeTextOverlay == null)
                foreach (var kw in RecipeNameKeywords)
                    if (lower.Contains(kw)) { scrapbookRecipeTextOverlay = tmp; break; }

            if (scrapbookPageTextOverlay != null && scrapbookRecipeTextOverlay != null) break;
        }

        // 2차 폴백: 이름 매칭 실패 시 발견된 TMP 순서대로 할당
        if (allTmp.Length > 0 && scrapbookPageTextOverlay == null)
        {
            scrapbookPageTextOverlay = allTmp[0];
            Debug.Log($"[BookPanelController] 우측 텍스트 폴백 연결: {allTmp[0].name}");
        }
        if (allTmp.Length > 1 && scrapbookRecipeTextOverlay == null)
        {
            scrapbookRecipeTextOverlay = allTmp[1];
            Debug.Log($"[BookPanelController] 좌측 텍스트 폴백 연결: {allTmp[1].name}");
        }

        // 3차 폴백: TMP 자체가 없으면 동적으로 생성
        if (scrapbookPageTextOverlay == null)
        {
            scrapbookPageTextOverlay = CreateOverlayTmp("PuzzleBookMemoText",
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            Debug.Log("[BookPanelController] 우측 텍스트 동적 생성: PuzzleBookMemoText");
        }

        if (scrapbookRecipeTextOverlay == null && scrapbookPageTextOverlay != null)
        {
            scrapbookRecipeTextOverlay = CreateOverlayTmp("PuzzleBookRecipeText",
                new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.95f));
            Debug.Log("[BookPanelController] 좌측 텍스트 동적 생성: PuzzleBookRecipeText");
        }

        // overlayFont 지정 시 발견/생성된 오버레이에 모두 적용
        if (overlayFont != null)
        {
            if (scrapbookPageTextOverlay   != null) scrapbookPageTextOverlay.font   = overlayFont;
            if (scrapbookRecipeTextOverlay != null) scrapbookRecipeTextOverlay.font = overlayFont;
        }
    }

    private TextMeshProUGUI CreateOverlayTmp(string goName, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go  = new GameObject(goName, typeof(RectTransform));
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (overlayFont != null) tmp.font = overlayFont;
        tmp.raycastTarget = false;
        tmp.richText      = true;
        tmp.fontSize      = 28f;
        tmp.color         = Color.black;
        tmp.textWrappingMode   = TextWrappingModes.Normal;
        tmp.horizontalAlignment = HorizontalAlignmentOptions.Left;
        tmp.verticalAlignment   = VerticalAlignmentOptions.Top;
        var rt = tmp.rectTransform;
        rt.SetParent(transform, false);
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return tmp;
    }

    private TextMeshProUGUI[] GetContentTextCandidates()
    {
        var candidates = new System.Collections.Generic.List<TextMeshProUGUI>();
        var allTmp = GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (var tmp in allTmp)
        {
            if (tmp == null || IsBookUiChromeText(tmp))
                continue;

            candidates.Add(tmp);
        }

        return candidates.ToArray();
    }

    private static bool IsBookUiChromeText(TextMeshProUGUI tmp)
    {
        Transform current = tmp.transform;
        while (current != null)
        {
            foreach (string chromeName in TextChromeAncestorNames)
            {
                if (string.Equals(current.name, chromeName, StringComparison.Ordinal))
                    return true;
            }

            if (current.GetComponent<Button>() != null)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static int ExtractTrailingInt(string name)
    {
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i])) i--;
        string digits = name.Substring(i + 1);
        return digits.Length > 0 ? int.Parse(digits) : 0;
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Map Puzzle Book (에디터 미리보기)")]
    private void EditorAutoMap()
    {
        AutoDiscoverTxt();
        AutoDiscoverPages();
        AutoDiscoverTextOverlays();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[BookPanelController] 에디터 자동 매핑 완료");
    }
#endif

    private void DeactivateOrphanPageChildren()
    {
        if (pages == null || pages.Length == 0) return;
        var pageSet = new System.Collections.Generic.HashSet<GameObject>(pages);
        foreach (Transform child in transform)
            if (child.GetComponent<Image>() != null
                && !pageSet.Contains(child.gameObject)
                && child.gameObject.name.StartsWith("CookBookPage"))
                child.gameObject.SetActive(false);
    }

    private static bool IsNullOrAllNull(Sprite[] arr)
    {
        if (arr == null || arr.Length == 0) return true;
        foreach (var s in arr) if (s != null) return false;
        return true;
    }

    private void TryAssignRecipeSpritesFromResources()
    {
        if (!isCookbookPanel || pages == null || pages.Length == 0) return;
        if (!IsNullOrAllNull(pageRecipeIllustrationSprites)) return;
        var loaded = new Sprite[pages.Length];
        bool any = false;
        for (int i = 0; i < pages.Length; i++)
        {
            loaded[i] = Resources.Load<Sprite>($"CookbookPages/{i + 1}");
            if (loaded[i] != null) any = true;
        }
        if (any) pageRecipeIllustrationSprites = loaded;
    }

    private void TryAssignPageBackgroundSpritesFromResources()
    {
        if (!isCookbookPanel || pages == null || pages.Length == 0) return;
        if (!IsNullOrAllNull(pageBackgroundSprites)) return;
        var loaded = new Sprite[pages.Length];
        bool any = false;
        for (int i = 0; i < pages.Length; i++)
        {
            loaded[i] = Resources.Load<Sprite>($"CookbookPageBg/{i + 1}");
            if (loaded[i] != null) any = true;
        }
        if (any) pageBackgroundSprites = loaded;
    }

    private void ApplyPageBackgroundSprites()
    {
        if (pageBackgroundSprites == null || pages == null) return;
        for (int i = 0; i < pages.Length && i < pageBackgroundSprites.Length; i++)
        {
            if (pageBackgroundSprites[i] == null) continue;
            var img = pages[i].GetComponent<Image>();
            if (img != null) img.sprite = pageBackgroundSprites[i];
        }
    }

    private void ApplyRecipeIllustrationSprites()
    {
        if (pageRecipeIllustrationSprites == null || pages == null) return;
        for (int i = 0; i < pages.Length && i < pageRecipeIllustrationSprites.Length; i++)
        {
            var sp = pageRecipeIllustrationSprites[i];
            if (sp == null) continue;
            var art = pages[i].transform.Find(RecipeIllustrationChildName);
            if (art == null) continue;
            var img = art.GetComponent<Image>();
            if (img == null) continue;
            img.sprite  = sp;
            img.enabled = true;
        }
    }

    private void SaveCurrentPage()
    {
        PlayerPrefs.SetInt(PREF_KEY, currentPageIndex);
        PlayerPrefs.Save();
    }

    void OnDisable() => SaveCurrentPage();
}
