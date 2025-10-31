using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class WorldItemDropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("성공 시 실행될 이벤트")]
    public UnityEvent onUnlock; // Fungus 블록 실행, 효과음 등

    public void OnDrop(PointerEventData eventData)
    {
        // 드롭된 아이템이 올바른 열쇠인지 확인
        if (InventorySlot.draggedItem == requiredItem)
        {
            Debug.Log($"올바른 아이템({requiredItem.itemName})을 사용했습니다!");

            // ✅ 1. 인스펙터에서 연결된 이벤트 실행 (Fungus 블록 실행 포함 가능)
            onUnlock.Invoke();

            // ✅ 2. 인벤토리에서 사용한 아이템 제거
            InventoryManager.instance.RemoveItem(requiredItem);

            // ✅ 3. 이 드롭존은 한 번만 사용되도록 비활성화
            this.enabled = false;
        }
    }
}
