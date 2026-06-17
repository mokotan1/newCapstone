using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioController : SingletonMonoBehaviour<AudioController>
{
    [System.Obsolete("Use Instance instead.")]
    public static AudioController instance => Instance;

    protected override bool PersistAcrossScenes => true;

    [Header("Settings")]
    [SerializeField] private AudioSource bgmAudioSource;

    [Header("BGM Playlist")]
    public AudioClip[] bgmList;

    protected override void Awake()
    {
        ResolveAudioSource();
        base.Awake();
    }

    private void ResolveAudioSource()
    {
        if (bgmAudioSource == null)
            bgmAudioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.MainMenu)
            StopMusic();
    }

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
}
