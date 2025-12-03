using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class MainMenu : MonoBehaviour
{
    public Button[] menuButtons; // Start, Load, Setting, Exit

    private int currentButtonIndex = 0;
    private Vector3 lastMousePosition;

    void Awake() 
    {
        Time.timeScale = 1f; 
    }

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
        // ▼▼▼ [핵심 수정] 다음 씬으로 가기 전에 커서를 무조건 보이게 하고 잠금을 풉니다. ▼▼▼
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 기존 로직 (데이터 초기화 등)
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        Debug.Log("게임 시작! (커서 잠금 해제 완료)");

        // (참고) 만약 여기서 코드로 씬을 이동한다면:
        // SceneManager.LoadScene("GameScene");
        
        // (참고) 만약 버튼에 Fungus 블록이 연결되어 있다면:
        // 이 함수가 실행된 후 Fungus가 씬을 이동시키므로, 
        // 위에서 커서를 풀어주면 다음 씬에서도 풀린 채로 시작됩니다.
    }

    public void OnLoadButton()
    {
        Debug.Log("게임 불러오기... (기능 구현 필요)");
    }

    public void OnSettingButton()
    {

    }

    public void OnExitButton()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}
