using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Linq; // ★★★ 이 줄을 꼭 추가해주세요! ★★★

public class SettingManager : MonoBehaviour
{
    [Header("UI Components")]
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

    void Start()
    {
        LoadSettings();
        AssignListeners();
        InitializeResolutionDropdown(); // ★★★ 함수 이름 변경 ★★★

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SelectUIElement(0);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToMainMenuViaEsc();
            return;
        }

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
        // 리스너 추가는 InitializeResolutionDropdown에서 하므로 여기서는 제거합니다.
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

    // ★★★ 이 아래 해상도 관련 함수들이 모두 수정되었습니다. ★★★

    private void InitializeResolutionDropdown()
    {
        // 1. 시스템이 지원하는 모든 해상도를 가져옵니다.
        var allResolutions = Screen.resolutions;

        // 2. Parsec 등으로 인한 주사율 중복을 제거합니다.
        //    (너비/높이가 같으면 주사율이 가장 높은 것 1개만 남깁니다)
        var uniqueResolutions = allResolutions
            .GroupBy(r => new { r.width, r.height })
            .Select(group => group.OrderByDescending(r => r.refreshRateRatio.value).First());

        // 3. 우리가 원하는 일반적인 해상도 목록만 필터링합니다.
        List<Vector2Int> commonResolutions = new List<Vector2Int>()
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1366, 768),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160) // 사용자가 제공한 목록 기준
        };

        // 4. 중복 제거된 해상도 중에서 '일반 해상도'에 포함되는 것만 골라냅니다.
        resolutions = uniqueResolutions
            .Where(r => commonResolutions.Any(c => c.x == r.width && c.y == r.height))
            .ToList(); // 최종 목록 생성

        // 5. 드롭다운 옵션을 구성합니다.
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();
        int defaultResolutionIndex = 0; // 저장된 값이 없을 경우의 기본값

        for (int i = 0; i < resolutions.Count; i++)
        {
            var r = resolutions[i];
            options.Add($"{r.width} x {r.height}");

            // 현재 화면 해상도와 일치하는 인덱스를 찾아 기본값으로 설정
            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
            {
                defaultResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);

        // 6. 저장된 해상도 값을 불러옵니다.
        currentResolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", defaultResolutionIndex);
        
        // 저장된 값이 유효하지 않을 경우(예: 모니터 변경) 기본값으로 리셋
        if (currentResolutionIndex >= resolutions.Count || currentResolutionIndex < 0)
        {
            currentResolutionIndex = defaultResolutionIndex;
        }

        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();

        // 7. 리스너를 추가하고 해상도를 적용합니다.
        resolutionDropdown.onValueChanged.RemoveAllListeners(); // 중복 방지
        resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChanged);
        
        // 8. 저장된 해상도를 즉시 적용합니다.
        SetResolution(currentResolutionIndex);
    }

    // 드롭다운 값이 변경될 때 호출되는 함수
    private void OnResolutionDropdownChanged(int index)
    {
        SetResolution(index);
    }

    // 해상도를 적용하고 저장하는 함수
    private void SetResolution(int index)
    {
        // 유효한 인덱스인지 확인
        if (index < 0 || index >= resolutions.Count)
        {
            return;
        }

        currentResolutionIndex = index;
        Resolution resolution = resolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
    }

    public void BackToMainMenu()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void BackToMainMenuViaEsc()
    {
        BackToMainMenu();
    }

    // --- 이하 키보드 조작 처리 ---
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
            HandleSelectionKeyboardInput();
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