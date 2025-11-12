using UnityEngine;
using UnityEngine.SceneManagement;

public class DestroyOnEndScene : MonoBehaviour
{
    [Tooltip("이 씬 이름일 때 오브젝트를 파괴합니다.")]
    public string targetSceneName = "BetaEnd";

    void OnEnable()
    {
        // 씬이 바뀔 때마다 확인하도록 이벤트 등록
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        // 씬 언로드 시 이벤트 해제
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 이름이 BetaEnd일 경우 자신을 파괴
        if (scene.name == targetSceneName)
        {
            Debug.Log($"[{gameObject.name}] BetaEnd 씬 진입 감지 → 오브젝트 파괴됨");
            Destroy(gameObject);
        }
    }
}
