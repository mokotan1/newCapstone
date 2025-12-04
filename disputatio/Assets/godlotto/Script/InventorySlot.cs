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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item != null)
        {
            // 현재 드래그 중인 아이템 저장
            draggedItem = item;

            // 마우스를 따라다닐 드래그 아이콘 생성
            dragIcon = new GameObject("DragIcon");
            dragIcon.transform.SetParent(transform.root, false); 
            dragIcon.transform.SetAsLastSibling();
            
            var image = dragIcon.AddComponent<Image>();
            image.sprite = icon.sprite;
            image.raycastTarget = false; // 다른 UI 클릭 방해 X

            dragIcon.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragIcon != null)
        {
            dragIcon.transform.position = eventData.position;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그 아이콘 제거
        if (dragIcon != null)
        {
            Destroy(dragIcon);
        }

        draggedItem = null;   // 마지막에만 초기화
    }

    public void OnSlotClicked()
    {
        if (item != null)
        {
            InventoryManager.instance.SelectItem(item);
        }
    }
}
