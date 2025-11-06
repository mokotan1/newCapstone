using UnityEngine;
using UnityEngine.EventSystems;

public class RightClickDeselector : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // 만약 오른쪽 버튼으로 클릭했다면
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            // 인벤토리 매니저의 아이템 선택을 취소합니다.
            if (InventoryManager.instance != null)
            {
                InventoryManager.instance.DeselectItem();
            }
        }
    }
}