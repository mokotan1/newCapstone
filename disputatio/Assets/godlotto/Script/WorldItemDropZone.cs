using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using Fungus; // Fungus를 사용하기 위해 이 줄을 추가합니다.

public class WorldItemDropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("Fungus 연동")]
    public Flowchart targetFlowchart; // 이벤트를 보낼 Flowchart
    public string fungusBooleanVariableName = "UsedKey"; // True로 바꿀 변수 이름

    [Header("잠금 해제 이벤트 (선택사항)")]
    public UnityEvent onUnlock; // 문 열림 애니메이션 등 추가 효과를 위해 사용

    public void OnDrop(PointerEventData eventData)
    {
        // 드롭된 아이템이 올바른 열쇠인지 확인합니다.
        if (InventorySlot.draggedItem == requiredItem)
        {
            Debug.Log("올바른 아이템(" + requiredItem.itemName + ")을 사용했습니다!");

            // 1. Fungus 변수를 True로 설정합니다.
            if (targetFlowchart != null && !string.IsNullOrEmpty(fungusBooleanVariableName))
            {
                targetFlowchart.SetBooleanVariable(fungusBooleanVariableName, true);
                Debug.Log($"Fungus 변수 '{fungusBooleanVariableName}'을(를) True로 설정했습니다.");
            }
            else
            {
                Debug.LogWarning("Fungus 연동 설정이 비어있습니다.");
            }

            // 2. 추가적인 성공 이벤트를 실행합니다 (예: 문 열림 애니메이션).
            if (onUnlock != null)
            {
                onUnlock.Invoke();
            }

            // 3. 인벤토리에서 사용한 열쇠를 제거합니다.
            InventoryManager.instance.RemoveItem(requiredItem);

            // 4. 이 드롭 존은 더 이상 사용할 필요가 없으므로 비활성화합니다.
            this.enabled = false;
        }
    }
}