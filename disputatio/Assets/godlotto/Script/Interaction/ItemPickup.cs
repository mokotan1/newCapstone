using UnityEngine;
using UnityEngine.EventSystems;
using Fungus;

public class ItemPickup : MonoBehaviour, IPointerClickHandler
{
    [Header("아이템 데이터")]
    public Item item;

    [Header("Inventory")]
    [SerializeField] private bool addToInventory = true;

    [Header("Fungus 연동 (선택사항)")]
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    [SerializeField] private Flowchart targetFlowchart;
    public string fungusVariableName;
    public string executeBlockName;

    [Header("연관 오브젝트 정리 (선택사항)")]
    [Tooltip(
        "픽업이 확정되면 이 오브젝트를 함께 SetActive(false)합니다. "
        + "예: FoodItemEffect, 겹쳐 배치된 픽업 스프라이트. "
        + "Fungus SetActive 커맨드에 의존하지 않고 C# pickup 완료가 정리를 소유하도록 합니다. "
        + "취소(PickUp이 호출되지 않음) 시에는 그대로 활성 상태를 유지합니다.")]
    [SerializeField] private GameObject[] objectsToDeactivateOnPickup;

    private bool hasPickedUp;

    private void Start()
    {
        SuppressIfAlreadyTaken();
    }

    /// <summary>
    /// 지도·씬 이동 후 씬이 다시 로드되면 프리팹이 복구되므로,
    /// 습득 비트마스크 또는 Fungus bool이 이미 켜져 있으면 오브젝트를 제거합니다.
    /// </summary>
    private void SuppressIfAlreadyTaken()
    {
        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc == null)
            return;

        if (item != null && ItemAcquisitionTracker.IsAcquired(fc, item.itemId))
        {
            GameLog.LogWarning(
                $"[ItemPickup] Destroy '{gameObject.name}': itemId {item.itemId} already in "
                + $"{ItemAcquisitionTracker.FungusVariableKey}.");
            Destroy(gameObject);
            return;
        }

        if (!string.IsNullOrEmpty(fungusVariableName) && fc.GetBooleanVariable(fungusVariableName))
        {
            GameLog.LogWarning(
                $"[ItemPickup] Destroy '{gameObject.name}': Fungus bool '{fungusVariableName}' is already true.");
            Destroy(gameObject);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        PickUp();
    }

    private void OnMouseDown()
    {
        PickUp();
    }

    [ContextMenu("PickUp (Manual Test)")]
    public void PickUpDirect()
    {
        PickUp();
    }

    private void PickUp()
    {
        if (hasPickedUp)
            return;

        hasPickedUp = true;

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
        {
            if (!string.IsNullOrEmpty(fungusVariableName))
            {
                fc.SetBooleanVariable(fungusVariableName, true);
                GameLog.Log($"[ItemPickup] Fungus 변수 '{fungusVariableName}' → True");
            }

            if (!string.IsNullOrEmpty(executeBlockName))
            {
                fc.ExecuteBlock(executeBlockName);
                GameLog.Log($"[ItemPickup] Fungus 블록 '{executeBlockName}' 실행");
            }
        }

        if (item != null && !addToInventory)
        {
            if (fc != null)
                ItemAcquisitionTracker.MarkAcquired(fc, item);

            BibleCommentaryPanelHintRuntime.RefreshAll();
            GameLog.Log($"[ItemPickup] {item.name} acquisition recorded without adding to inventory.");
        }
        else if (InventoryManager.instance != null && item != null)
        {
            InventoryManager.instance.AddItem(item);
            GameLog.Log($"[ItemPickup] {item.name} 아이템을 인벤토리에 추가했습니다.");
        }
        else
        {
            GameLog.LogWarning("[ItemPickup] InventoryManager.instance 또는 item이 null입니다!");
        }

        ClickInteractionCleanup.ResetAfterUiBoundary(fc);
        DeactivateLinkedObjects();

        // 확정된 성공 경로이므로 즉시 비활성화합니다. Destroy()는 실제 프레임 종료 시점에
        // 처리되어(에디터 모드에서는 즉시 처리되지 않음) 시각적으로 한 프레임 더 남을 수 있으므로,
        // SetActive(false)로 즉시 비가시·비활성 상태를 보장한 뒤 메모리 정리를 위해 Destroy합니다.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    /// <summary>
    /// 픽업이 확정된 시점(성공 경로 종료)에 연관 오브젝트를 비활성화합니다.
    /// Fungus 블록의 SetActive 명령이 이후에 중단되어도 이미 정리가 끝난 상태이므로
    /// 이펙트가 남지 않고, PickUp() 자체가 호출되지 않은 취소 경로에는 영향이 없습니다.
    /// </summary>
    private void DeactivateLinkedObjects()
    {
        if (objectsToDeactivateOnPickup == null)
            return;

        foreach (GameObject linked in objectsToDeactivateOnPickup)
        {
            if (linked != null)
                linked.SetActive(false);
        }
    }
}
