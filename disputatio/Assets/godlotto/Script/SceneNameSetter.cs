using UnityEngine;
using Fungus;
using UnityEngine.SceneManagement;

public class SceneNameSetter : MonoBehaviour
{
    [Header("연결할 전역 Flowchart")]
    public Flowchart globalFlowchart;

    [Header("씬 이름을 기록할 전역 문자열 변수명")]
    public string sceneVarName = "SceneName";

    [Header("세이브 포인트 키로 사용할 전역 문자열 변수명")]
    public string savePointKeyVarName = "SavePointKey";

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        UpdateSceneVariables(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneVariables(scene.name);
    }

    private void UpdateSceneVariables(string currentSceneName)
    {
        if (globalFlowchart == null)
        {
            globalFlowchart = FindObjectOfType<Flowchart>();
            if (globalFlowchart == null)
            {
                Debug.LogWarning("[SceneNameSetter] 전역 Flowchart를 찾지 못했습니다.");
                return;
            }
        }

        // SceneName 갱신
        if (!string.IsNullOrEmpty(sceneVarName))
        {
            globalFlowchart.SetStringVariable(sceneVarName, currentSceneName);
            Debug.Log($"[SceneNameSetter] SceneName → '{currentSceneName}'");
        }

        // SavePointKey 갱신 (원래대로 _Start 포함)
        if (!string.IsNullOrEmpty(savePointKeyVarName))
        {
            string saveKey = currentSceneName + "_Start";
            globalFlowchart.SetStringVariable(savePointKeyVarName, saveKey);
            Debug.Log($"[SceneNameSetter] SavePointKey → '{saveKey}'");
        }
    }
}
