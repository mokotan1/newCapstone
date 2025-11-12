using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class EndSceneManager : MonoBehaviour
{
    [Header("UI Components")]
    public Button mainMenuButton;
    public Button exitButton;

    [Header("Optional: 배경음")]
    public AudioSource endBgm;

    [Header("씬 이름 설정")]
    public string mainMenuSceneName = "MainMenuScene";

    public TextMeshProUGUI playTimeText;

    void Start()
    {
        // BGM 재생
        if (endBgm != null)
        {
            endBgm.loop = false;
            endBgm.Play();
        }

        // 커서 활성화 (FPS 모드 등에서도 보이게)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 버튼 리스너 등록
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuButton);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButton);
        
        if (InGameSettingsPanel.instance != null)
    {
        InGameSettingsPanel.instance.StopCounting(); // ✅ 시간 카운트 중단
        float totalSeconds = InGameSettingsPanel.instance.GetPlayTime();
        int minutes = Mathf.FloorToInt(totalSeconds / 60);
        int seconds = Mathf.FloorToInt(totalSeconds % 60);
        playTimeText.text = $"플레이 시간 : {minutes}분 {seconds}초";
    }
    }

    // 메인 메뉴로 이동
    public void OnMainMenuButton()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // 게임 종료
    public void OnExitButton()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
