using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class AudioController : MonoBehaviour
{
    public static AudioController instance; // 싱글톤

    [Header("Settings")]
    private AudioSource audioSource;

    [Header("BGM Playlist")]
    public AudioClip[] bgmList; // [0]:메인, [1]:게임, [2]:보스 등

    [Header("SFX Playlist")]
    public AudioClip[] sfxList;

    [Header("Footstep Settings")]
    public float delayBetweenSteps = 0.3f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (bgmList != null && bgmList.Length > 0)
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }
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
        if (scene.name == "MainMenuScene")
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

        if (audioSource != null)
        {
            if (audioSource.isPlaying && audioSource.clip == bgmList[index]) return;

            audioSource.clip = bgmList[index];
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void StopMusic()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    // SFX 기능
    public void PlaySFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;

        if (audioSource != null)
            audioSource.PlayOneShot(sfxList[index]);
    }

    // 발자국 기능
    public void PlayFootstep(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;

        if (audioSource != null)
            StartCoroutine(FootstepCoroutine(index));
    }

    private IEnumerator FootstepCoroutine(int index)
    {
        for (int i = 0; i < 4; i++)
        {
            audioSource.PlayOneShot(sfxList[index]);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}
