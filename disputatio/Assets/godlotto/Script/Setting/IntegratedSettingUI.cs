using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class IntegratedSettingUI : MonoBehaviour
{
    const string SettingsCanvasSortingLayerName = "Setting";
    const int SettingsCanvasSortingOrder = 50;

    public enum UIMode { StandaloneScene, PopupPanel }
    [Header("Mode Setting")]
    public UIMode uiMode = UIMode.PopupPanel; 

    [Header("UI Components")]
    public AudioMixer audioMixer;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Navigation")]
    public string mainMenuSceneName = SceneNames.MainMenu;
    public GameObject panelRoot; 
    
    [Header("Fungus Integration")]
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    public Flowchart targetFlowchart;
    public string fungusVariableName = FungusVariableKeys.IsClicked;
    public Fungus.DialogInput dialogInput;

    [Header("Keyboard Input")]
    public Selectable[] navigableElements;
    private int currentIndex = 0;
    private Vector3 lastMousePosition;
    private bool isReturningToMainMenu;
    private ResolutionAudioSettings resolutionAudio;

    void Start()
    {
        EnsureResolutionAudio();
        EnsureUiReferences();
        SyncUIWithManager();

        lastMousePosition = Input.mousePosition;
        UnlockCursor(); 
    }

    void OnEnable()
    {
        isReturningToMainMenu = false;
        EnsureUiReferences();
        SyncUIWithManager();
        UnlockCursor();
        if (uiMode == UIMode.PopupPanel && panelRoot != null && panelRoot.activeInHierarchy)
            EnsureSettingsCanvasSortsAboveSayDialog();
    }

    public void UnlockCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void SyncUIWithManager()
    {
        EnsureResolutionAudio();
        EnsureUiReferences();

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveAllListeners();
            bgmSlider.value = resolutionAudio.GetPersistedBgmLinear();
            bgmSlider.onValueChanged.AddListener(OnBGMChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.value = resolutionAudio.GetPersistedSfxLinear();
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveAllListeners();
            fullscreenToggle.isOn = resolutionAudio.GetPersistedFullscreen();
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }

        if (resolutionDropdown != null)
            resolutionAudio.InitializeResolutionDropdown(resolutionDropdown);

        if (bgmSlider != null && sfxSlider != null)
            resolutionAudio.ApplyAudioFromLinear(bgmSlider.value, sfxSlider.value);
    }

    private void EnsureUiReferences()
    {
        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;
        SettingDisplayControlsFactory.EnsureDisplayControls(searchRoot, ref resolutionDropdown, ref fullscreenToggle);
    }

    private void EnsureResolutionAudio()
    {
        if (resolutionAudio == null)
            resolutionAudio = new ResolutionAudioSettings(audioMixer);
    }

    public void OnBGMChanged(float value)
    {
        EnsureResolutionAudio();
        resolutionAudio.SetBgmVolume(value);
    }

    public void OnSFXChanged(float value)
    {
        EnsureResolutionAudio();
        resolutionAudio.SetSfxVolume(value);
    }

    public void OnFullscreenChanged(bool value)
    {
        EnsureResolutionAudio();
        resolutionAudio.SetFullscreen(value);
    }

    // ★★★ [추가됨] 드롭다운이 펼쳐져 있는지 확인하는 함수 ★★★
    // TMP Dropdown은 펼쳐질 때 'Dropdown List'라는 이름의 자식 오브젝트를 생성합니다.
    private bool IsDropdownExpanded()
    {
        if (resolutionDropdown == null) return false;
        return resolutionDropdown.transform.Find("Dropdown List") != null;
    }

    void Update()
    {
        if (IsMainMenuSceneActive())
        {
            if (uiMode == UIMode.PopupPanel && panelRoot != null && panelRoot.activeSelf)
                panelRoot.SetActive(false);
            return;
        }

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

        EnsureSettingsCanvasSortsAboveSayDialog();
        SettingPanelWorldInputBlocker.Begin(panelRoot);
        
        if (dialogInput != null) dialogInput.enabled = false;
        Flowchart fcOpen = FlowchartLocator.Resolve(targetFlowchart);
        if (fcOpen != null) fcOpen.SetBooleanVariable(fungusVariableName, true);

        Time.timeScale = 0f;
        UnlockCursor();
        SyncUIWithManager();
    }

    /// <summary>
    /// 설정 UI 캔버스를 SayDialog(정렬 레이어)보다 위에 그리도록 합니다.
    /// </summary>
    void EnsureSettingsCanvasSortsAboveSayDialog()
    {
        if (panelRoot == null)
            return;
        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = SettingsCanvasSortingLayerName;
        canvas.sortingOrder = SettingsCanvasSortingOrder;
    }

    public void ReturnToGame()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        SettingPanelWorldInputBlocker.End();
        
        Flowchart fcClose = FlowchartLocator.Resolve(targetFlowchart);
        if (fcClose != null) fcClose.SetBooleanVariable(fungusVariableName, false);
        if (dialogInput != null) dialogInput.enabled = true;
        
        Time.timeScale = 1f;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private bool IsMainMenuSceneActive()
    {
        return SceneManager.GetActiveScene().name == SceneNames.MainMenu;
    }

    public void BackToMainMenu()
    {
        if (isReturningToMainMenu)
            return;

        isReturningToMainMenu = true;
        Time.timeScale = 1f;
        if (panelRoot != null)
            panelRoot.SetActive(false);
        
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
        SettingPanelWorldInputBlocker.End();
        if (panelRoot != null)
            panelRoot.SetActive(false);

        Flowchart fcMenu = FlowchartLocator.Resolve(targetFlowchart);
        if (fcMenu != null) fcMenu.SetBooleanVariable(fungusVariableName, false);
        
        yield return null;

        SceneManager.LoadScene(mainMenuSceneName);
        Destroy(gameObject); 
    }

    private void OnDestroy()
    {
        SettingPanelWorldInputBlocker.End();
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
