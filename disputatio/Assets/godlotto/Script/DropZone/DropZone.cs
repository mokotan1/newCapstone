using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    [Header("필요한 아이템")]
    public Item requiredItem;

    [Header("상호작용 오브젝트")]
    public GameObject filterCardObject;
    public GameObject rotateRightButtonObject;
    public GameObject rotateLeftButtonObject;

    // ★★★ 이 아래 두 줄이 추가되었습니다! ★★★
    [Header("사용 횟수 설정")]
    public int maxUses = 2; // 최대 사용 횟수 (인스펙터에서 2로 설정)
    private int currentUses = 0; // 현재 사용 횟수를 저장할 변수

    void Start()
    {
        // 시작 시 버튼들을 화면에서 숨깁니다.
        if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(false);
        if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 드롭된 아이템이 올바른지 확인
        if (InventorySlot.draggedItem == requiredItem)
        {
            if (currentUses < maxUses)
            {
                // 사용 횟수를 1 증가시킵니다.
                currentUses++;
                Debug.Log(requiredItem.itemName + " 아이템을 사용했습니다. (" + currentUses + "/" + maxUses + ")");

                // 기존의 퍼즐 활성화 로직은 그대로 실행합니다.
                filterCardObject.SetActive(true);
                filterCardObject.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                filterCardObject.transform.localScale = Vector3.one;

                if (rotateRightButtonObject != null) rotateRightButtonObject.SetActive(true);
                if (rotateLeftButtonObject != null) rotateLeftButtonObject.SetActive(true);

                // ★★★ 마지막 사용일 때만 아이템을 제거하도록 조건을 추가합니다 ★★★
                if (currentUses >= maxUses)
                {
                    Debug.Log("마지막 사용! 인벤토리에서 아이템을 제거합니다.");
                    InventoryManager.instance.RemoveItem(requiredItem);
                }
            }
            else
            {
                Debug.Log(requiredItem.itemName + " 아이템은 더 이상 사용할 수 없습니다.");
            }
        }
    }
}