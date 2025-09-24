using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    [Header("Inventory Data")]
    public List<Item> items = new List<Item>();
    public Item selectedItem;

    [Header("Inventory UI")]
    public GameObject inventoryUI_Background;
    public Transform slotsHolder;
    public GameObject slotPrefab;
    public int maxSlots = 12;

    private List<InventorySlot> slots = new List<InventorySlot>();
    private Animator animator;
    private bool isOpen = false;

    void Awake()
    {
        if (instance != null) { Destroy(gameObject); return; }
        instance = this;
    }

    void Start()
    {
        animator = inventoryUI_Background.GetComponent<Animator>();
        inventoryUI_Background.SetActive(false);
        CreateSlots();
        UpdateUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                inventoryUI_Background.SetActive(true);
                animator.SetTrigger("Open");
            }
            else
            {
                animator.SetTrigger("Close");
            }
        }
    }

    void CreateSlots()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            GameObject slotGO = Instantiate(slotPrefab, slotsHolder);
            slots.Add(slotGO.GetComponent<InventorySlot>());
        }
    }

    public void AddItem(Item item)
    {
        items.Add(item);
        UpdateUI();
    }

    void UpdateUI()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                slots[i].AddItem(items[i]);
            }
            else
            {
                slots[i].ClearSlot();
            }
        }
    }

    public void SelectItem(Item item)
    {
        selectedItem = item;
        Debug.Log(item.itemName + " 선택됨.");
        if (item.icon != null)
        {
            Cursor.SetCursor(item.icon.texture, Vector2.zero, CursorMode.Auto);
        }
    }

    public void DeselectItem()
    {
        Debug.Log("DeselectItem 함수가 호출되었습니다!", this.gameObject);


        selectedItem = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }


    public void UseItemOn(GameObject targetObject)
{
    // 1. 선택된 아이템이 있는지 확인
    if (selectedItem == null)
    {
        Debug.Log("사용할 아이템이 선택되지 않았습니다.");
        return;
    }

    Debug.Log(selectedItem.itemName + " 아이템을 " + targetObject.name + "에 사용합니다.");

    // 2. 아이템과 대상이 맞는지 조건 확인 (예시)
    if (selectedItem.itemName == "열쇠" && targetObject.name == "잠긴 문")
    {
        Debug.Log("문이 열렸습니다!");
        // 여기에 문을 여는 실제 로직 추가 (예: targetObject.SetActive(false);)

        items.Remove(selectedItem); // 인벤토리에서 사용한 아이템 제거
        UpdateUI();                 // UI 갱신
        DeselectItem();             // 아이템 선택 해제 및 커서 복구
    }
    else
    {
        Debug.Log("아무 일도 일어나지 않았습니다.");
        DeselectItem(); // 잘못된 사용이므로 아이템 선택만 해제
    }
}
}