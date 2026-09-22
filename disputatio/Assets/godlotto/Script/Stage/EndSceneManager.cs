using System;
using System.Collections.Generic;
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
    public string mainMenuSceneName = SceneNames.MainMenu;

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

        // BetaEnd -> MainMenu는 InGameSettingsPanel의 "메인메뉴로" 버튼과 동일하게
        // Fungus 전역 변수·퀘스트 트래커 DDOL 루트를 정리해야 한다. 그렇지 않으면
        // 다음 New Game이 이전 회차의 Fungus/퀘스트 상태를 그대로 이어받는다.
        CleanupDontDestroyGameplayRoots();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Runtime entry point: discovers every current DontDestroyOnLoad root and
    /// wipes the ones not covered by <see cref="DontDestroyGameplayCleanup.ShouldPreserveRoot"/>
    /// (GlobalSettingManager and this object are preserved; Fungus globals and
    /// quest tracker systems are not).
    /// </summary>
    public void CleanupDontDestroyGameplayRoots()
    {
        CleanupDontDestroyGameplayRoots(DontDestroyGameplayCleanup.FindDontDestroyOnLoadRoots());
    }

    /// <summary>
    /// EditMode-testable overload: applies the shared cleanup policy to an
    /// explicit root list with an injectable destroy callback instead of the
    /// Play-Mode-only DontDestroyOnLoad discovery.
    /// </summary>
    public void CleanupDontDestroyGameplayRoots(IList<GameObject> roots, Action<GameObject> destroyRoot = null)
    {
        DontDestroyGameplayCleanup.DestroyUnpreservedRoots(roots, gameObject, destroyRoot);
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
