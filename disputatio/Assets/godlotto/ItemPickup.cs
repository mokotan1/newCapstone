using UnityEngine;
using UnityEngine.EventSystems; // 이 줄을 추가해야 합니다.

public class ItemPickup : MonoBehaviour, IPointerClickHandler
{
    public Item item;

    // IPointerClickHandler 인터페이스의 함수입니다.
    // UI 요소가 클릭되면 이 함수가 호출됩니다.
    public void OnPointerClick(PointerEventData eventData)
    {
        PickUp();
    }

    void PickUp()
    {
        InventoryManager.instance.AddItem(item);
        Destroy(gameObject);
    }
}