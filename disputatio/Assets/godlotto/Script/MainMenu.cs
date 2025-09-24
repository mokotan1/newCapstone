using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    // 인스펙터에서 연결할 UI 요소들
    public Button[] menuButtons; // 메뉴 버튼 배열 (Start, Load, Setting, Exit 순서)
    public string gameSceneName = "GameScene"; // 'Start' 클릭 시 이동할 게임 씬 이름
    public string settingSceneName = "SettingScene"; // 'Setting' 클릭 시 이동할 설정 씬 이름

    private int currentButtonIndex = 0; // 현재 선택된 버튼의 인덱스
    private Vector3 lastMousePosition; // 마우스 움직임 감지를 위한 마지막 위치

    void Start()
    {
        // 키보드 우선 모드로 시작 (커서 숨김)
        SetKeyboardMode();
        lastMousePosition = Input.mousePosition;
        
        // 첫 번째 버튼을 기본으로 선택
        SelectButton(currentButtonIndex);
    }

    void Update()
    {
        // 마우스가 움직였는지 확인
        if (Input.mousePosition != lastMousePosition)
        {
            SetMouseMode();
        }
        lastMousePosition = Input.mousePosition;

        // 키보드 입력이 감지되면 키보드 모드로 전환
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
            currentButtonIndex++;
            if (currentButtonIndex >= menuButtons.Length) currentButtonIndex = 0;
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

    // --- 모드 설정 함수 ---
    private void SetKeyboardMode()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void SetMouseMode()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        // 마우스 모드에서는 키보드 하이라이트를 제거하여 마우스 오버와 겹치지 않게 함
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void SelectButton(int index)
    {
        EventSystem.current.SetSelectedGameObject(menuButtons[index].gameObject);
    }

    // --- 버튼 클릭 시 호출될 함수들 ---
    public void OnStartButton()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnLoadButton()
    {
        Debug.Log("게임 불러오기... (기능 구현 필요)");
    }

    public void OnSettingButton()
    {
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