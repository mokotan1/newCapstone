using System.Collections.Generic;
using UnityEngine;
using Fungus;
using System;

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
    [SerializeField]
    public Flowchart targetflowchart;

    public bool pressTab = false;



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
        pressTab = targetflowchart.GetBooleanVariable("pressTab");
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
                pressTab = true;
                targetflowchart.SetBooleanVariable("pressTab", pressTab);
                Debug.Log(pressTab);
                inventoryUI_Background.SetActive(true);
                animator.SetTrigger("Open");
            }
            else
            {
                pressTab = false;
                targetflowchart.SetBooleanVariable("pressTab", pressTab);
                Debug.Log(pressTab);
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

}