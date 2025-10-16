using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    public Button[] menuButtons; // Start, Load, Setting, Exit
    public string gameSceneName = "GameScene";
    public string settingSceneName = "SettingScene";

    private int currentButtonIndex = 0;
    private Vector3 lastMousePosition;

    void Start()
    {
        SetKeyboardMode();
        lastMousePosition = Input.mousePosition;
        SelectButton(currentButtonIndex);
    }

    void Update()
    {
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
        UnlockCursorForSceneChange();              // ★ 추가
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnLoadButton()
    {
        Debug.Log("게임 불러오기... (기능 구현 필요)");
    }

    public void OnSettingButton()
    {
        UnlockCursorForSceneChange();              // 선택: 설정 씬에서도 마우스 사용 시
        SceneManager.LoadScene(settingSceneName);
    }

    public void OnExitButton()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
