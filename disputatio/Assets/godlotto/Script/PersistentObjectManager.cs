using UnityEngine;
using UnityEngine.SceneManagement; // Scene 관리를 위해 추가

public class PersistentObjectManager : MonoBehaviour
{
    public static PersistentObjectManager instance;

    // ✨ Inspector에서 SettingPanel을 직접 연결할 수 있도록 변수 추가
    public GameObject settingPanel;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ✨ 스크립트가 활성화될 때 씬 로드 이벤트 구독
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // ✨ 스크립트가 비활성화될 때 구독 해제
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ✨ 씬 로드가 완료될 때마다 호출되는 함수
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 만약 로드된 씬이 "MainMenuScene"이라면
        if (scene.name == "MainMenuScene")
        {
            // SettingPanel이 연결되어 있다면, 확실하게 비활성화
            if (settingPanel != null)
            {
                settingPanel.SetActive(false);
            }
        }
    }
}