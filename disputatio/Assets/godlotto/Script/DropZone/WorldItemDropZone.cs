using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Fungus;

public class WorldItemDropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("성공 시 실행될 이벤트")]
    public UnityEvent onUnlock;

    [Header("Fungus 연동")]
    public Flowchart flowchart;              // 연결할 Flowchart
    public string dialogBoolName = "isTalking"; // Fungus Bool 변수 이름

    public void OnDrop(PointerEventData eventData)
    {
        // ❗ Fungus에서 대사 중인지 확인 (Boolean 변수 값 읽기)
        bool dialog = flowchart.GetBooleanVariable(dialogBoolName);

        // 대사 중이면 드롭 안되게
        if (dialog)
        {
            Debug.Log("대사가 진행 중이라 아이템을 사용할 수 없습니다.");
            return;
        }

        // 올바른 아이템인지 확인
        if (InventorySlot.draggedItem == requiredItem)
        {
            Debug.Log($"올바른 아이템({requiredItem.itemName})을 사용했습니다!");

            // 1. 인스펙터에서 연결된 이벤트 실행
            onUnlock.Invoke();

            // 2. 인벤토리에서 사용한 아이템 제거
            InventoryManager.instance.RemoveItem(requiredItem);

            // 3. 드롭존 비활성화 (한 번만 사용되도록)
            this.enabled = false;
        }
        else
        {
            Debug.Log("잘못된 아이템입니다.");
        }
    }
}
