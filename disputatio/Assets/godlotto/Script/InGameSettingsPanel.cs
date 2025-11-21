using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Fungus;
using System.Linq;

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
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;

    [Header("Keyboard Navigation")]
    public Selectable[] navigableElements;
    private int currentIndex = 0;

    [Header("Scene Navigation")]
    public string mainMenuSceneName = "MainMenuScene";

    private List<Resolution> resolutions;
    private int currentResolutionIndex = 0;
    private bool isPanelOpen = false;

    // ✅ 플레이 시간 관련 변수
    private float playTime = 0f;       
    private bool isCounting = true;    

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
        AssignListeners(); // 볼륨, 전체화면 리스너 등록
        InitializeResolutionDropdown(); // ★★★ 여기가 수정되었습니다 ★★★
    }

    void Update()
    {
        if (isCounting)
            playTime += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettingPanel();
        }

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
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        
        // ★ 중요: 전체화면 여부도 PlayerPrefs를 맹신하지 않고 현재 상태를 반영하는 것이 안전합니다.
        // 하지만 여기선 PlayerPrefs와 현재 상태를 동기화합니다.
        bool isFull = Screen.fullScreen;
        fullscreenToggle.isOn = isFull; 
        
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

    // --- 키보드 입력 처리 생략 (기존과 동일) ---
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
    // ----------------------------------------

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

    // ▼▼▼ [핵심 수정 부분] ▼▼▼
    private void InitializeResolutionDropdown()
    {
        // 1. 해상도 목록 생성 (SettingManager와 동일한 로직)
        var allResolutions = Screen.resolutions;
        var uniqueResolutions = allResolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(group => group.OrderByDescending(r => r.refreshRateRatio.value).First());

        List<Vector2Int> commonResolutions = new List<Vector2Int>()
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160)
        };

        resolutions = uniqueResolutions
            .Where(r => commonResolutions.Any(c => c.x == r.width && c.y == r.height))
            .ToList();

        // 2. 드롭다운 옵션 추가
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        
        int currentScreenIndex = 0; // 현재 화면 해상도와 일치하는 인덱스를 찾을 변수

        for (int i = 0; i < resolutions.Count; i++)
        {
            var r = resolutions[i];
            options.Add($"{r.width} x {r.height}");

            // ★ 수정됨: 저장된 값이 아니라, '지금 현재 화면의 해상도'와 일치하는지 확인합니다.
            // SettingManager에서 이미 해상도를 바꿨다면 Screen.width/height는 바뀐 값을 가지고 있습니다.
            if (r.width == Screen.width && r.height == Screen.height)
            {
                currentScreenIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        // 3. 드롭다운의 값을 '현재 화면 해상도'에 해당하는 인덱스로 설정
        // 이렇게 하면 1600x900 상태로 들어왔을 때 드롭다운도 1600x900을 가리킵니다.
        currentResolutionIndex = currentScreenIndex;
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 4. 리스너 연결
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChanged);

        // ★ 삭제됨: SetResolution(currentResolutionIndex); 
        // 시작하자마자 해상도를 강제로 다시 적용하지 않습니다. 
        // 이미 메인 메뉴에서 설정된 해상도가 유지되어야 하기 때문입니다.
    }

    private void OnResolutionDropdownChanged(int index)
    {
        SetResolution(index);
    }

    private void SetResolution(int index)
    {
        if (index < 0 || index >= resolutions.Count) return;
        currentResolutionIndex = index;
        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        
        // 인게임에서 바꿀 때만 PlayerPrefs에 저장
        PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
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
            if (obj.GetComponent<GlobalVariables>() != null) continue;
            if (obj.name == "Variablemanager") continue; // 오타 수정 (VariableManager 일 수 있음)
            Destroy(obj);
        }
    }

    public void ReturnToGame()
    {
        CloseSettingPanel();
        Debug.Log("게임 복귀");
    }
}