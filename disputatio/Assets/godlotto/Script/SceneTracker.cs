using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

public class SceneTracker : MonoBehaviour
{
    [SerializeField] string globalFlowchartName = "Variablemanager";
    [SerializeField] string prevVarKey = "PrevScene";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    // 현재 씬을 떠나기 직전에 이름 저장
    private void OnSceneUnloaded(Scene current)
    {
        Flowchart global = GameObject.Find(globalFlowchartName)?.GetComponent<Flowchart>();
        if (global != null)
        {
            global.SetStringVariable(prevVarKey, current.name);
            Debug.Log($"[SceneTracker] PrevScene 저장됨 → {current.name}");
        }
    }
}
