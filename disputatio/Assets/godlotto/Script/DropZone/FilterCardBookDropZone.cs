using Godlotto.Interaction;
using Godlotto.ModalInput;
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

    [Tooltip("거울 숫자 퍼즐에서는 5도 미세 회전 버튼을 표시한다. 이전 호환이 필요할 때만 켠다.")]
    public bool hideRotateButtonsForDiaryMirror = false;

    private int currentUses = 0;
    private RectTransform resolvedBook;
    private GameObject filterCardImageObject;
    private RectTransform filterCardImageRect;
    private FilterCardRotator activeCardRotator;
    private FilterCardBoundedDrag activeCardDrag;
    private bool fallbackDraggingMirror;
    private Vector2 fallbackPointerOffset;
    private bool hasStarted;
    private bool hasPlacedMirror;

    private void Start()
    {
        hasStarted = true;
        EnsureBookOverlay();
        EnsureFilterCardImage();
        SetFilterCardImageActive(false);
        HideFilterCardImage();
        if (diaryMirrorPuzzleController != null)
            diaryMirrorPuzzleController.ShowHalfCodeClue(resolvedBook);
        StyleDiaryMirrorDropPanel();
        RewireRotateButtons();

        // 기존 DropZone과 동일: 시작 시 회전 버튼을 숨긴다.
        if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(false);
        if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (!hasStarted || diaryMirrorPuzzleController == null)
            return;

        RestoreDiaryMirrorDropPanel();
    }

    private void Update()
    {
        if (TryHandleCloseButtonClick())
            return;

        UpdateDiaryMirrorFallbackDrag();
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
        hasPlacedMirror = true;

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

    private void RestoreDiaryMirrorDropPanel()
    {
        fallbackDraggingMirror = false;
        EnsureBookOverlay();

        if (hasPlacedMirror)
        {
            EnsureFilterCardImage();
            ResetFilterCardImage();
            SetFilterCardImageActive(true);
            RewireRotateButtons();

            bool showRotateButtons = !hideRotateButtonsForDiaryMirror || diaryMirrorPuzzleController == null;
            if (rotateRightButtonObject != null)
                rotateRightButtonObject.SetActive(showRotateButtons);
            if (rotateLeftButtonObject != null)
                rotateLeftButtonObject.SetActive(showRotateButtons);

            if (diaryMirrorPuzzleController != null)
                diaryMirrorPuzzleController.NotifyMirrorCardActivated(filterCardImageRect, activeCardRotator);

            KeepCloseButtonOnTop();
            return;
        }

        HideFilterCardImage();
        SetFilterCardImageActive(false);

        if (rotateRightButtonObject != null)
            rotateRightButtonObject.SetActive(false);

        if (rotateLeftButtonObject != null)
            rotateLeftButtonObject.SetActive(false);

        if (diaryMirrorPuzzleController != null)
            diaryMirrorPuzzleController.ShowHalfCodeClue(resolvedBook);

        KeepCloseButtonOnTop();
    }

    private void EnsureBookOverlay()
    {
        if (resolvedBook == null)
        {
            resolvedBook = bookOverlayInstance != null ? bookOverlayInstance : FindExistingBookOverlay();
            if (resolvedBook == null)
                resolvedBook = SpawnBookOverlay();

            if (resolvedBook != null)
            {
                PrepareBookOverlayForCardDrop(resolvedBook.gameObject);
                ApplyBookOverlayLayout(resolvedBook);
            }
        }

        if (resolvedBook != null)
        {
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
            ConfigureFilterCardInteractions();
            if (diaryMirrorPuzzleController != null)
                HideLegacyRotateChrome();
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

        ConfigureFilterCardInteractions();
        filterCardImageRect.SetAsLastSibling();
        KeepCloseButtonOnTop();
    }

    private void ConfigureFilterCardInteractions()
    {
        if (filterCardImageObject == null)
            return;

        var image = filterCardImageObject.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;
        DisableChildRaycastTargets(filterCardImageObject.transform);

        activeCardDrag = filterCardImageObject.GetComponent<FilterCardBoundedDrag>();
        if (activeCardDrag == null)
            activeCardDrag = filterCardImageObject.AddComponent<FilterCardBoundedDrag>();
        activeCardDrag.enabled = true;
        activeCardDrag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);

        activeCardRotator = filterCardImageObject.GetComponent<FilterCardRotator>();
        if (activeCardRotator == null)
            activeCardRotator = filterCardImageObject.AddComponent<FilterCardRotator>();
        activeCardRotator.enabled = true;

        if (diaryMirrorPuzzleController != null)
            activeCardRotator.ConfigureRotationSteps(5f, 5f);
    }

    private void UpdateDiaryMirrorFallbackDrag()
    {
        if (diaryMirrorPuzzleController == null
            || filterCardImageRect == null
            || filterCardImageObject == null
            || !filterCardImageObject.activeInHierarchy)
        {
            fallbackDraggingMirror = false;
            return;
        }

        Camera eventCamera = ResolveCanvasCamera();
        RectTransform parent = filterCardImageRect.parent as RectTransform;
        if (parent == null)
            return;

        if (Input.GetMouseButtonDown(0)
            && !IsPointerOverCloseButton(Input.mousePosition, eventCamera)
            && RectTransformUtility.RectangleContainsScreenPoint(filterCardImageRect, Input.mousePosition, eventCamera)
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                Input.mousePosition,
                eventCamera,
                out Vector2 localPoint))
        {
            fallbackDraggingMirror = true;
            fallbackPointerOffset = filterCardImageRect.anchoredPosition - localPoint;
        }

        if (!fallbackDraggingMirror)
            return;

        if (!Input.GetMouseButton(0))
        {
            fallbackDraggingMirror = false;
            if (activeCardDrag != null)
                activeCardDrag.NotifyExternalDragEnded();
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            Input.mousePosition,
            eventCamera,
            out Vector2 currentLocalPoint))
        {
            return;
        }

        filterCardImageRect.anchoredPosition = currentLocalPoint + fallbackPointerOffset;
        ClampMirrorInsideResolvedBook();
    }

    private Camera ResolveCanvasCamera()
    {
        var canvas = filterCardImageObject != null ? filterCardImageObject.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private void ClampMirrorInsideResolvedBook()
    {
        if (filterCardImageRect == null)
            return;

        RectTransform bounds = resolvedBook != null ? resolvedBook : transform as RectTransform;
        RectTransform parent = filterCardImageRect.parent as RectTransform;
        if (bounds == null || parent == null)
            return;

        Vector3[] cardCorners = new Vector3[4];
        Vector3[] boundCorners = new Vector3[4];
        filterCardImageRect.GetWorldCorners(cardCorners);
        bounds.GetWorldCorners(boundCorners);

        float cardMinX = Mathf.Min(cardCorners[0].x, cardCorners[1].x, cardCorners[2].x, cardCorners[3].x);
        float cardMaxX = Mathf.Max(cardCorners[0].x, cardCorners[1].x, cardCorners[2].x, cardCorners[3].x);
        float cardMinY = Mathf.Min(cardCorners[0].y, cardCorners[1].y, cardCorners[2].y, cardCorners[3].y);
        float cardMaxY = Mathf.Max(cardCorners[0].y, cardCorners[1].y, cardCorners[2].y, cardCorners[3].y);

        float boundMinX = Mathf.Min(boundCorners[0].x, boundCorners[1].x, boundCorners[2].x, boundCorners[3].x);
        float boundMaxX = Mathf.Max(boundCorners[0].x, boundCorners[1].x, boundCorners[2].x, boundCorners[3].x);
        float boundMinY = Mathf.Min(boundCorners[0].y, boundCorners[1].y, boundCorners[2].y, boundCorners[3].y);
        float boundMaxY = Mathf.Max(boundCorners[0].y, boundCorners[1].y, boundCorners[2].y, boundCorners[3].y);

        Vector3 worldOffset = Vector3.zero;
        if (cardMinX < boundMinX)
            worldOffset.x = boundMinX - cardMinX;
        else if (cardMaxX > boundMaxX)
            worldOffset.x = boundMaxX - cardMaxX;

        if (cardMinY < boundMinY)
            worldOffset.y = boundMinY - cardMinY;
        else if (cardMaxY > boundMaxY)
            worldOffset.y = boundMaxY - cardMaxY;

        if (worldOffset != Vector3.zero)
            filterCardImageRect.position += worldOffset;
    }

    private static void DisableChildRaycastTargets(Transform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            foreach (var graphic in child.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }
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

        SetFilterCardImageParent();

        if (diaryMirrorPuzzleController != null)
        {
            ApplyBookmarkMirrorLayout();
            HideLegacyRotateChrome();
            KeepCloseButtonOnTop();
            return;
        }

        filterCardImageRect.anchoredPosition = Vector2.zero;
        filterCardImageRect.sizeDelta = ResolveFilterCardImageSize();
        filterCardImageRect.localScale = Vector3.one;
        filterCardImageRect.localRotation = Quaternion.identity;
        filterCardImageRect.SetAsLastSibling();
        KeepCloseButtonOnTop();

        var drag = filterCardImageObject != null ? filterCardImageObject.GetComponent<FilterCardBoundedDrag>() : null;
        if (drag != null)
            drag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);

        ConfigureFilterCardInteractions();
    }

    private void ApplyBookmarkMirrorLayout()
    {
        filterCardImageRect.pivot = new Vector2(0.5f, 1f);
        filterCardImageRect.anchorMin = new Vector2(0.5f, 1f);
        filterCardImageRect.anchorMax = new Vector2(0.5f, 1f);
        filterCardImageRect.anchoredPosition = new Vector2(0f, -28f);
        filterCardImageRect.sizeDelta = ResolveFilterCardImageSize();
        filterCardImageRect.localScale = Vector3.one;
        filterCardImageRect.localRotation = Quaternion.identity;
        filterCardImageRect.SetAsLastSibling();

        var image = filterCardImageObject != null ? filterCardImageObject.GetComponent<Image>() : null;
        if (image != null)
            image.preserveAspect = true;

        var drag = filterCardImageObject != null ? filterCardImageObject.GetComponent<FilterCardBoundedDrag>() : null;
        if (drag != null)
            drag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);
    }

    private void HideLegacyRotateChrome()
    {
        if (filterCardImageRect == null)
            return;

        Transform rotateImage = filterCardImageRect.Find("RotateImage");
        if (rotateImage != null)
            rotateImage.gameObject.SetActive(false);
    }

    private void StyleDiaryMirrorDropPanel()
    {
        if (diaryMirrorPuzzleController == null)
            return;

        var panelImage = GetComponent<Image>();
        if (panelImage == null)
            return;

        panelImage.color = new Color(1f, 0.95f, 0.82f, 0.06f);
        panelImage.raycastTarget = true;
    }

    private Vector2 ResolveFilterCardImageSize()
    {
        if (diaryMirrorPuzzleController == null)
            return new Vector2(710.8362f, 695.5485f);

        var image = filterCardImageObject != null ? filterCardImageObject.GetComponent<Image>() : null;
        Sprite sprite = image != null ? image.sprite : null;
        if (sprite == null || sprite.rect.height <= 0f)
            return new Vector2(56f, 280f);

        float targetHeight = 280f;
        float targetWidth = targetHeight * (sprite.rect.width / sprite.rect.height);
        return new Vector2(Mathf.Clamp(targetWidth, 48f, 110f), targetHeight);
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

        if (book.GetComponent<ModalInputScope>() == null)
            book.AddComponent<ModalInputScope>();

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
        {
            EnsureCloseButtonInteractive(closeButton);
            closeButton.SetAsLastSibling();
        }
    }

    private bool IsPointerOverCloseButton(Vector2 screenPosition, Camera eventCamera)
    {
        if (resolvedBook == null)
            return false;

        Transform closeButton = resolvedBook.Find("CloseButton");
        RectTransform closeRect = closeButton as RectTransform;
        return closeRect != null
            && closeButton.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(closeRect, screenPosition, eventCamera);
    }

    private bool TryHandleCloseButtonClick()
    {
        if (resolvedBook == null || !resolvedBook.gameObject.activeInHierarchy || !Input.GetMouseButtonDown(0))
            return false;

        Camera eventCamera = ResolveCanvasCamera();
        if (!IsPointerOverCloseButton(Input.mousePosition, eventCamera))
            return false;

        CloseBookOverlayFromPuzzle();
        return true;
    }

    private void EnsureCloseButtonInteractive(Transform closeButton)
    {
        if (closeButton == null)
            return;

        closeButton.gameObject.SetActive(true);

        var image = closeButton.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;

        var button = closeButton.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
            button.onClick.RemoveListener(CloseBookOverlayFromPuzzle);
            button.onClick.AddListener(CloseBookOverlayFromPuzzle);
        }

        foreach (var graphic in closeButton.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.transform == closeButton)
                continue;

            graphic.raycastTarget = false;
        }
    }

    private void CloseBookOverlayFromPuzzle()
    {
        fallbackDraggingMirror = false;

        if (filterCardImageObject != null)
            filterCardImageObject.SetActive(false);

        if (rotateRightButtonObject != null)
            rotateRightButtonObject.SetActive(false);

        if (rotateLeftButtonObject != null)
            rotateLeftButtonObject.SetActive(false);

        if (resolvedBook != null)
            resolvedBook.gameObject.SetActive(false);

        var cardStackPanel = FindAncestorNamed("CardStackPanel");
        if (cardStackPanel != null)
            cardStackPanel.SetActive(false);

        InteractionLock.ForceUnlock();
        var flowchart = FlowchartLocator.Find();
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked: false);
        DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
    }

    private GameObject FindAncestorNamed(string ancestorName)
    {
        var current = transform.parent;
        while (current != null)
        {
            if (current.name == ancestorName)
                return current.gameObject;

            current = current.parent;
        }

        return null;
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
