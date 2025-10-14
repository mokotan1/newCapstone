using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem; // 인스펙터에서 '필터 카드' 아이템 에셋을 연결

    [Header("상호작용 오브젝트")]
    public GameObject filterCardObject;
    public GameObject rotateRightButtonObject;
    public GameObject rotateLeftButtonObject;

    // ... Start() 함수는 기존과 동일 ...

    public void OnDrop(PointerEventData eventData)
    {
        // 현재 드래그 중인 아이템(InventorySlot.draggedItem)이
        // 이 드롭존이 필요로 하는 아이템(requiredItem)과 일치하는지 확인
        if (InventorySlot.draggedItem == requiredItem)
        {
            Debug.Log("올바른 아이템(" + requiredItem.itemName + ")이 사용되었습니다!");

            // 실제 필터 카드를 활성화하고 제자리에 놓습니다.
            filterCardObject.SetActive(true);
            filterCardObject.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            filterCardObject.transform.localScale = Vector3.one;

            // 회전 버튼들을 활성화합니다.
            if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(true);
            if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(true);

            // 인벤토리에서 사용한 아이템을 제거합니다.
            InventoryManager.instance.RemoveItem(requiredItem);
        }
    }
}