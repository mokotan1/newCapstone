using Fungus;
using UnityEngine;

/// <summary>
/// 씬 로드 시 글로벌 Flowchart의 bool이 true이면 이 GameObject를 비활성화합니다.
/// 필드 지도(FeildMap)처럼 Fungus로만 숨긴 오브젝트가 씬 재진입 시 다시 보이는 문제를 막습니다.
/// </summary>
public class HideIfFungusBoolTrue : MonoBehaviour
{
    [SerializeField] private string fungusBoolKey = "MapClicked";
    [Tooltip("비우면 FlowchartLocator로 Variablemanager Flowchart를 사용합니다.")]
    [SerializeField] private Flowchart flowchartOverride;

    private void Start()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchartOverride);
        if (fc == null)
            return;

        if (fc.GetBooleanVariable(fungusBoolKey))
            gameObject.SetActive(false);
    }
}
