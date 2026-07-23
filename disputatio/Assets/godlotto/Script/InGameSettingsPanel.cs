using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Fungus;
using Godlotto.ModalInput;

public class InGameSettingsPanel : SingletonMonoBehaviour<InGameSettingsPanel>
{
    [System.Obsolete("Use Instance instead.")]
    public static InGameSettingsPanel instance => Instance;

    protected override bool PersistAcrossScenes => true;

    [Header("Fungus 연동")]
    [SerializeField] private Flowchart targetFlowchart;
    [SerializeField] private string fungusVariableName = FungusVariableKeys.IsClicked;
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
    private Image settingsRaycastBlocker;

    public bool IsOpen => isPanelOpen;

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
        EnsureUiReferences();
        LoadSettings();
        AssignListeners();
        if (resolutionDropdown != null)
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

        if (Input.GetKeyDown(KeyCode.Escape) && !ShouldIgnoreEscapeToggle())
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
        EnsureUiReferences();

        if (bgmSlider != null)
            bgmSlider.value = _resolutionAudio.GetPersistedBgmLinear();
        if (sfxSlider != null)
            sfxSlider.value = _resolutionAudio.GetPersistedSfxLinear();
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = _resolutionAudio.GetPersistedFullscreen();

        _resolutionAudio.ApplyAudioFromLinear(
            bgmSlider != null ? bgmSlider.value : _resolutionAudio.GetPersistedBgmLinear(),
            sfxSlider != null ? sfxSlider.value : _resolutionAudio.GetPersistedSfxLinear());
    }

    private void AssignListeners()
    {
        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(SetBgmVolume);
            bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(SetFullscreen);
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
    }

    private void EnsureUiReferences()
    {
        if (settingPanel == null)
            return;

        SettingDisplayControlsFactory.EnsureDisplayControls(
            settingPanel.transform,
            ref resolutionDropdown,
            ref fullscreenToggle);
    }

    public void ToggleSettingPanel()
    {
        if (!isPanelOpen && ModalGamePause.IsDialogueLogOpen)
            return;

        isPanelOpen = !isPanelOpen;
        settingPanel.SetActive(isPanelOpen);

        if (dialogInput != null)
            dialogInput.enabled = !isPanelOpen;

        Flowchart fc = FlowchartLocator.Resolve(targetFlowchart);
        if (fc != null)
            fc.SetBooleanVariable(fungusVariableName, isPanelOpen);

        if (isPanelOpen)
        {
            EnsureUiReferences();
            LoadSettings();
            if (resolutionDropdown != null)
                _resolutionAudio.InitializeResolutionDropdown(resolutionDropdown);
            AssignListeners();
            EnsureSettingsCanvasSortsAboveSayDialog();
            SettingPanelWorldInputBlocker.Begin(settingPanel);
            ModalInputGate.Begin(this, settingPanel, blocksHud: true, blocksWorld: true);
            // 설정 패널 밖 UI 클릭을 EventSystem 레벨에서 소비하는 투명 차단막(공통 처리).
            settingsRaycastBlocker = ModalRaycastBlocker.Create(settingPanel.transform);
            Time.timeScale = 0f;
        }
        else
        {
            ModalInputGate.End(this);
            ModalRaycastBlocker.Remove(settingsRaycastBlocker);
            settingsRaycastBlocker = null;
            if (ModalGamePause.ShouldEndWorldInputBlocker())
                SettingPanelWorldInputBlocker.End();
            Time.timeScale = ModalGamePause.ResolveTimeScaleOnClose();
        }
    }

    static bool ShouldIgnoreEscapeToggle()
    {
        var log = DialogueLogPanel.Instance;
        if (log == null)
            return false;

        if (log.SuppressOtherModalEscapeHandling)
            return true;

        return log.IsOpen;
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
        CleanupDontDestroyGameplayRoots();
    }

    /// <summary>
    /// Runtime entry point: discovers every current DontDestroyOnLoad root and
    /// wipes the ones not covered by <see cref="DontDestroyGameplayCleanup.ShouldPreserveRoot"/>.
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
    public void CleanupDontDestroyGameplayRoots(IList<GameObject> roots, System.Action<GameObject> destroyRoot = null)
    {
        DontDestroyGameplayCleanup.DestroyUnpreservedRoots(roots, gameObject, destroyRoot);
    }

    public static bool ShouldPreserveDontDestroyRoot(GameObject root, GameObject currentSettingsObject)
    {
        // Keep audio/video settings across return-to-menu.
        // Do NOT preserve GlobalVariables / Variablemanager — those carry Fungus gameplay flags.
        return DontDestroyGameplayCleanup.ShouldPreserveRoot(root, currentSettingsObject);
    }

    public void ReturnToGame()
    {
        CloseSettingPanel();
        GameLog.Log("게임 복귀");
    }

    protected override void OnDestroy()
    {
        ModalInputGate.End(this);
        ModalRaycastBlocker.Remove(settingsRaycastBlocker);
        settingsRaycastBlocker = null;
        SettingPanelWorldInputBlocker.End();
        base.OnDestroy();
    }
}
