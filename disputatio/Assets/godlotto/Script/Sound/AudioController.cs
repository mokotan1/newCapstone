using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class AudioController : SingletonMonoBehaviour<AudioController>
{
    [System.Obsolete("Use Instance instead.")]
    public static AudioController instance => Instance;

    protected override bool PersistAcrossScenes => true;

    [Header("Settings")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("BGM Playlist")]
    public AudioClip[] bgmList; // [0]:메인, [1]:게임, [2]:보스 등

    [Header("SFX Playlist")]
    public AudioClip[] sfxList;

    [Header("Footstep Settings")]
    public float delayBetweenSteps = 0.3f;

    private readonly Dictionary<int, List<AudioSource>> activeSfxSourcesByIndex = new Dictionary<int, List<AudioSource>>();

    protected override void Awake()
    {
        ResolveAudioSources();
        base.Awake();
    }

    private void ResolveAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        if (bgmAudioSource == null)
            bgmAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        if (sfxAudioSource == null)
            sfxAudioSource = sources.Length > 1 ? sources[1] : bgmAudioSource;
    }

    // ★★★★★ MainMenuScene에서 음악 자동 정지
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.MainMenu)
        {
            StopMusic();
            // 필요하면 메인메뉴 BGM을 재생하고 싶을 경우:
            // PlayBGM(0); // 예: 0번이 메인메뉴 BGM
        }
    }
    // ★★★★★ 끝

    // BGM 기능
    public void PlayBGM(int index)
    {
        if (bgmList == null || index < 0 || index >= bgmList.Length) return;

        if (bgmAudioSource != null)
        {
            if (bgmAudioSource.isPlaying && bgmAudioSource.clip == bgmList[index]) return;

            bgmAudioSource.clip = bgmList[index];
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }
    }

    public void StopMusic()
    {
        if (bgmAudioSource != null)
            bgmAudioSource.Stop();
    }

    // SFX 기능
    public void PlaySFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        PlayTrackedSFX(index, loop: false);
    }

    public void PlayLoopingSFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        PlayTrackedSFX(index, loop: true);
    }

    public void StopSFX(int index)
    {
        if (!activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
            return;

        for (int i = sources.Count - 1; i >= 0; i--)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.Stop();
            Destroy(source);
        }

        sources.Clear();
        activeSfxSourcesByIndex.Remove(index);
    }

    public void StopAllSFX()
    {
        var indices = new List<int>(activeSfxSourcesByIndex.Keys);
        foreach (int index in indices)
            StopSFX(index);
    }

    private void PlayTrackedSFX(int index, bool loop)
    {
        if (sfxAudioSource == null)
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = sfxList[index];
        source.outputAudioMixerGroup = sfxAudioSource.outputAudioMixerGroup;
        source.volume = sfxAudioSource.volume;
        source.pitch = sfxAudioSource.pitch;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = sfxAudioSource.spatialBlend;
        source.panStereo = sfxAudioSource.panStereo;
        source.priority = sfxAudioSource.priority;
        source.Play();

        if (!activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
        {
            sources = new List<AudioSource>();
            activeSfxSourcesByIndex[index] = sources;
        }

        sources.Add(source);

        if (!loop)
            StartCoroutine(ReleaseSfxSourceAfterPlayback(index, source));
    }

    private IEnumerator ReleaseSfxSourceAfterPlayback(int index, AudioSource source)
    {
        yield return new WaitWhile(() => source != null && source.isPlaying);

        if (source != null)
        {
            if (activeSfxSourcesByIndex.TryGetValue(index, out List<AudioSource> sources))
            {
                sources.Remove(source);
                if (sources.Count == 0)
                    activeSfxSourcesByIndex.Remove(index);
            }

            Destroy(source);
        }
    }

    // 발자국 기능
    public void PlayFootstep(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        if (sfxList[index] == null) return;

        if (sfxAudioSource != null)
            StartCoroutine(FootstepCoroutine(index));
    }

    private IEnumerator FootstepCoroutine(int index)
    {
        for (int i = 0; i < 4; i++)
        {
            PlaySFX(index);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}
