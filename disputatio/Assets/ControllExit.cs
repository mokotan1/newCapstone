using UnityEngine;
using Fungus;

public class ControllExit : MonoBehaviour
{
    [SerializeField] public GameObject penel; // 오타(penel)는 기존 연결 유지를 위해 그대로 두었습니다.
    public Flowchart flowchart;

    // 패널의 원래 집(지도 프리펩 내부)을 기억할 변수
    private Transform originalParent;

    void Awake()
    {
        // 시작할 때 패널의 원래 부모(프리펩 내 위치)를 저장합니다.
        if (penel != null)
        {
            originalParent = penel.transform.parent;
        }
    }

    public void whenClicked()
    {


        if (penel == null) return;

        // 1. 패널을 끕니다.
        penel.SetActive(false);

        // 2. ★ 핵심: 패널을 다시 원래 부모(지도 프리펩)의 자식으로 되돌립니다.
        // 이렇게 해야 프리펩이 파괴되거나 이동할 때 패널이 유실되지 않습니다.
        if (originalParent != null)
        {
            penel.transform.SetParent(originalParent, false);
        }

        // 3. 패널 닫기 경계: 연타 시 toggle이 isClicked를 다시 true로 만들지 않도록 명시 reset
        Flowchart targetFlowchart = flowchart != null ? flowchart : FlowchartLocator.Find();
        if (targetFlowchart != null)
        {
            ClickInteractionCleanup.ResetAfterUiBoundary(targetFlowchart, resetWindowClicked: false);
            DeferredClickCleanup.Run(targetFlowchart, resetWindowClicked: false);
        }
    }
}
