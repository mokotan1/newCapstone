using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Fungus;

public class InventoryManager : SingletonMonoBehaviour<InventoryManager>
{
    protected override bool PersistAcrossScenes => true;

    [System.Obsolete("Use Instance instead.")]
    public static InventoryManager instance => Instance;

    [Header("Inventory Data")]
    [SerializeField] private List<Item> items = new List<Item>();
    [SerializeField] private Item selectedItem;

    public IReadOnlyList<Item> Items => items;
    public Item SelectedItem => selectedItem;

    [Header("Inventory UI")]
    [SerializeField] private GameObject inventoryUI_Background;
    [SerializeField] private Transform slotsHolder;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private int maxSlots = 12;
    [SerializeField] private Flowchart targetflowchart;
    [SerializeField] private InventoryTooltipController tooltipController;
    [SerializeField] private InventoryGuideController guideController;

    [Header("슬롯 레이아웃")]
    [Tooltip("슬롯 사이의 가로/세로 간격 (픽셀)")]
    [SerializeField] private Vector2 slotSpacing = new Vector2(50f, 0f);
    [Tooltip("슬롯 하나의 가로/세로 크기 (픽셀)")]
    [SerializeField] private Vector2 cellSize = new Vector2(150f, 150f);

    [SerializeField] private bool pressTab = false;

    private List<InventorySlot> slots = new List<InventorySlot>();
    private Animator animator;
    private bool isOpen = false;

    protected override void OnSingletonAwake()
    {
        targetflowchart = ResolveFlowchart();
        var fallback = FindFirstObjectByType<InventoryTooltipController>(FindObjectsInactive.Include);
        if (tooltipController == null && fallback != null)
            GameLog.LogWarning($"[{nameof(InventoryManager)}] tooltipController resolved via FindFirstObjectByType — assign in Inspector for faster startup.");
        tooltipController = SelectTooltipController(tooltipController, fallback);
        if (tooltipController == null)
        {
            tooltipController = gameObject.AddComponent<InventoryTooltipController>();
            GameLog.LogWarning($"[{nameof(InventoryManager)}] tooltipController가 없어 런타임에 자동 생성했습니다.");
        }

        var guideFallback = FindFirstObjectByType<InventoryGuideController>(FindObjectsInactive.Include);
        guideController = SelectGuideController(guideController, guideFallback);
        if (guideController == null)
            guideController = gameObject.AddComponent<InventoryGuideController>();
    }

    void OnEnable()
    {
        SaveManagerSignals.OnSaveReset += OnSaveReset;
        SaveManagerSignals.OnSavePointLoaded += OnSavePointLoaded;
    }

    void OnDisable()
    {
        SaveManagerSignals.OnSaveReset -= OnSaveReset;
        SaveManagerSignals.OnSavePointLoaded -= OnSavePointLoaded;
    }

    void Start()
    {
        if (inventoryUI_Background == null)
        {
            GameLog.LogWarning($"[{nameof(InventoryManager)}] inventoryUI_Background가 할당되지 않았습니다.");
            return;
        }

        animator = inventoryUI_Background.GetComponent<Animator>();
        EnsureInventoryRootActive(inventoryUI_Background);
        NormalizeInventoryCanvasTransform(inventoryUI_Background != null ? inventoryUI_Background.transform : null);
        SetInitialClosedState();
        if (guideController != null)
            guideController.BindInventoryRoot(inventoryUI_Background.transform);

        if (ResolveFlowchart() != null)
            pressTab = targetflowchart.GetBooleanVariable(FungusVariableKeys.PressTab);

        CreateSlots();
        UpdateUI();
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Tab))
            return;

        if (!InventoryAccessState.ShouldAllowInventoryInput(InventoryAccessState.IsUnlocked))
            return;

        EnsureInventoryRootActive(inventoryUI_Background);
        NormalizeInventoryCanvasTransform(inventoryUI_Background != null ? inventoryUI_Background.transform : null);

        isOpen = !isOpen;
        if (isOpen)
        {
            pressTab = true;
            SyncPressTab();
            guideController?.OnInventoryOpened();
            animator?.SetTrigger("Open");
        }
        else
        {
            pressTab = false;
            SyncPressTab();
            guideController?.HideGuide();
            animator?.SetTrigger("Close");

            if (selectedItem != null)
                DeselectItem();
        }
    }

    public void AddItem(Item item)
    {
        TryAddItemInternal(item, respectSlotLimit: true);
    }

    /// <summary>개발자 모드 전용: 슬롯 한도를 무시하고 아이템을 추가합니다.</summary>
    internal bool TryAddItemForDeveloperMode(Item item)
    {
        if (!DeveloperModeController.CanUseDeveloperModeRuntime || !DeveloperModeController.IsDeveloperModeEnabled)
            return false;

        return TryAddItemInternal(item, respectSlotLimit: false);
    }

    /// <summary>개발자 모드 전용: UI 슬롯 수를 최소 <paramref name="minimumSlots"/>까지 늘립니다.</summary>
    internal void EnsureDeveloperSlotCapacity(int minimumSlots)
    {
        if (!DeveloperModeController.CanUseDeveloperModeRuntime || !DeveloperModeController.IsDeveloperModeEnabled)
            return;

        int target = Mathf.Clamp(minimumSlots, slots.Count, DeveloperModeItemGrantService.MaxDeveloperInventorySlots);
        while (slots.Count < target)
            CreateSingleSlot();
    }

    private bool TryAddItemInternal(Item item, bool respectSlotLimit)
    {
        if (item == null)
            return false;

        if (items.Contains(item))
        {
            GameLog.Log($"[InventoryManager] {item.itemName}은(는) 이미 인벤토리에 있습니다. 중복 추가 무시.");
            return false;
        }

        if (ResolveFlowchart() != null)
            ItemAcquisitionTracker.MarkAcquired(targetflowchart, item);

        if (respectSlotLimit && items.Count >= maxSlots)
        {
            GameLog.Log($"인벤토리가 가득 찼습니다! {item.itemName}을(를) 더 이상 추가할 수 없습니다.");
            return false;
        }

        items.Add(item);
        UpdateUI();
        return true;
    }

    public void RemoveItem(Item item)
    {
        if (item == null)
            return;

        items.Remove(item);

        if (selectedItem == item)
            DeselectItem();

        UpdateUI();
    }

    public void RestoreItemsByIds(IEnumerable<int> itemIds)
    {
        items.Clear();
        if (selectedItem != null)
            DeselectItem();

        if (itemIds != null)
        {
            foreach (int itemId in itemIds)
            {
                Item found = ItemLookup.FindById(itemId);
                if (found != null && !items.Contains(found))
                    items.Add(found);
                else if (found == null)
                    GameLog.LogWarning($"[InventoryManager] 체크포인트 복원 실패: itemId={itemId} 에 해당하는 Item을 찾을 수 없습니다.");
            }
        }

        UpdateUI();
    }

    public void SelectItem(Item item)
    {
        if (selectedItem == item)
        {
            DeselectItem();
            return;
        }

        selectedItem = item;
        GameLog.Log($"{item.itemName} 을(를) 손에 들었다.");
    }

    public void DeselectItem()
    {
        selectedItem = null;
        GameLog.Log("손에 든 아이템을 내려놓았다.");
    }

    public void ShowTooltip(Item item, Vector2 screenPosition)
    {
        if (tooltipController == null || item == null)
            return;

        tooltipController.Show(item, screenPosition);
    }

    public void HideTooltip()
    {
        if (tooltipController == null)
            return;

        tooltipController.Hide();
    }

    private void CreateSlots()
    {
        if (slotPrefab == null)
        {
            GameLog.LogWarning($"[{nameof(InventoryManager)}] slotPrefab이 할당되지 않았습니다. Inspector에서 InventorySlot 프리팹을 지정해 주세요.");
            return;
        }

        if (slotsHolder == null)
        {
            GameLog.LogWarning($"[{nameof(InventoryManager)}] slotsHolder가 할당되지 않았습니다.");
            return;
        }

        if (slotsHolder.TryGetComponent<GridLayoutGroup>(out var grid))
        {
            grid.spacing = slotSpacing;
            grid.cellSize = cellSize;
        }

        for (int i = 0; i < maxSlots; i++)
            CreateSingleSlot();
    }

    private void CreateSingleSlot()
    {
        if (slotPrefab == null || slotsHolder == null)
            return;

        GameObject slotGO = Instantiate(slotPrefab, slotsHolder);
        var slot = slotGO.GetComponent<InventorySlot>()
                   ?? slotGO.GetComponentInChildren<InventorySlot>();
        if (slot != null)
        {
            slots.Add(slot);
            return;
        }

        GameLog.LogWarning($"[{nameof(InventoryManager)}] slotPrefab({slotPrefab.name})에 InventorySlot 컴포넌트가 없습니다. " +
                           $"프리팹 경로를 확인해 주세요.");
        Destroy(slotGO);
    }

    private void UpdateUI()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;

            if (i < items.Count)
                slots[i].AddItem(items[i]);
            else
                slots[i].ClearSlot();
        }
    }

    private void SyncPressTab()
    {
        if (ResolveFlowchart() != null)
            targetflowchart.SetBooleanVariable(FungusVariableKeys.PressTab, pressTab);
    }

    private void OnSaveReset()
    {
        items.Clear();
        if (selectedItem != null)
            DeselectItem();
        UpdateUI();
    }

    private void OnSavePointLoaded(string savePointKey)
    {
        RestoreInventoryFromFlowchart();
    }

    private void RestoreInventoryFromFlowchart()
    {
        Flowchart fc = FlowchartLocator.Find();
        if (fc == null) return;

        string raw = fc.GetStringVariable(FungusVariableKeys.InventoryItemIds);
        items.Clear();
        if (selectedItem != null)
            DeselectItem();

        if (!string.IsNullOrEmpty(raw))
        {
            foreach (string idStr in raw.Split(','))
            {
                if (int.TryParse(idStr, out int id))
                {
                    Item found = ItemLookup.FindById(id);
                    if (found != null)
                        items.Add(found);
                    else
                        GameLog.LogWarning($"[InventoryManager] 복원 실패: itemId={id} 에 해당하는 Item을 찾을 수 없습니다.");
                }
            }
        }

        UpdateUI();
    }

    private Flowchart ResolveFlowchart()
    {
        if (targetflowchart == null)
            targetflowchart = FlowchartLocator.Resolve(null);

        return targetflowchart;
    }

    public static InventoryTooltipController SelectTooltipController(
        InventoryTooltipController assignedController,
        InventoryTooltipController discoveredController)
    {
        return assignedController != null ? assignedController : discoveredController;
    }

    public static InventoryGuideController SelectGuideController(
        InventoryGuideController assignedController,
        InventoryGuideController discoveredController)
    {
        return assignedController != null ? assignedController : discoveredController;
    }

    internal static void EnsureInventoryRootActive(GameObject inventoryRoot)
    {
        if (inventoryRoot != null && !inventoryRoot.activeSelf)
            inventoryRoot.SetActive(true);
    }

    internal static void NormalizeInventoryCanvasTransform(Transform inventoryRoot)
    {
        if (inventoryRoot == null)
            return;

        Canvas canvas = inventoryRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null)
            return;

        Transform canvasTransform = canvas.transform;
        if (canvasTransform.localScale == Vector3.zero)
            canvasTransform.localScale = Vector3.one;
    }

    private void SetInitialClosedState()
    {
        isOpen = false;
        pressTab = false;
        SyncPressTab();

        if (animator == null)
            return;

        animator.Play("Inventory_Close", 0, 1f);
        animator.Update(0f);
    }
}
