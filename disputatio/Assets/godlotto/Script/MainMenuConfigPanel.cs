using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[ExecuteAlways]
public class MainMenuConfigPanel : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    private ResolutionAudioSettings resolutionAudio;
    private bool initialized;

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        FindExistingControls();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EnsureEditorVisibleControls();
#endif
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureEditorVisibleControls();
            return;
        }
#endif
        Initialize();
        SyncFromSettings();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (EventSystem.current != null && bgmSlider != null)
            EventSystem.current.SetSelectedGameObject(bgmSlider.gameObject);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        EnsureCursorVisible();
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        gameObject.SetActive(!gameObject.activeSelf);
    }

    private void Initialize()
    {
        if (initialized)
            return;

        FindExistingControls();

        resolutionAudio = new ResolutionAudioSettings(audioMixer);
        initialized = true;
    }

    private void SyncFromSettings()
    {
        if (!initialized)
            Initialize();

        if (bgmSlider != null)
        {
            bgmSlider.onValueChanged.RemoveListener(SetBgmVolume);
            bgmSlider.value = resolutionAudio.GetPersistedBgmLinear();
            bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(SetSfxVolume);
            sfxSlider.value = resolutionAudio.GetPersistedSfxLinear();
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }

        resolutionAudio.ApplyAudioFromLinear(
            bgmSlider != null ? bgmSlider.value : resolutionAudio.GetPersistedBgmLinear(),
            sfxSlider != null ? sfxSlider.value : resolutionAudio.GetPersistedSfxLinear());
    }

    private void SetBgmVolume(float volume)
    {
        EnsureCursorVisible();
        resolutionAudio.SetBgmVolume(volume);
    }

    private void SetSfxVolume(float volume)
    {
        EnsureCursorVisible();
        resolutionAudio.SetSfxVolume(volume);
    }

    private void EnsureCursorVisible()
    {
        if (!Cursor.visible)
            Cursor.visible = true;

        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;
    }

    private void FindExistingControls()
    {
        if (bgmSlider == null)
            bgmSlider = FindChildComponent<Slider>("BGMSlider");
        if (sfxSlider == null)
            sfxSlider = FindChildComponent<Slider>("SFXSlider");
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == childName)
                return components[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            EnsureEditorVisibleControls();
    }

    private void EnsureEditorVisibleControls()
    {
        FindExistingControls();
    }
#endif
}
