using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 드래그 앤 드롭을 위해 반드시 필요합니다!

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Components")]
    public Image icon;
    private Item item;

    [Header("Drag & Drop")]
    private static GameObject dragIcon; // 드래그 시 마우스를 따라다닐 아이콘 (static으로 하나만 존재)
    public static Item draggedItem;     // 현재 드래그 중인 아이템 (static으로 공유)

    // (기존 AddItem, ClearSlot 함수는 그대로 둡니다)
    public void AddItem(Item newItem)
    {
        item = newItem;
        icon.sprite = item.icon;
        icon.enabled = true;

        Button button = GetComponent<Button>();
        if (button != null) button.interactable = true;
    }

    public void ClearSlot()
    {
        item = null;
        icon.sprite = null;
        icon.enabled = false;

        Button button = GetComponent<Button>();
        if (button != null) button.interactable = false;
    }

    // ★★★ 이 아래에 드래그 앤 드롭 함수들이 추가되었습니다 ★★★

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 슬롯에 아이템이 있을 때만 드래그를 시작합니다.
        if (item != null)
        {
            // 현재 드래그 중인 아이템 정보를 static 변수에 저장합니다.
            draggedItem = item;

            // 마우스를 따라다닐 드래그 아이콘을 생성합니다.
            dragIcon = new GameObject("DragIcon");
            dragIcon.transform.SetParent(transform.root, false); // Canvas 최상단에 생성
            dragIcon.transform.SetAsLastSibling(); // 항상 맨 위에 보이도록
            
            var image = dragIcon.AddComponent<Image>();
            image.sprite = icon.sprite; // 현재 슬롯의 아이콘을 복사
            image.raycastTarget = false; // 아이콘이 다른 UI의 클릭을 방해하지 않도록 함 (매우 중요!)

            // 아이콘 크기 조절 (원하는 크기로)
            dragIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 드래그 아이콘이 있다면 마우스 위치를 따라다니게 합니다.
        if (dragIcon != null)
        {
            dragIcon.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그가 끝나면 아이콘을 파괴하고, static 변수를 초기화합니다.
        if (dragIcon != null)
        {
            Destroy(dragIcon);
        }
        draggedItem = null;
    }

    // 기존의 클릭 기능은 그대로 두거나, 필요 없다면 삭제해도 됩니다.
    public void OnSlotClicked()
    {
        if (item != null)
        {
            InventoryManager.instance.SelectItem(item);
        }
    }
}