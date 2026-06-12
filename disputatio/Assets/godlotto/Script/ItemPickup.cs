using UnityEngine;
using UnityEngine.EventSystems;
using Fungus;

public class ItemPickup : MonoBehaviour, IPointerClickHandler
{
    [Header("아이템 데이터")]
    public Item item;

    [Header("Fungus 연동 (선택사항)")]
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    [SerializeField] private Flowchart targetFlowchart;
    public string fungusVariableName;
    public string executeBlockName;

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
        if (IsAlreadyTaken(fc))
            Destroy(gameObject);
    }

    private bool IsAlreadyTaken(Flowchart fc)
    {
        if (fc == null)
            return false;

        if (item != null && ItemAcquisitionTracker.IsAcquired(fc, item.itemId))
            return true;

        if (!string.IsNullOrEmpty(fungusVariableName) && fc.GetBooleanVariable(fungusVariableName))
            return true;

        if (item != null && InventoryManager.instance != null && InventoryManager.instance.HasItem(item))
            return true;

        return false;
    }

    public void OnPointerClick(PointerEventData eventData)
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
        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (IsAlreadyTaken(fc))
        {
            Destroy(gameObject);
            return;
        }

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

        if (InventoryManager.instance != null && item != null)
        {
            InventoryManager.instance.AddItem(item);
            GameLog.Log($"[ItemPickup] {item.name} 아이템을 인벤토리에 추가했습니다.");
        }
        else
        {
            GameLog.LogWarning("[ItemPickup] InventoryManager.instance 또는 item이 null입니다!");
        }

        ClickInteractionCleanup.ResetAfterUiBoundary(fc);
        Destroy(gameObject);
    }
}
