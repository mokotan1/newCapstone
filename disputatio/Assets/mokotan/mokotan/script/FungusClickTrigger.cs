using UnityEngine;
using Fungus;

public class FungusClickTrigger : MonoBehaviour
{
    [Tooltip("연결할 Flowchart")]
    public Flowchart targetFlowchart;

    [Tooltip("아이템 없이 그냥 클릭했을 때 실행할 블록 이름")]
    public string blockToExecute;

    // 이 오브젝트에 Collider가 있고, 마우스로 클릭하면 자동 실행됩니다.
    private void OnMouseDown()
    {
        // 1. 인벤토리 매니저가 있고, 선택된 아이템이 있는지 먼저 확인
        if (InventoryManager.instance != null && InventoryManager.instance.selectedItem != null)
        {
            // 아이템이 선택된 상태라면, 아이템 사용을 시도합니다.
            InventoryManager.instance.UseItemOn(this.gameObject);
        }
        else
        {
            // 아이템이 선택되지 않은 상태라면, 지정된 Fungus 블록을 실행합니다.
            if (targetFlowchart != null && !string.IsNullOrEmpty(blockToExecute))
            {
                targetFlowchart.ExecuteBlock(blockToExecute);
            }
        }
    }
}