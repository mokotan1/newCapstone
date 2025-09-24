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
        targetFlowchart.ExecuteBlock(blockToExecute);
    }
}