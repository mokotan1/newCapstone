using UnityEngine;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour
{
    public Image icon;
    Item item;

    public void AddItem(Item newItem)
    {
        item = newItem;
        icon.sprite = item.icon;
        icon.enabled = true;

        // 버튼 컴포넌트가 있다면 상호작용 가능하게 설정
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
        }
    }

    public void ClearSlot()
    {
        item = null;
        icon.sprite = null;
        icon.enabled = false;

        // 버튼 컴포넌트가 있다면 상호작용 불가능하게 설정
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = false;
        }
    }

    public void OnSlotClicked()
    {
        if (item != null)
        {
            // InventoryManager의 instance를 찾아 SelectItem 함수를 호출
            InventoryManager.instance.SelectItem(item);
        }
    }
}