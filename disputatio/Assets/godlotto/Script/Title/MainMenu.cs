using System;
using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    public Button[] menuButtons; // Start, Load, Setting, Exit
    [SerializeField] private MainMenuConfigPanel configPanel;

    private int currentButtonIndex = 0;
    private Vector3 lastMousePosition;
    private bool newGameStarted;
    private Func<string, bool> requestSceneTransition =
        sceneName => SceneTransitionService.LoadSceneSafely(sceneName);

    void Awake()
    {
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (configPanel == null)
        {
            MainMenuConfigPanel[] panels = Resources.FindObjectsOfTypeAll<MainMenuConfigPanel>();
            if (panels.Length > 0)
                configPanel = panels[0];
        }

        SetKeyboardMode();
        lastMousePosition = Input.mousePosition;
        SelectButton(currentButtonIndex);
    }

    void Update()
    {
        if (configPanel != null && configPanel.IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                configPanel.Close();
            return;
        }

        if (Input.mousePosition != lastMousePosition)
        {
            SetMouseMode();
        }
        lastMousePosition = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            SetKeyboardMode();
            HandleKeyboardInput();
        }
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentButtonIndex = (currentButtonIndex + 1) % menuButtons.Length;
            SelectButton(currentButtonIndex);
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentButtonIndex--;
            if (currentButtonIndex < 0) currentButtonIndex = menuButtons.Length - 1;
            SelectButton(currentButtonIndex);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            menuButtons[currentButtonIndex].onClick.Invoke();
        }
    }

    // --- 모드 설정 ---
    private void SetKeyboardMode()
    {
        // 커서는 숨기되, 잠그지 않습니다 (잠금은 다음 씬으로 carry-over 되므로 금지)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;  // ★ 변경: Locked → None
    }

    private void SetMouseMode()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void SelectButton(int index)
    {
        EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
    }

    // --- 공통: 씬 전환 전 커서 해제 ---
    private void UnlockCursorForSceneChange()
    {
        Cursor.visible = true;                     // 다음 씬이 원하면 알아서 숨기게
        Cursor.lockState = CursorLockMode.None;    // 잠금 carry-over 방지
    }

    // --- 버튼 핸들러 ---
    public void OnStartButton()
    {
        if (newGameStarted || SceneTransitionService.IsTransitionPending)
            return;

        newGameStarted = true;
        UnlockCursorForSceneChange();

        // 새 게임 시작 시 진행 데이터만 초기화하고 오디오/화면 설정은 유지합니다.
        PlayDataPrefsCleaner.ClearProgressPreserveAudioVideoSettings();

        InventorySlot.ClearDragState();
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ClearItemsForNewGame();

        GameLog.Log("게임 시작! (커서 잠금 해제 완료)");

        if (!requestSceneTransition(SceneNames.IntroScene))
            newGameStarted = false;
    }

    public void OnLoadButton()
    {
        UnlockCursorForSceneChange();
        CheckpointLoadCoordinator.LoadLatestOrFallback(SceneNames.MainScene);
    }

    public void OnSettingButton()
    {
        if (configPanel != null)
        {
            configPanel.Toggle();
            return;
        }

        GameLog.LogWarning("[MainMenu] ConfigPanel is not assigned.");
    }

    public void OnExitButton()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
