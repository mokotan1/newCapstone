using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTracker : MonoBehaviour
{
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
        SceneRouteState.RecordDepartedScene(current.name);
        GameLog.Log($"[SceneTracker] previous scene recorded → {current.name}");
    }
}
