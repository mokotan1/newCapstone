// AnimEndToFungus.cs
using UnityEngine;
using Fungus;

public class AnimEndToFungus : MonoBehaviour
{
    public Flowchart flowchart;          // Scene B에 있는 Flowchart 참조
    public string messageName = "AnimFinished";

    // 애니메이션 이벤트에서 호출
    public void OnAnimEnd()
    {
        if (flowchart != null)
            flowchart.SendFungusMessage(messageName);
    }
}