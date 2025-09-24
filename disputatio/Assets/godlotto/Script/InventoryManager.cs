using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    [Header("Inventory Data")]
    public List<Item> items = new List<Item>();
    public Item selectedItem; // 현재 '손에 든' 아이템을 저장할 변수

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
                // 인벤토리를 닫을 때 현재 선택된 아이템이 있다면 해제합니다.
                if (selectedItem != null)
                {
                    DeselectItem();
                }
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
        // 인벤토리가 가득 찼는지 확인
        if (items.Count >= maxSlots)
        {
            Debug.Log("인벤토리가 가득 찼습니다! " + item.itemName + "을(를) 더 이상 추가할 수 없습니다.");
            return;
        }

        items.Add(item);
        UpdateUI();
    }
    
    // 아이템 제거 기능 추가 (필요시 사용)
    public void RemoveItem(Item item)
    {
        items.Remove(item);
        // 만약 제거된 아이템이 현재 선택된 아이템이었다면 선택 해제
        if (selectedItem == item)
        {
            DeselectItem();
        }
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

    // InventorySlot이 이 함수를 호출하여 아이템을 '손에 듭니다'.
    public void SelectItem(Item item)
    {
        // 이미 같은 아이템을 들고 있다면 해제 (토글 기능)
        if (selectedItem == item)
        {
            DeselectItem();
            return;
        }

        selectedItem = item;
        Debug.Log(item.itemName + " 을(를) 손에 들었다.");
    }

    // 아이템 사용 후 '손에 든' 상태를 해제하는 함수
    public void DeselectItem()
    {
        Debug.Log("DeselectItem 함수가 호출되었습니다!", this.gameObject);


        selectedItem = null;
        Debug.Log("손에 든 아이템을 내려놓았다.");
    }
<<<<<<< HEAD:disputatio/Assets/godlotto/InventoryManager.cs


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
=======
    
>>>>>>> main:disputatio/Assets/godlotto/Script/InventoryManager.cs
}