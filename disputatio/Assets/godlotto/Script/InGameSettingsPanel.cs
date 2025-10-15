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

    [Header("Fungus 연동")] // 이 부분을 추가하세요
    public Flowchart targetFlowchart;
    public Fungus.DialogInput dialogInput;


    [Header("UI Components")]
    public GameObject settingPanel; // 설정 UI의 부모 패널 GameObject
    public GameObject targetObject;
    public AudioMixer audioMixer;
    public Slider bgmSlider;
    public Slider sfxSlider;
    public TextMeshProUGUI resolutionText;
    public Button resolutionButton; // 키보드 조작을 위해 참조
    public Toggle fullscreenToggle;

    [Header("Keyboard Navigation")]
    public Selectable[] navigableElements; // 키보드로 이동할 UI 요소들
    private int currentIndex = 0;

    [Header("Scene Navigation")]
    public string mainMenuSceneName = "MainMenuScene";

    private List<Resolution> resolutions;
    private int currentResolutionIndex = 0;
    private bool isPanelOpen = false;

    void Awake()
    {
        // 싱글톤 패턴: 단 하나의 인스턴스만 유지
        if (instance != null)
        {
            Debug.LogWarning("GameManager 중복 생성 시도. 기존 인스턴스가 있어 자신을 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject); // 이 GameManager는 씬 전환 시 파괴되지 않도록 설정
        
        // settingPanel은 초기에는 비활성화 상태
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
        // 이 스크립트는 게임 내 패널을 위한 것이므로, 시작 시 항상 패널을 닫고 시작합니다.
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
    
    // (이 아래 코드는 이전 답변의 최종 완성 코드와 거의 동일합니다.
    // 클래스 이름만 InGameSettingsPanel로 바뀌었습니다.)
    
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
        {
            // isPanelOpen이 true면(패널이 열리면) dialogInput.enabled는 false가 됩니다.
            // isPanelOpen이 false면(패널이 닫히면) dialogInput.enabled는 true가 됩니다.
            dialogInput.enabled = !isPanelOpen;
        }
    }

    public void OpenSettingPanel()
    {
        if (!isPanelOpen) ToggleSettingPanel();
    }

    public void CloseSettingPanel()
    {
        if (isPanelOpen)
        {
            ToggleSettingPanel();
        }
    }
    
    private void HandleKeyboardInput()
    {
        bool isKeyboardInput = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                                 Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                                 Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        if (isKeyboardInput && EventSystem.current.currentSelectedGameObject == null)
        {
            SelectUIElement(currentIndex);
        }

        if (EventSystem.current.currentSelectedGameObject == null) return;
        
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            HandleNavigation();
        }
        else
        {
            HandleSelectionKeyboardInput();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                HandleEnterPress();
            }
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
        {
            button.onClick.Invoke();
        }
    }

    private void SelectUIElement(int index)
    {
        if (navigableElements.Length > 0 && index >= 0 && index < navigableElements.Length)
        {
            EventSystem.current.SetSelectedGameObject(navigableElements[index].gameObject);
            currentIndex = index;
        }
    }
    
    private IEnumerator SelectFirstElementAfterRealtime()
    {
        yield return new WaitForSecondsRealtime(0.05f);
        EventSystem.current.SetSelectedGameObject(null);
        if (navigableElements.Length > 0)
        {
            SelectUIElement(0);
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
        {
            currentResolutionIndex = resolutions.Count - 1;
        }
        
        SetResolution(currentResolutionIndex);
    }
    
    public void CycleResolutionForward()
    {
        CycleResolution(1);
    }

    public void CycleResolutionBackward()
    {
        CycleResolution(-1);
    }

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
            resolutionText.text = resolutions[currentResolutionIndex].width + " x " + resolutions[currentResolutionIndex].height;
    }

    public void BackToMainMenu()
{
    // 1. 게임 시간을 반드시 정상으로 되돌립니다.
    Time.timeScale = 1f;

    // 2. 메인 메뉴 씬을 로드합니다. (오브젝트를 파괴하거나 숨기는 코드는 모두 제거)
    SceneManager.LoadScene(mainMenuSceneName);
}

    public void ReturnToGame()
    {
        CloseSettingPanel();
        Debug.Log("클릭되었다");
    }
}