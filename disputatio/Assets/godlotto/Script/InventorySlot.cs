using UnityEngine;

using UnityEngine.UI;

using UnityEngine.EventSystems;

using Fungus;



public class InventorySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler

{

    const int DragIconSortingOrder = 5;

    internal const bool TraceDrag = true;



    [Header("UI Components")]

    public Image icon;

    private Item item;



    [Header("Drag & Drop")]

    private static GameObject dragIcon;

    public static Item draggedItem;



    CanvasGroup slotCanvasGroup;



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

        if (item == null)

        {

            LogDrag("OnBeginDrag skipped: empty slot");

            return;

        }



        ClearDragState();



        draggedItem = item;

        Sprite dragSprite = icon != null ? icon.sprite : null;

        if (icon != null)

            icon.enabled = false;



        BeginSlotDragPassthrough();



        dragIcon = new GameObject("DragIcon");

        Transform dragParent = ResolveDragIconParent(transform);

        dragIcon.transform.SetParent(dragParent, false);

        dragIcon.transform.SetAsLastSibling();



        var image = dragIcon.AddComponent<Image>();

        image.sprite = dragSprite;

        image.raycastTarget = false;

        image.preserveAspect = true;



        var dragCanvas = dragIcon.AddComponent<Canvas>();

        dragCanvas.overrideSorting = true;

        dragCanvas.sortingOrder = DragIconSortingOrder;



        var dragRect = dragIcon.GetComponent<RectTransform>();

        dragRect.sizeDelta = new Vector2(100f, 100f);

        SetDragIconScreenPosition(eventData, dragRect, dragParent);



        LogDrag(

            $"OnBeginDrag item={item.itemName} parent={dragParent.name} " +

            $"pos={dragRect.anchoredPosition} blocksRaycasts={GetSlotBlocksRaycasts()}");

    }



    private void OnDisable()

    {

        EndSlotDragPassthrough();

        RestoreSlotIcon();

        ClearDragState();

    }



    public void OnDrag(PointerEventData eventData)

    {

        if (dragIcon == null)

            return;



        SetDragIconScreenPosition(

            eventData,

            dragIcon.GetComponent<RectTransform>(),

            dragIcon.transform.parent);



        LogDrag($"OnDrag screen={eventData.position}");

    }



    public void OnEndDrag(PointerEventData eventData)

    {

        GameObject dropTarget = eventData.pointerEnter;

        if (IsDragIconPointer(dropTarget))

            dropTarget = null;



        LogDrag(

            $"OnEndDrag dropTarget={(dropTarget != null ? dropTarget.name : "null")} " +

            $"draggedItem={(draggedItem != null ? draggedItem.itemName : "null")} " +

            $"blocksRaycasts={GetSlotBlocksRaycasts()}");



        if (dropTarget != null)

        {

            if (dropTarget.GetComponent<DropZone>() != null

                || dropTarget.GetComponentInParent<DropZone>() != null)

            {

                LogDrag("OnEndDrag handled by UI DropZone");

                FinishDrag();

                return;

            }



            if (dropTarget.GetComponent<WorldItemDropZone>() != null

                || dropTarget.GetComponentInParent<WorldItemDropZone>() != null)

            {

                LogDrag("OnEndDrag handled by UI WorldItemDropZone");

                FinishDrag();

                return;

            }

        }



        Vector2 mouseWorld = ScreenToWorldPoint2D(Input.mousePosition);

        Collider2D[] atPoint = Physics2D.OverlapPointAll(mouseWorld);

        foreach (Collider2D col in atPoint)

        {

            WorldItemDropZone worldDropZone = WorldItemDropZone.FindFromHitCollider(col);

            if (worldDropZone != null && worldDropZone.TryApplyDroppedItem(draggedItem))

            {

                LogDrag($"OnEndDrag applied to world drop zone '{worldDropZone.gameObject.name}'");

                break;

            }

        }



        FinishDrag();

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



    void FinishDrag()

    {

        EndSlotDragPassthrough();

        RestoreSlotIcon();

        ClearDragState();

        LogDrag("OnEndDrag finished: slot restored");

    }



    void RestoreSlotIcon()

    {

        if (item != null && icon != null)

            icon.enabled = true;

    }



    void BeginSlotDragPassthrough()

    {

        if (!TryGetComponent(out slotCanvasGroup))

            slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();



        slotCanvasGroup.blocksRaycasts = false;

        slotCanvasGroup.interactable = true;

    }



    void EndSlotDragPassthrough()

    {

        if (slotCanvasGroup == null)

            return;



        slotCanvasGroup.blocksRaycasts = true;

        slotCanvasGroup.interactable = true;

    }



    bool GetSlotBlocksRaycasts()

    {

        return slotCanvasGroup != null && slotCanvasGroup.blocksRaycasts;

    }



    static bool IsDragIconPointer(GameObject target)

    {

        if (target == null || dragIcon == null)

            return false;



        return target == dragIcon || target.transform.IsChildOf(dragIcon.transform);

    }



    static void SetDragIconScreenPosition(PointerEventData eventData, RectTransform dragRect, Transform dragParent)

    {

        if (dragRect == null || dragParent == null)

            return;



        var parentRect = dragParent as RectTransform;

        if (parentRect == null)

        {

            dragRect.position = eventData.position;

            return;

        }



        Canvas canvas = dragParent.GetComponentInParent<Canvas>();

        Camera eventCamera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)

            eventCamera = canvas.worldCamera;



        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(

                parentRect,

                eventData.position,

                eventCamera,

                out Vector2 localPoint))

        {

            dragRect.anchoredPosition = localPoint;

        }

        else

        {

            dragRect.position = eventData.position;

        }

    }



    static Transform ResolveDragIconParent(Transform slotTransform)

    {

        if (slotTransform != null)

        {

            Canvas slotCanvas = slotTransform.GetComponentInParent<Canvas>();

            if (slotCanvas != null)

                return slotCanvas.transform;

        }



        if (InventoryManager.instance != null)

        {

            Canvas inventoryCanvas = InventoryManager.instance.GetComponentInChildren<Canvas>(true);

            if (inventoryCanvas != null)

                return inventoryCanvas.transform;

        }



        return slotTransform != null ? slotTransform.root : null;

    }



    public void OnSlotClicked()

    {

        if (item != null)

            InventoryManager.instance.SelectItem(item);

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



    static Vector2 ScreenToWorldPoint2D(Vector3 screenPosition)

    {

        Camera cam = Camera.main;

        if (cam == null)

            return Vector2.zero;



        Vector3 p = screenPosition;

        p.z = Mathf.Abs(cam.transform.position.z);

        return cam.ScreenToWorldPoint(p);

    }



    static void LogDrag(string message)

    {

        if (!TraceDrag)

            return;



        GameLog.Log($"[InventorySlot.Drag] {message}");

    }

}


