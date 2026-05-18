using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

public class BackNavigator : MonoBehaviour
{
    [Header("전역 Flowchart 설정")]
    [SerializeField] string globalFlowchartName = "Variablemanager";
    [SerializeField] string prevVarKey = "PrevScene";
    [SerializeField] string fallbackSceneName = ""; // 비상용 (없으면 생략 가능)

    private void Awake()
    {

    }

    public void GoBack()
    {
        Flowchart global = FlowchartLocator.FindByGameObjectName(globalFlowchartName);
        if (global == null)
        {
            GameLog.LogWarning($"전역 Flowchart '{globalFlowchartName}'를 찾지 못했습니다.");
            TryFallback();
            return;
        }

        string prev = global.GetStringVariable(prevVarKey);
        if (string.IsNullOrEmpty(prev))
        {
            GameLog.LogWarning($"전역 변수 '{prevVarKey}'가 비어 있습니다.");
            TryFallback();
            return;
        }

        GameLog.Log($"[BackNavigator] 이전 씬으로 이동 중 → {prev}");
        SceneManager.LoadScene(prev);

        Debug.Log("클릭됨");
    }

    private void TryFallback()
    {
        if (!string.IsNullOrEmpty(fallbackSceneName))
        {
            GameLog.Log($"[BackNavigator] PrevScene이 비어 있어서 '{fallbackSceneName}'로 이동합니다.");
            SceneManager.LoadScene(fallbackSceneName);
        }
        else
        {
            GameLog.LogWarning("[BackNavigator] 이전 씬 정보를 찾지 못했습니다.");
        }
    }
}
