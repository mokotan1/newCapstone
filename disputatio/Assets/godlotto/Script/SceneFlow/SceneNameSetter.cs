using UnityEngine;
using Fungus;
using UnityEngine.SceneManagement;

public class SceneNameSetter : MonoBehaviour
{
    [Header("연결할 전역 Flowchart")]
    public Flowchart globalFlowchart;

    [Header("씬 이름을 기록할 전역 문자열 변수명")]
    public string sceneVarName = "SceneName";

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
        Flowchart fc = FlowchartLocator.Find();
        if (fc == null)
        {
            GameLog.LogWarning("[SceneNameSetter] Variablemanager Flowchart를 찾지 못했습니다.");
            return;
        }

        globalFlowchart = fc;

        if (!string.IsNullOrEmpty(sceneVarName))
        {
            fc.SetStringVariable(sceneVarName, currentSceneName);
            GameLog.Log($"[SceneNameSetter] SceneName → '{currentSceneName}'");
        }
    }
}
