using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class IntegratedSettingUI : MonoBehaviour
{
    public enum UIMode { StandaloneScene, PopupPanel }
    [Header("Mode Setting")]
    public UIMode uiMode = UIMode.PopupPanel; 

    [Header("UI Components")]
    public Slider bgmSlider;
    public Slider sfxSlider;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Navigation")]
    public string mainMenuSceneName = "MainMenuScene";
    public GameObject panelRoot; 
    
    [Header("Fungus Integration")]
    public Flowchart targetFlowchart;
    public string fungusVariableName = "isCalled";
    public Fungus.DialogInput dialogInput;

    [Header("Keyboard Input")]
    public Selectable[] navigableElements;
    private int currentIndex = 0;
    private Vector3 lastMousePosition;

    void Start()
    {
        if (GlobalSettingManager.Instance == null)
        {
            Debug.LogError("[UI] GlobalSettingManager가 없습니다!");
            return;
        }

        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        fullscreenToggle.onValueChanged.AddListener(GlobalSettingManager.Instance.SetFullscreen);
        
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(GlobalSettingManager.Instance.GetResolutionOptions());
        resolutionDropdown.onValueChanged.AddListener(GlobalSettingManager.Instance.SetResolutionIndex);

        SyncUIWithManager();

        lastMousePosition = Input.mousePosition;
        UnlockCursor(); 
    }

    void OnEnable()
    {
        if (GlobalSettingManager.Instance != null)
        {
            SyncUIWithManager();
        }
        UnlockCursor();
    }

    public void UnlockCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void SyncUIWithManager()
    {
        var mgr = GlobalSettingManager.Instance;

        bgmSlider.onValueChanged.RemoveAllListeners();
        sfxSlider.onValueChanged.RemoveAllListeners();
        fullscreenToggle.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.RemoveAllListeners();

        bgmSlider.value = mgr.bgmVolume;
        sfxSlider.value = mgr.sfxVolume;
        fullscreenToggle.isOn = mgr.isFullscreen;
        
        int savedIndex = mgr.currentResolutionIndex;
        if (savedIndex < 0 || savedIndex >= resolutionDropdown.options.Count)
            savedIndex = mgr.FindCurrentResolutionIndex();

        resolutionDropdown.value = savedIndex;
        resolutionDropdown.RefreshShownValue();

        bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        fullscreenToggle.onValueChanged.AddListener(GlobalSettingManager.Instance.SetFullscreen);
        resolutionDropdown.onValueChanged.AddListener(GlobalSettingManager.Instance.SetResolutionIndex);
    }

    public void OnBGMChanged(float value) => GlobalSettingManager.Instance.SetBGM(value);
    public void OnSFXChanged(float value) => GlobalSettingManager.Instance.SetSFX(value);

    // ★★★ [추가됨] 드롭다운이 펼쳐져 있는지 확인하는 함수 ★★★
    // TMP Dropdown은 펼쳐질 때 'Dropdown List'라는 이름의 자식 오브젝트를 생성합니다.
    private bool IsDropdownExpanded()
    {
        if (resolutionDropdown == null) return false;
        return resolutionDropdown.transform.Find("Dropdown List") != null;
    }

    void Update()
    {
        if (Vector3.Distance(Input.mousePosition, lastMousePosition) > 1f) 
        {
            UnlockCursor();
            EventSystem.current.SetSelectedGameObject(null);
        }
        lastMousePosition = Input.mousePosition;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // ★ 드롭다운이 열려있으면 ESC 누를 때 드롭다운만 닫히게 (Unity 기본 동작) 하고,
            // 패널은 닫지 않도록 return 합니다.
            if (IsDropdownExpanded()) return;

            if (uiMode == UIMode.StandaloneScene)
            {
                BackToMainMenu();
            }
            else if (uiMode == UIMode.PopupPanel && panelRoot != null)
            {
                if (panelRoot.activeSelf) ReturnToGame();
                else OpenSettingPanel();
            }
        }

        if (uiMode == UIMode.PopupPanel && panelRoot != null && !panelRoot.activeSelf) return;

        HandleKeyboardInput();
    }

    public void OpenSettingPanel()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        
        if (dialogInput != null) dialogInput.enabled = false;
        if (targetFlowchart != null) targetFlowchart.SetBooleanVariable(fungusVariableName, true);

        Time.timeScale = 0f; 
        UnlockCursor();
        SyncUIWithManager();
    }

    public void ReturnToGame()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        
        if (targetFlowchart != null) targetFlowchart.SetBooleanVariable(fungusVariableName, false);
        if (dialogInput != null) dialogInput.enabled = true;
        
        Time.timeScale = 1f;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        
        if (uiMode == UIMode.StandaloneScene)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            StartCoroutine(GoToMainMenuProcess());
        }
    }

    private IEnumerator GoToMainMenuProcess()
    {
        Time.timeScale = 1f;
        if (targetFlowchart != null) targetFlowchart.SetBooleanVariable(fungusVariableName, false);
        
        yield return null;

        SceneManager.LoadScene(mainMenuSceneName);
        Destroy(gameObject); 
    }

    private void CleanupDontDestroyObjects()
    {
        var temp = new GameObject("TempSceneProbe");
        DontDestroyOnLoad(temp);
        var ddScene = temp.scene;
        Destroy(temp);

        var roots = new List<GameObject>();
        ddScene.GetRootGameObjects(roots);

        foreach (var obj in roots)
        {
            if (obj == gameObject) continue;
            if (obj.GetComponent<GlobalSettingManager>() != null) continue;
            Destroy(obj);
        }
    }

    private void HandleKeyboardInput()
    {
        // ★★★ [핵심 수정] 드롭다운이 펼쳐져 있으면 내비게이션 로직 중단 ★★★
        // 이렇게 하면 Unity의 기본 드롭다운 네비게이션(화살표로 내부 목록 이동)이 작동합니다.
        if (IsDropdownExpanded()) return;

        if (EventSystem.current.currentSelectedGameObject == null && 
           (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)))
        {
            SelectUIElement(currentIndex);
        }
        
        if (EventSystem.current.currentSelectedGameObject == null) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            HandleNavigation();
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            HandleEnterPress();
        }
    }

    private void HandleNavigation()
    {
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            currentIndex++;
            if (currentIndex >= navigableElements.Length) currentIndex = 0;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            currentIndex--;
            if (currentIndex < 0) currentIndex = navigableElements.Length - 1;
        }
        SelectUIElement(currentIndex);
    }

    private void HandleEnterPress()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null) return;

        Button button = selectedObj.GetComponent<Button>();
        if (button != null) button.onClick.Invoke();
    }

    private void SelectUIElement(int index)
    {
        if (navigableElements.Length > 0 && index >= 0 && index < navigableElements.Length)
        {
            EventSystem.current.SetSelectedGameObject(navigableElements[index].gameObject);
            currentIndex = index;
        }
    }
}