using UnityEngine;
using System.Collections;

public class AudioController : MonoBehaviour
{
    public static AudioController instance; // 싱글톤

    [Header("Settings")]
    private AudioSource audioSource; 

    [Header("BGM Playlist")]
    public AudioClip[] bgmList; // [0]:메인, [1]:게임, [2]:보스 등

    [Header("SFX Playlist")]
    // ★ 수정됨: 하나의 변수 대신 배열로 변경하여 여러 효과음 등록 가능
    // 예: [0]:발자국, [1]:클릭, [2]:점프, [3]:아이템획득
    public AudioClip[] sfxList; 

    [Header("Footstep Settings")]
    public float delayBetweenSteps = 0.3f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // 싱글톤 로직 (BGM 관리자일 경우에만 유지)
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
            }
        }
    }

    // ---------------------------------------------------------
    // BGM 기능 (기존 동일)
    // ---------------------------------------------------------
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
        if (audioSource != null) audioSource.Stop();
    }

    // ---------------------------------------------------------
    // ★ [추가됨] 일반 효과음 재생 기능 (단발성)
    // ---------------------------------------------------------
    // 사용법: AudioController.instance.PlaySFX(1); // 1번 효과음 재생
    public void PlaySFX(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;
        
        if (audioSource != null)
        {
            // PlayOneShot은 현재 재생 중인 BGM을 끊지 않고 소리를 겹쳐서 재생합니다.
            audioSource.PlayOneShot(sfxList[index]);
        }
    }

    // ---------------------------------------------------------
    // ★ [수정됨] 발자국 소리 기능 (특정 인덱스의 소리를 4번 반복)
    // ---------------------------------------------------------
    // 사용법: AudioController.instance.PlayFootstep(0); // 0번(발자국) 소리를 패턴대로 재생
    public void PlayFootstep(int index)
    {
        if (sfxList == null || index < 0 || index >= sfxList.Length) return;

        if (audioSource != null)
        {
            StartCoroutine(FootstepCoroutine(index));
        }
    }

    private IEnumerator FootstepCoroutine(int index)
    {
        // 지정된 효과음을 4번 반복 재생
        for (int i = 0; i < 4; i++)
        {
            audioSource.PlayOneShot(sfxList[index]);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}