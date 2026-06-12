using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// CardStackPanel 아래 책 패널에 부착되는 Mirror Item Drop Zone.
/// 서재 다이어리 퍼즐에서는 <c>BookmarkMirror</c>를 드롭하면 BookOverlayPanelA 안의
/// 거울 카드 이미지를 활성화해 자유 이동·배치로 7337 단서를 완성한다.
/// (클래스명은 기존 FilterCard 책장 퍼즐과의 호환을 위해 유지)
/// </summary>
public class FilterCardBookDropZone : MonoBehaviour, IDropHandler
{
    [Header("Mirror Item Drop Zone")]
    [Tooltip("서재 다이어리 거울 퍼즐: BookmarkMirror. 책장 단서 퍼즐 등 다른 용도는 별도 DropZone 사용.")]
    public Item requiredItem;

    [Header("Mirror Card (UI Image)")]
    [Tooltip("드롭 시 활성화되는 거울 카드 UI. Source Image는 BookmarkMirror 스프라이트를 사용한다.")]
    public GameObject filterCardObject;
    public GameObject rotateRightButtonObject;
    public GameObject rotateLeftButtonObject;

    [Header("책 오버레이")]
    [Tooltip("씬에 미리 배치한 BookOverlayPanelA 인스턴스. 지정하면 이걸 드래그 경계로 쓴다.")]
    public RectTransform bookOverlayInstance;

    [Tooltip("bookOverlayInstance 미지정 시 런타임 생성에 쓸 프리팹. 비우면 Resources에서 로드.")]
    public GameObject bookOverlayPrefab;

    [Tooltip("bookOverlayPrefab 미지정 시 사용할 Resources 경로.")]
    public string bookOverlayResourcePath = "BookOverlayPanelA";

    [Tooltip("BookOverlayPanelA를 원래 책 노트 패널처럼 부모 화면 안쪽 여백에 맞춰 배치할지 여부.")]
    public bool useBookOverlayPrefabLayout = true;

    [Tooltip("useBookOverlayPrefabLayout이 꺼져 있을 때 BookOverlayPanelA에 적용할 고정 크기.")]
    public Vector2 bookOverlaySize = new Vector2(720f, 960f);

    [Tooltip("BookOverlayPanelA가 카드 뒤에서 보이도록 강제로 적용할 배경색.")]
    public Color bookOverlayBackgroundColor = new Color(0.78f, 0.70f, 0.54f, 1f);

    [Header("사용 횟수 설정")]
    public int maxUses = 1;

    [Tooltip("켜져 있으면 최대 사용 횟수에 도달했을 때 인벤토리에서 아이템을 제거한다. BookmarkMirror처럼 퍼즐 도구로 재사용해야 하는 아이템은 끈다.")]
    public bool consumeItemOnDrop = true;

    [Header("Diary Mirror Puzzle")]
    [Tooltip("지정하면 BookmarkMirror 활성화 후 거울 숫자 퍼즐(7337) 정답 검사를 시작한다.")]
    public StudyRoomDiaryMirrorPuzzleController diaryMirrorPuzzleController;

    [Tooltip("거울 숫자 퍼즐에서는 회전 버튼을 표시하지 않는다.")]
    public bool hideRotateButtonsForDiaryMirror = true;

    private int currentUses = 0;
    private RectTransform resolvedBook;
    private GameObject filterCardImageObject;
    private RectTransform filterCardImageRect;
    private FilterCardRotator activeCardRotator;

    private void Start()
    {
        EnsureBookOverlay();
        EnsureFilterCardImage();
        SetFilterCardImageActive(false);
        HideFilterCardImage();
        if (diaryMirrorPuzzleController != null)
            diaryMirrorPuzzleController.ShowHalfCodeClue(resolvedBook);
        RewireRotateButtons();

        // 기존 DropZone과 동일: 시작 시 회전 버튼을 숨긴다.
        if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(false);
        if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 드롭된 아이템이 올바른지 확인.
        if (InventorySlot.draggedItem != requiredItem)
            return;

        string itemName = requiredItem != null ? requiredItem.itemName : "아이템";

        bool enforceUseLimit = consumeItemOnDrop && maxUses > 0;
        if (enforceUseLimit && currentUses >= maxUses)
        {
            GameLog.Log(itemName + " 은(는) 더 이상 사용할 수 없습니다.");
            return;
        }

        if (enforceUseLimit)
        {
            currentUses++;
            GameLog.Log(itemName + " 을(를) 책 패널에 사용했습니다. (" + currentUses + "/" + maxUses + ")");
        }
        else
        {
            GameLog.Log(itemName + " 을(를) 책 패널에 사용했습니다.");
        }

        // 1) 드래그 경계가 될 책 패널을 확보한다. 씬에 미리 배치된 인스턴스를 우선 사용한다.
        EnsureBookOverlay();

        // 2) BookOverlayPanelA 안의 이미지 카드만 활성화한다.
        HideFilterCardImage();
        EnsureFilterCardImage();
        ResetFilterCardImage();
        SetFilterCardImageActive(true);
        RewireRotateButtons();

        bool showRotateButtons = !hideRotateButtonsForDiaryMirror || diaryMirrorPuzzleController == null;
        if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(showRotateButtons);
        if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(showRotateButtons);

        if (diaryMirrorPuzzleController != null)
            diaryMirrorPuzzleController.NotifyMirrorCardActivated(filterCardImageRect, activeCardRotator);

        // 5) 소비형 아이템만 인벤토리에서 제거한다. BookmarkMirror는 재시도 가능한 퍼즐 도구로 남긴다.
        if (enforceUseLimit && currentUses >= maxUses && InventoryManager.instance != null)
            InventoryManager.instance.RemoveItem(requiredItem);

        // 드롭 처리가 끝났으므로 드래그 상태를 정리한다.
        InventorySlot.ClearDragState();
    }

    private void EnsureBookOverlay()
    {
        if (resolvedBook != null)
            return;

        resolvedBook = bookOverlayInstance != null ? bookOverlayInstance : FindExistingBookOverlay();
        if (resolvedBook == null)
            resolvedBook = SpawnBookOverlay();

        if (resolvedBook != null)
        {
            PrepareBookOverlayForCardDrop(resolvedBook.gameObject);
            ApplyBookOverlayLayout(resolvedBook);
            resolvedBook.gameObject.SetActive(true);
            resolvedBook.SetAsFirstSibling();
            KeepCloseButtonOnTop();
        }
    }

    private RectTransform FindExistingBookOverlay()
    {
        Transform child = transform.Find("BookOverlayPanelA");
        return child as RectTransform;
    }

    private void EnsureFilterCardImage()
    {
        if (filterCardImageObject != null && filterCardImageRect != null)
        {
            SetFilterCardImageParent();
            return;
        }

        if (filterCardObject == null)
            return;

        filterCardImageObject = filterCardObject;
        filterCardImageRect = filterCardImageObject.GetComponent<RectTransform>();
        if (filterCardImageRect == null)
        {
            filterCardImageObject = null;
            return;
        }

        SetFilterCardImageParent();

        var image = filterCardImageObject.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        var drag = filterCardImageObject.GetComponent<FilterCardBoundedDrag>();
        if (drag == null)
            drag = filterCardImageObject.AddComponent<FilterCardBoundedDrag>();
        drag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);

        activeCardRotator = filterCardImageObject.GetComponent<FilterCardRotator>();
        if (activeCardRotator == null)
            activeCardRotator = filterCardImageObject.AddComponent<FilterCardRotator>();

        filterCardImageRect.SetAsLastSibling();
        KeepCloseButtonOnTop();
    }

    private void SetFilterCardImageParent()
    {
        if (filterCardImageRect == null)
            return;

        RectTransform cardParent = resolvedBook != null ? resolvedBook : transform as RectTransform;
        if (cardParent != null && filterCardImageRect.parent != cardParent)
            filterCardImageRect.SetParent(cardParent, false);
    }

    private void ResetFilterCardImage()
    {
        if (filterCardImageRect == null)
            return;

        filterCardImageRect.anchoredPosition = Vector2.zero;
        // 카드 크기를 고정값으로 강제한다. 스케일 대신 sizeDelta로 직접 지정해
        // RectTransform의 Width/Height가 곧 표시 크기가 되도록 한다.
        filterCardImageRect.sizeDelta = ResolveFilterCardImageSize();
        filterCardImageRect.localScale = Vector3.one;
        filterCardImageRect.localRotation = Quaternion.identity;
        filterCardImageRect.SetAsLastSibling();
        KeepCloseButtonOnTop();

        var drag = filterCardImageObject != null ? filterCardImageObject.GetComponent<FilterCardBoundedDrag>() : null;
        if (drag != null)
            drag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);
    }

    private Vector2 ResolveFilterCardImageSize()
    {
        if (diaryMirrorPuzzleController == null)
            return new Vector2(710.8362f, 695.5485f);

        var image = filterCardImageObject != null ? filterCardImageObject.GetComponent<Image>() : null;
        Sprite sprite = image != null ? image.sprite : null;
        if (sprite == null || sprite.rect.height <= 0f)
            return new Vector2(190f, 620f);

        float targetHeight = 620f;
        float targetWidth = targetHeight * (sprite.rect.width / sprite.rect.height);
        return new Vector2(Mathf.Clamp(targetWidth, 150f, 240f), targetHeight);
    }

    private void SetFilterCardImageActive(bool active)
    {
        if (filterCardImageObject != null)
            filterCardImageObject.SetActive(active);
    }

    private void HideFilterCardImage()
    {
        if (filterCardObject != null)
            filterCardObject.SetActive(false);
    }

    private void RewireRotateButtons()
    {
        if (activeCardRotator == null)
            return;

        var rightButton = rotateRightButtonObject != null ? rotateRightButtonObject.GetComponent<Button>() : null;
        if (rightButton != null)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(activeCardRotator.RotateRight);
        }

        var leftButton = rotateLeftButtonObject != null ? rotateLeftButtonObject.GetComponent<Button>() : null;
        if (leftButton != null)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(activeCardRotator.RotateLeft);
        }
    }

    private void PrepareBookOverlayForCardDrop(GameObject book)
    {
        if (book == null)
            return;

        var reader = book.GetComponent<BookOverlayPagedReader>();
        if (reader != null)
            reader.enabled = false;

        var canvas = book.GetComponent<Canvas>();
        if (canvas != null)
            Destroy(canvas);

        var scaler = book.GetComponent<CanvasScaler>();
        if (scaler != null)
            Destroy(scaler);

        var raycaster = book.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            Destroy(raycaster);

        Transform dimOverlay = book.transform.Find("DimOverlay");
        if (dimOverlay != null)
            dimOverlay.gameObject.SetActive(false);

        foreach (var button in book.GetComponentsInChildren<Button>(true))
        {
            if (IsCloseButtonContent(button.transform))
                continue;

            button.gameObject.SetActive(false);
        }

        foreach (var text in book.GetComponentsInChildren<Text>(true))
        {
            if (IsCloseButtonContent(text.transform))
                continue;

            text.gameObject.SetActive(false);
        }

        foreach (var image in book.GetComponentsInChildren<Image>(true))
        {
            if (IsCloseButtonContent(image.transform))
                continue;

            image.raycastTarget = false;
        }

        EnsureVisibleBookBackground(book);
        KeepCloseButtonOnTop();
    }

    private void KeepCloseButtonOnTop()
    {
        if (resolvedBook == null)
            return;

        Transform closeButton = resolvedBook.Find("CloseButton");
        if (closeButton != null)
            closeButton.SetAsLastSibling();
    }

    private static bool IsCloseButtonContent(Transform target)
    {
        while (target != null)
        {
            if (target.name == "CloseButton")
                return true;

            target = target.parent;
        }

        return false;
    }

    private void EnsureVisibleBookBackground(GameObject book)
    {
        var background = book.GetComponent<Image>();
        if (background == null)
            background = book.AddComponent<Image>();

        background.enabled = true;
        background.color = bookOverlayBackgroundColor;
        background.raycastTarget = false;
        background.SetAllDirty();
    }

    private void ApplyBookOverlayLayout(RectTransform rect)
    {
        if (rect == null)
            return;

        if (useBookOverlayPrefabLayout)
        {
            rect.anchorMin = new Vector2(0.04f, 0.06f);
            rect.anchorMax = new Vector2(0.96f, 0.94f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            return;
        }

        Vector2 targetSize = bookOverlaySize;
        var cardRect = filterCardObject != null ? filterCardObject.GetComponent<RectTransform>() : null;
        if ((targetSize.x <= 0f || targetSize.y <= 0f) && cardRect != null)
            targetSize = cardRect.sizeDelta + new Vector2(120f, 120f);

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = targetSize;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>
    /// bookOverlayInstance가 비어 있을 때만 사용하는 폴백. BookOverlayPanelA 프리팹을
    /// 이 패널의 자식으로 생성하고 패널을 가득 채우게 늘린다.
    /// </summary>
    private RectTransform SpawnBookOverlay()
    {
        GameObject prefab = bookOverlayPrefab != null
            ? bookOverlayPrefab
            : Resources.Load<GameObject>(bookOverlayResourcePath);

        if (prefab == null)
        {
            GameLog.LogWarning($"[FilterCardBookDropZone] BookOverlayPanelA 프리팹을 찾지 못했습니다. (경로: {bookOverlayResourcePath})");
            return null;
        }

        GameObject book = Instantiate(prefab, transform);
        book.name = "BookOverlayPanelA";

        var rect = book.GetComponent<RectTransform>();
        if (rect != null)
        {
            ApplyBookOverlayLayout(rect);
            rect.SetAsFirstSibling();
        }

        book.SetActive(true);
        book.transform.SetAsFirstSibling();
        return rect;
    }
}
