using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // 드래그 앤 드롭을 위해 반드시 필요합니다!
using Fungus;

public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
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
            ClearDragState();

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

    private void OnDisable()
    {
        ClearDragState();
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
        // 1. UI 드롭존이 먼저 처리했는지 확인합니다 (기존 기능).
        if (eventData.pointerEnter != null && eventData.pointerEnter.GetComponent<DropZone>() != null)
        {
            // DropZone.cs가 OnDrop을 실행할 것이므로, 여기서는 아무것도 할 필요가 없습니다.
            ClearDragState();
            return;
        }

        // 2. UI가 아니라면, 2D 월드에 드롭했는지 확인합니다.
        // Raycast(origin, Vector2.zero)는 방향이 0이라 히트가 나오지 않거나 불안정합니다. OverlapPoint/OverlapPointAll 사용.
        Vector2 mouseWorld = ScreenToWorldPoint2D(Input.mousePosition);
        Collider2D[] atPoint = Physics2D.OverlapPointAll(mouseWorld);
        foreach (Collider2D col in atPoint)
        {
            WorldItemDropZone worldDropZone = WorldItemDropZone.FindFromHitCollider(col);
            if (worldDropZone != null && worldDropZone.TryApplyDroppedItem(draggedItem))
                break;
        }

        // 드롭에 성공했든 실패했든 static 변수를 초기화합니다.
        ClearDragState();
    }

    public static void ClearDragState()
    {
        if (dragIcon != null)
        {
            if (Application.isPlaying)
                Destroy(dragIcon);
            else
                DestroyImmediate(dragIcon);

            dragIcon = null;
        }

        draggedItem = null;
    }

    public void OnSlotClicked()
    {
        if (item != null)
        {
            InventoryManager.instance.SelectItem(item);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item == null || InventoryManager.instance == null)
            return;

        InventoryManager.instance.ShowTooltip(item, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (InventoryManager.instance == null)
            return;

        InventoryManager.instance.HideTooltip();
    }

    /// <summary>스프라이트가 z=0 평면에 있다고 가정하고, ScreenToWorldPoint의 깊이(z)를 보정합니다.</summary>
    static Vector2 ScreenToWorldPoint2D(Vector3 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        Vector3 p = screenPosition;
        p.z = Mathf.Abs(cam.transform.position.z);
        return cam.ScreenToWorldPoint(p);
    }
}
