using UnityEngine;
using UnityEngine.SceneManagement;

public class DestroyOnEndScene : MonoBehaviour
{
    [Tooltip("이 씬들에 진입하면 오브젝트를 파괴합니다.")]
    public string[] targetSceneNames = { "BetaEnd", "MainMenuScene" };

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (var target in targetSceneNames)
        {
            if (scene.name == target)
            {
                Debug.Log($"[{gameObject.name}] {target} 씬 진입 → 오브젝트 파괴됨");
                Destroy(gameObject);
                return;
            }
        }
    }
}
