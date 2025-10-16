using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Fungus;

public class InGameSettingsPanel : MonoBehaviour
{
    public static InGameSettingsPanel instance;

    [Header("Fungus 연동")]
    public Flowchart targetFlowchart;
    public string fungusVariableName = "isCalled";
    public Fungus.DialogInput dialogInput;

    [Header("UI Components")]
    public GameObject settingPanel;
    public AudioMixer audioMixer;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public TextMeshProUGUI resolutionText;
    public Button resolutionButton;
    public Toggle fullscreenToggle;

    [Header("Keyboard Navigation")]
    public Selectable[] navigableElements;
    private int currentIndex = 0;

    [Header("Scene Navigation")]
    public string mainMenuSceneName = "MainMenuScene";

    private List<Resolution> resolutions;
    private int currentResolutionIndex = 0;
    private bool isPanelOpen = false;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (settingPanel != null)
            settingPanel.SetActive(false);

        isPanelOpen = false;
    }

    void Start()
    {
        LoadSettings();
        AssignListeners();
        InitializeResolution();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettingPanel();
        }

        if (!isPanelOpen) return;
        HandleKeyboardInput();
    }

    private void LoadSettings()
    {
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        SetBgmVolume(bgmSlider.value);
        SetSfxVolume(sfxSlider.value);
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

        if (targetFlowchart != null)
            targetFlowchart.SetBooleanVariable(fungusVariableName, isPanelOpen);
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
        {
            HandleSelectionKeyboardInput();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
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

    private void HandleSelectionKeyboardInput()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null) return;

        if (selectedObj == bgmSlider.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) bgmSlider.value += 0.1f;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) bgmSlider.value -= 0.1f;
        }
        else if (selectedObj == sfxSlider.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) sfxSlider.value += 0.1f;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) sfxSlider.value -= 0.1f;
        }
        else if (selectedObj == resolutionButton.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) CycleResolution(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) CycleResolution(-1);
        }
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
        audioMixer.SetFloat("BGMVolume", volume == 0 ? -80 : Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("BGMVolume", volume);
    }

    public void SetSfxVolume(float volume)
    {
        audioMixer.SetFloat("SFXVolume", volume == 0 ? -80 : Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
    }

    private void InitializeResolution()
    {
        resolutions = new List<Resolution>(Screen.resolutions);
        resolutions.RemoveAll(res => res.refreshRateRatio.value < 60);
        resolutions.Sort((a, b) => (a.width.CompareTo(b.width) * 1000) + a.height.CompareTo(b.height));

        currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", resolutions.Count - 1);
        if (currentResolutionIndex >= resolutions.Count)
            currentResolutionIndex = resolutions.Count - 1;

        SetResolution(currentResolutionIndex);
    }

    public void CycleResolutionForward() => CycleResolution(1);
    public void CycleResolutionBackward() => CycleResolution(-1);

    private void CycleResolution(int direction)
    {
        currentResolutionIndex += direction;
        if (currentResolutionIndex >= resolutions.Count) currentResolutionIndex = 0;
        if (currentResolutionIndex < 0) currentResolutionIndex = resolutions.Count - 1;
        SetResolution(currentResolutionIndex);
    }

    private void SetResolution(int index)
    {
        currentResolutionIndex = index;
        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
        UpdateResolutionText();
    }

    private void UpdateResolutionText()
    {
        if (resolutionText != null)
            resolutionText.text = $"{resolutions[currentResolutionIndex].width} x {resolutions[currentResolutionIndex].height}";
    }

    public void BackToMainMenu()
{
    Debug.Log("메인메뉴 이동 버튼 클릭됨");
    StartCoroutine(GoToMainMenu());
}

    private IEnumerator GoToMainMenu()
{
    Time.timeScale = 1f;

    if (targetFlowchart != null)
        targetFlowchart.SetBooleanVariable(fungusVariableName, false);

    CleanupDontDestroyObjects();
    Debug.Log("모든 DontDestroyOnLoad 오브젝트 삭제 완료");

    yield return null;

    Debug.Log($"씬 로드 시도: {mainMenuSceneName}");
    SceneManager.LoadScene(mainMenuSceneName);

    // ✅ 메인 메뉴로 넘어간 뒤 자신 제거 (다음 프레임에 파괴)
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
        // 자기 자신은 남겨야 코루틴이 끝까지 돌아서 씬 로드가 됨
        if (obj == gameObject) continue;

        // ① Fungus 전역 변수 저장소는 삭제하지 않음
        if (obj.GetComponent<GlobalVariables>() != null) continue;

        // ② (선택) 마커/이름으로도 제외 가능
        if (obj.GetComponent<KeepAcrossScenes>() != null) continue;
        if (obj.name == "Variablemanager") continue;

        Destroy(obj);
    }
}


    public void ReturnToGame()
    {
        CloseSettingPanel();
        Debug.Log("게임 복귀");
    }
}
