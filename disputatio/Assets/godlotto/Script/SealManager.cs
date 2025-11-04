using UnityEngine;
using UnityEngine.Events;
using Fungus;

/// <summary>
/// 씬 내부에서 모든 Seal 변수를 감시하고,
/// 7개 모두 true가 되었을 때 onAllSealsComplete 이벤트를 단 한 번만 실행.
/// 씬을 재입장해도 allSealsComplete가 이미 true라면 다시 실행되지 않음.
/// </summary>
public class SealManager : MonoBehaviour
{
    [Header("Fungus 연동")]
    public Flowchart flowchart;

    [Tooltip("감시할 Seal 변수 이름들 (Fungus Global Booleans)")]
    public string[] sealVariableNames =
    {
        "seal1", "seal2", "seal3", "seal4", "seal5", "seal6", "seal7"
    };

    [Tooltip("모든 Seal 완성 여부(글로벌 Bool)")]
    public string allSealsVar = "allSealsComplete";

    [Header("이벤트")]
    [Tooltip("모두 true가 되었을 때 실행할 이벤트 (Flowchart.ExecuteBlock 연결 가능)")]
    public UnityEvent onAllSealsComplete;

    private bool triggered = false;

    void Start()
    {
        if (flowchart == null)
        {
            Debug.LogWarning("SealManager: Flowchart가 연결되지 않았습니다.");
            return;
        }

        // ✅ 씬 시작 시 이미 allSealsComplete == true라면 재트리거 방지
        if (flowchart.GetBooleanVariable(allSealsVar))
        {
            triggered = true;
            Debug.Log("SealManager: allSealsComplete가 이미 true — 재트리거 방지.");
        }
    }

    void Update()
    {
        if (triggered || flowchart == null) return;

        if (AllSealsAreTrue())
        {
            triggered = true;

            // ✅ 글로벌 완료 플래그 세팅(중복세팅해도 무해)
            flowchart.SetBooleanVariable(allSealsVar, true);

            // ✅ 처음 한 번만 실행
            onAllSealsComplete?.Invoke();

            Debug.Log("✅ All seals are TRUE → onAllSealsComplete fired.");
        }
    }

    private bool AllSealsAreTrue()
    {
        foreach (var varName in sealVariableNames)
        {
            if (!flowchart.GetBooleanVariable(varName))
                return false;
        }
        return true;
    }
}
