using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

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

        if (sfxAudioSource != null)
            sfxAudioSource.PlayOneShot(sfxList[index]);
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
            sfxAudioSource.PlayOneShot(sfxList[index]);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}
