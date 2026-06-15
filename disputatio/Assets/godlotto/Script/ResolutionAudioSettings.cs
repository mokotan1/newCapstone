using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Resolution list, fullscreen, and mixer volume persistence for in-game settings UI.
/// When <see cref="GlobalSettingManager"/> exists, all changes go through it (single source of truth).
/// Otherwise uses the supplied <see cref="AudioMixer"/> and <see cref="Screen"/> directly.
/// </summary>
public sealed class ResolutionAudioSettings
{
    private readonly AudioMixer _fallbackMixer;

    private List<Resolution> _localResolutions;

    public ResolutionAudioSettings(AudioMixer fallbackMixer)
    {
        _fallbackMixer = fallbackMixer;
    }

    private static GlobalSettingManager Global => GlobalSettingManager.Instance;

    /// <summary>Audio changes go through the global manager when it has a mixer wired.</summary>
    private static bool UseGlobalAudio =>
        Global != null && Global.audioMixer != null;

    /// <summary>Resolution list / index use the global manager when it has been initialized.</summary>
    private static bool UseGlobalGraphics =>
        Global != null &&
        Global.availableResolutions != null &&
        Global.availableResolutions.Count > 0;

    /// <summary>BGM linear slider value to assign on load.</summary>
    public float GetPersistedBgmLinear()
    {
        if (Global != null)
            return Global.bgmVolume;
        return PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
    }

    /// <summary>SFX linear slider value to assign on load.</summary>
    public float GetPersistedSfxLinear()
    {
        if (Global != null)
            return Global.sfxVolume;
        return PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume, SettingPlayerPrefsKeys.DefaultLinearVolume);
    }

    public bool GetPersistedFullscreen()
    {
        if (Global != null)
            return Global.isFullscreen;
        return PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen, SettingPlayerPrefsKeys.FullscreenDefaultEnabled) == 1;
    }

    /// <summary>Apply mixer volumes for current slider values (call after sliders are set).</summary>
    public void ApplyAudioFromLinear(float bgmLinear, float sfxLinear)
    {
        SetBgmVolume(bgmLinear);
        SetSfxVolume(sfxLinear);
    }

    public void SetBgmVolume(float volume)
    {
        if (UseGlobalAudio)
        {
            Global.SetBGM(volume);
            return;
        }

        AudioMixerVolumeUtility.SetExposedVolume(_fallbackMixer, SettingPlayerPrefsKeys.BgmVolume, volume);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, volume);
    }

    public void SetSfxVolume(float volume)
    {
        if (UseGlobalAudio)
        {
            Global.SetSFX(volume);
            return;
        }

        AudioMixerVolumeUtility.SetExposedVolume(_fallbackMixer, SettingPlayerPrefsKeys.SfxVolume, volume);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, volume);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        if (Global != null)
        {
            Global.SetFullscreen(isFullscreen);
            return;
        }

        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, isFullscreen ? 1 : 0);
    }

    public void InitializeResolutionDropdown(TMP_Dropdown resolutionDropdown)
    {
        if (resolutionDropdown == null)
            return;

        resolutionDropdown.ClearOptions();

        if (UseGlobalGraphics)
        {
            List<string> options = Global.GetResolutionOptions();
            if (options == null || options.Count == 0)
                options = ResolutionListUtility.BuildLabels(ResolutionListUtility.BuildPreferredResolutionList());
            if (options.Count == 0)
                options.Add("1920 x 1080");

            resolutionDropdown.AddOptions(options);
            int idx = Mathf.Clamp(Global.currentResolutionIndex, 0, Mathf.Max(0, options.Count - 1));
            resolutionDropdown.value = idx;
            resolutionDropdown.RefreshShownValue();
            resolutionDropdown.onValueChanged.RemoveAllListeners();
            resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChangedGlobal);
            return;
        }

        _localResolutions = ResolutionListUtility.BuildPreferredResolutionList();
        List<string> localOptions = ResolutionListUtility.BuildLabels(_localResolutions);
        if (localOptions.Count == 0)
            localOptions.Add("1920 x 1080");

        resolutionDropdown.AddOptions(localOptions);

        int currentScreenIndex = 0;
        for (int i = 0; i < _localResolutions.Count; i++)
        {
            Resolution r = _localResolutions[i];
            if (r.width == Screen.width && r.height == Screen.height)
                currentScreenIndex = i;
        }

        int savedResIndex = PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex, -1);
        if (savedResIndex >= 0 && savedResIndex < _localResolutions.Count)
            currentScreenIndex = savedResIndex;

        resolutionDropdown.value = currentScreenIndex;
        resolutionDropdown.RefreshShownValue();
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.AddListener(OnResolutionDropdownChangedLocal);
    }

    private void OnResolutionDropdownChangedGlobal(int index)
    {
        Global.SetResolutionIndex(index);
    }

    private void OnResolutionDropdownChangedLocal(int index)
    {
        if (_localResolutions == null || index < 0 || index >= _localResolutions.Count)
            return;

        Resolution resolution = _localResolutions[index];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, index);
    }
}
