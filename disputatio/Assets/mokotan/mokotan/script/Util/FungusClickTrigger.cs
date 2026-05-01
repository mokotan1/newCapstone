using UnityEngine;
using Fungus;

public class FungusClickTrigger : MonoBehaviour
{
    [Tooltip("연결할 Flowchart")]
    public Flowchart targetFlowchart;

    [Tooltip("아이템 없이 그냥 클릭했을 때 실행할 블록 이름")]
    public string blockToExecute;

    /// <summary>
    /// OnMouseDown 경로는 Clickable2D/InteractionLock을 거치지 않아 대사·메뉴 중에도 연타될 수 있습니다.
    /// Say·Menu가 열려 있거나 전역 잠금 중이면 무시합니다.
    /// </summary>
    private static bool IsFungusConversationBlockingRepeatWorldClick()
    {
        MenuDialog md = MenuDialog.ActiveMenuDialog;
        if (md != null && md.gameObject.activeInHierarchy)
            return true;

        SayDialog say = SayDialog.ActiveSayDialog;
        if (say == null || !say.gameObject.activeInHierarchy)
            return false;

        Writer writer = say.GetComponentInChildren<Writer>(true);
        return writer != null && (writer.IsWriting || writer.IsWaitingForInput);
    }

    private void OnMouseDown()
    {
        if (targetFlowchart == null || string.IsNullOrEmpty(blockToExecute))
            return;

        if (InteractionLock.IsLocked)
            return;

        if (IsFungusConversationBlockingRepeatWorldClick())
            return;

        targetFlowchart.ExecuteBlock(blockToExecute);
    }
}