using UnityEngine;
using Fungus;
using UnityEngine.SceneManagement;

public class SceneNameSetter : MonoBehaviour
{
    [Header("연결할 전역 Flowchart")]
    public Flowchart globalFlowchart;

    [Header("씬 이름을 기록할 전역 문자열 변수명")]
    public string sceneVarName = "CurrentScene";

    void Start()
    {
        // Flowchart가 안 연결되어 있으면 자동 탐색 시도
        if (globalFlowchart == null)
        {
            globalFlowchart = FindObjectOfType<Flowchart>();
            if (globalFlowchart == null)
            {
                Debug.LogWarning("[SceneNameSetter] 전역 Flowchart를 찾지 못했습니다.");
                return;
            }
        }

        // 현재 씬 이름 가져오기
        string currentSceneName = SceneManager.GetActiveScene().name;

        // Fungus 전역 변수에 기록
        if (!string.IsNullOrEmpty(sceneVarName))
        {
            globalFlowchart.SetStringVariable(sceneVarName, currentSceneName);
            Debug.Log($"[SceneNameSetter] 현재 씬 이름 '{currentSceneName}'을(를) '{sceneVarName}' 변수에 저장했습니다.");
        }
        else
        {
            Debug.LogWarning("[SceneNameSetter] 변수 이름이 비어 있습니다.");
        }
    }
}
