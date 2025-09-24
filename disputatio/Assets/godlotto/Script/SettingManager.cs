using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class SettingManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioMixer audioMixer;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("Graphics Settings")]
    public TextMeshProUGUI resolutionText;
    public Button resolutionButton;
    public Toggle fullscreenToggle;
    private List<Resolution> resolutions;
    private int currentResolutionIndex = 0;

    [Header("Navigation")]
    public string mainMenuSceneName = "MainMenuScene";
    public Selectable[] navigableElements;
    private int currentIndex = 0;

    [Header("In-Game Panel Specific")]
    public GameObject settingPanel;

    void Start()
    {
        // 씬/패널 시작 시 커서 상태를 항상 '보이고 자유로운' 상태로 초기화
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        InitializeAudioSliders();
        InitializeResolution();
        
        if (fullscreenToggle != null)
        {
            // 저장된 전체화면 설정을 불러와 토글에 반영
            fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
        }
    }

    // 오브젝트가 활성화될 때마다 (패널이 켜질 때마다) 호출됩니다.
    void OnEnable()
    {
        // Time.timeScale에 영향을 받지 않는 안정적인 UI 선택 코루틴 실행
        StartCoroutine(SelectFirstElementAfterRealtime());
    }

    // 현실 시간 기준으로 기다려서 첫 UI 요소를 선택하는 코루틴
    IEnumerator SelectFirstElementAfterRealtime()
    {
        // 게임 시간이 멈춰도 현실 시간으로 아주 잠시 기다립니다.
        yield return new WaitForSecondsRealtime(0.02f); 
        
        EventSystem.current.SetSelectedGameObject(null);
        if (navigableElements.Length > 0)
        {
            EventSystem.current.SetSelectedGameObject(navigableElements[0].gameObject);
            currentIndex = 0;
        }
    }

    void Update()
    {
        // 키보드 입력을 감지하여 마우스 사용 후 포커스를 다시 가져오는 로직
        bool isKeyboardInput = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                                 Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                                 Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        if (isKeyboardInput && EventSystem.current.currentSelectedGameObject == null)
        {
            SelectUIElement(currentIndex);
        }
        
        if (EventSystem.current.currentSelectedGameObject == null) return;
        
        // 상하 방향키로 UI 요소 간 이동
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            HandleNavigation();
        }
        // 그 외, 좌우 방향키로 값 조절
        else
        {
            HandleSelectionKeyboardInput();
        }

        // 엔터/스페이스 키로 버튼 클릭
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            HandleEnterPress();
        }

        // Backspace 키로 메인 메뉴로 돌아가기 (SettingScene에서 유용)
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            BackToMainMenu();
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
        Button selectedButton = navigableElements[currentIndex].GetComponent<Button>();
        if (selectedButton != null)
        {
            selectedButton.onClick.Invoke();
        }
    }

    private void SelectUIElement(int index)
    {
        if (navigableElements.Length > 0 && index < navigableElements.Length)
        {
            EventSystem.current.SetSelectedGameObject(navigableElements[index].gameObject);
            currentIndex = index;
        }
    }

    private void HandleSelectionKeyboardInput()
    {
        GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
        if (selectedObj == null) return;

        if (selectedObj == bgmSlider.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) bgmSlider.value = Mathf.Clamp01(bgmSlider.value + 0.1f);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) bgmSlider.value = Mathf.Clamp01(bgmSlider.value - 0.1f);
        }
        else if (selectedObj == sfxSlider.gameObject)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) sfxSlider.value = Mathf.Clamp01(sfxSlider.value + 0.1f);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) sfxSlider.value = Mathf.Clamp01(sfxSlider.value - 0.1f);
        }
        else if (selectedObj.GetComponentInParent<Button>() == resolutionButton)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) CycleResolution(1);
            if (Input.GetKeyDown(KeyCode.LeftArrow)) CycleResolution(-1);
        }
    }
    
    private void InitializeAudioSliders()
    {
        bgmSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
        bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        SetBgmVolume(bgmSlider.value);
        SetSfxVolume(sfxSlider.value);
    }

    private void InitializeResolution()
    {
        resolutions = new List<Resolution>(Screen.resolutions);
        resolutions.Sort((a, b) => {
            if (a.width != b.width) return a.width.CompareTo(b.width);
            else return a.height.CompareTo(b.height);
        });
        
        int savedIndex = PlayerPrefs.GetInt("ResolutionIndex", -1);
        if (savedIndex != -1 && savedIndex < resolutions.Count)
        {
            currentResolutionIndex = savedIndex;
        }
        else
        {
            bool foundCurrent = false;
            for (int i = 0; i < resolutions.Count; i++)
            {
                if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                {
                    currentResolutionIndex = i;
                    foundCurrent = true;
                    break;
                }
            }
            if (!foundCurrent) currentResolutionIndex = resolutions.Count - 1;
        }

        UpdateResolutionText();
        SetResolution(currentResolutionIndex);
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
    
    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ReturnToGame()
    {
        // '게임으로 돌아가기' 버튼의 OnClick() 이벤트를 Inspector에서 GameManager의 ResumeGame() 함수에 직접 연결하는 것을 권장합니다.
        if (FindFirstObjectByType<GameManager>() != null)
        {
            FindFirstObjectByType<GameManager>().ResumeGame();
        }
    }
    
    public void CycleResolution(int direction)
    {
        currentResolutionIndex += direction;
        if (currentResolutionIndex >= resolutions.Count) currentResolutionIndex = 0;
        if (currentResolutionIndex < 0) currentResolutionIndex = resolutions.Count - 1;

        SetResolution(currentResolutionIndex);
    }

    public void SetResolution(int resolutionIndex)
    {
        currentResolutionIndex = resolutionIndex;
        UpdateResolutionText();
        
        Resolution resolution = resolutions[currentResolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        
        PlayerPrefs.SetInt("ResolutionIndex", currentResolutionIndex);
        
        if (resolutionButton != null)
        {
            resolutionButton.Select();
        }
    }

    public void CycleResolutionForward()
    {
        CycleResolution(1);
    }

    public void CycleResolutionBackward()
    {
        CycleResolution(-1);
    }

    private void UpdateResolutionText()
    {
        if(resolutionText != null)
            resolutionText.text = resolutions[currentResolutionIndex].width + " x " + resolutions[currentResolutionIndex].height;
    }
}