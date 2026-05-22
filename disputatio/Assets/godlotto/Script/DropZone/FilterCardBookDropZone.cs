using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// CardStackPanel 아래 새 책 패널에 부착되는 드롭 존. 인벤토리의 FilterCard 아이템을
/// 드롭하면 BookOverlayPanelA 안의 FilterCardImage를 활성화해서 자유 이동/회전할 수
/// 있도록 만든다.
/// </summary>
public class FilterCardBookDropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("상호작용 오브젝트")]
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

        if (currentUses >= maxUses)
        {
            GameLog.Log(itemName + " 은(는) 더 이상 사용할 수 없습니다.");
            return;
        }

        currentUses++;
        GameLog.Log(itemName + " 을(를) 책 패널에 사용했습니다. (" + currentUses + "/" + maxUses + ")");

        // 1) 드래그 경계가 될 책 패널을 확보한다. 씬에 미리 배치된 인스턴스를 우선 사용한다.
        EnsureBookOverlay();

        // 2) BookOverlayPanelA 안의 이미지 카드만 활성화한다.
        HideFilterCardImage();
        EnsureFilterCardImage();
        ResetFilterCardImage();
        SetFilterCardImageActive(true);
        RewireRotateButtons();

        // 4) 회전 버튼 표시 (기존 회전 흐름 유지).
        if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(true);
        if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(true);

        // 5) 인벤토리에서 아이템을 소비한다.
        if (currentUses >= maxUses && InventoryManager.instance != null)
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
        filterCardImageRect.sizeDelta = new Vector2(710.8362f, 695.5485f);
        filterCardImageRect.localScale = Vector3.one;
        filterCardImageRect.localRotation = Quaternion.identity;
        filterCardImageRect.SetAsLastSibling();
        KeepCloseButtonOnTop();

        var drag = filterCardImageObject != null ? filterCardImageObject.GetComponent<FilterCardBoundedDrag>() : null;
        if (drag != null)
            drag.SetBounds(resolvedBook != null ? resolvedBook : transform as RectTransform);
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
