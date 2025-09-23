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
        selectedItem = null;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}