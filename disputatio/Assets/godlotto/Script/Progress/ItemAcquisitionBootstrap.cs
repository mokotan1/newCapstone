using Fungus;
using UnityEngine;

/// <summary>
/// 씬 시작 시 아이템 표시명 캐시를 채우고, 레거시 bool 습득 플래그를 비트마스크로 이전합니다.
/// Variablemanager가 있는 씬에 하나만 부착하세요.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ItemAcquisitionBootstrap : MonoBehaviour
{
    [Tooltip("비워 두면 FlowchartLocator로 자동 탐색합니다.")]
    [SerializeField] private Flowchart flowchart;

    void Start()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchart);
        if (fc == null)
            return;

        ItemAcquisitionTracker.WarmupDisplayNameCache();
        ItemAcquisitionTracker.MigrateLegacyBools(fc);
    }
}
