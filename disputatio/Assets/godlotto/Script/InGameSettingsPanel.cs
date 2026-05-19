using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Fungus;

public class InGameSettingsPanel : SingletonMonoBehaviour<InGameSettingsPanel>
{
    [System.Obsolete("Use Instance instead.")]
    public static InGameSettingsPanel instance => Instance;

    protected override bool PersistAcrossScenes => true;

    [Header("Fungus 연동")]
    [SerializeField] private Flowchart targetFlowchart;
    [SerializeField] private string fungusVariableName = FungusVariableKeys.IsCalled;
    [SerializeField] private Fungus.DialogInput dialogInput;

    [Header("UI Components")]
    [SerializeField] private GameObject settingPanel;
    public GameObject SettingPanel => settingPanel;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Keyboard Navigation")]
    [SerializeField] private Selectable[] navigableElements;
    private int currentIndex = 0;

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = SceneNames.MainMenu;

    const string SettingsCanvasSortingLayerName = "Setting";
    const int SettingsCanvasSortingOrder = 50;

    private ResolutionAudioSettings _resolutionAudio;
    private bool isPanelOpen = false;

    private float playTime = 0f;
    private bool isCounting = true;

    protected override void OnSingletonAwake()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);

        isPanelOpen = false;
    }

    void Start()
    {
        _resolutionAudio = new ResolutionAudioSettings(audioMixer);
        LoadSettings();
        AssignListeners();
        _resolutionAudio.InitializeResolutionDropdown(resolutionDropdown);
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.MainMenu)
        {
            if (isPanelOpen)
                CloseSettingPanel();
            return;
        }

        if (isCounting)
            playTime += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleSettingPanel();

        if (!isPanelOpen) return;
        HandleKeyboardInput();
    }

    public float GetPlayTime() => playTime;

    public void StopCounting()
    {
        isCounting = false;
    }

    public void ResetPlayTime()
    {
        playTime = 0f;
    }

    private void LoadSettings()
    {
        bgmSlider.value = _resolutionAudio.GetPersistedBgmLinear();
        sfxSlider.value = _resolutionAudio.GetPersistedSfxLinear();
        fullscreenToggle.isOn = _resolutionAudio.GetPersistedFullscreen();
        _resolutionAudio.ApplyAudioFromLinear(bgmSlider.value, sfxSlider.value);
    }

    private void AssignListeners()
    {
        bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
    }

    public void ToggleSettingPanel()
    {
        isPanelOpen = !isPanelOpen;
        settingPanel.SetActive(isPanelOpen);

        if (dialogInput != null)
            dialogInput.enabled = !isPanelOpen;

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
            fc.SetBooleanVariable(fungusVariableName, isPanelOpen);

        if (isPanelOpen)
        {
            EnsureSettingsCanvasSortsAboveSayDialog();
            SettingPanelWorldInputBlocker.Begin(settingPanel);
            Time.timeScale = 0f;
        }
        else
        {
            SettingPanelWorldInputBlocker.End();
            Time.timeScale = 1f;
        }
    }

    void EnsureSettingsCanvasSortsAboveSayDialog()
    {
        if (settingPanel == null)
            return;
        Canvas canvas = settingPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = settingPanel.GetComponent<Canvas>();
        if (canvas == null)
            return;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = SettingsCanvasSortingLayerName;
        canvas.sortingOrder = SettingsCanvasSortingOrder;
    }

    public void OpenSettingPanel()
    {
        if (!isPanelOpen) ToggleSettingPanel();
    }

    public void CloseSettingPanel()
    {
        if (isPanelOpen) ToggleSettingPanel();
    }

    private void HandleKeyboardInput()
    {
        bool isKeyboardInput = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                               Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                               Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        if (isKeyboardInput && EventSystem.current.currentSelectedGameObject == null)
            SelectUIElement(currentIndex);

        if (EventSystem.current.currentSelectedGameObject == null) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            HandleNavigation();
        else
            HandleEnterPress();
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
        if (button != null)
            button.onClick.Invoke();
    }

    private void SelectUIElement(int index)
    {
        if (navigableElements.Length > 0 && index >= 0 && index < navigableElements.Length)
        {
            EventSystem.current.SetSelectedGameObject(navigableElements[index].gameObject);
            currentIndex = index;
        }
    }

    public void SetBgmVolume(float volume)
    {
        _resolutionAudio.SetBgmVolume(volume);
    }

    public void SetSfxVolume(float volume)
    {
        _resolutionAudio.SetSfxVolume(volume);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        _resolutionAudio.SetFullscreen(isFullscreen);
    }

    public void BackToMainMenu()
    {
        GameLog.Log("메인메뉴 이동 버튼 클릭됨");
        StartCoroutine(GoToMainMenu());
    }

    private IEnumerator GoToMainMenu()
    {
        Time.timeScale = 1f;
        CloseSettingPanel();

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
            fc.SetBooleanVariable(fungusVariableName, false);

        CleanupDontDestroyObjects();
        GameLog.Log("모든 DontDestroyOnLoad 오브젝트 삭제 완료");
        yield return null;

        GameLog.Log($"씬 로드 시도: {mainMenuSceneName}");
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
            if (ShouldPreserveDontDestroyRoot(obj, gameObject))
                continue;

            Destroy(obj);
        }
    }

    public static bool ShouldPreserveDontDestroyRoot(GameObject root, GameObject currentSettingsObject)
    {
        if (root == null)
            return false;

        if (root == currentSettingsObject)
            return true;

        if (root.GetComponent<GlobalSettingManager>() != null)
            return true;

        if (root.GetComponent<GlobalVariables>() != null)
            return true;

        return root.name == "Variablemanager";
    }

    public void ReturnToGame()
    {
        CloseSettingPanel();
        GameLog.Log("게임 복귀");
    }

    protected override void OnDestroy()
    {
        SettingPanelWorldInputBlocker.End();
        base.OnDestroy();
    }
}
