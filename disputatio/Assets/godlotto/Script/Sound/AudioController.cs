using UnityEngine;
using System.Collections;

public class AudioController : MonoBehaviour
{
    public static AudioController instance; // 싱글톤 (BGM 관리용)

    [Header("Settings")]
    private AudioSource audioSource; // 스피커 하나만 사용

    [Header("BGM Playlist")]
    public AudioClip[] bgmList; // [0], [1], [2]... 여러 곡 등록 가능

    [Header("SFX Settings")]
    public AudioClip footstepSound;
    public float delayBetweenSteps = 0.3f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        // ★ 중요: 이 스크립트가 "BGM 관리자"로 쓰일 때만 싱글톤 적용
        // bgmList에 곡이 하나라도 들어있다면 "아, 나는 BGM 관리자구나"라고 판단
        if (bgmList != null && bgmList.Length > 0)
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject); // 씬 이동 시 살아남음
            }
            else
            {
                Destroy(gameObject); // 이미 BGM 관리자가 있으면 나는 사라짐
            }
        }
        // bgmList가 비어있다면(발자국용 오브젝트라면) 그냥 평범한 오브젝트로 남음 (싱글톤 X, DontDestroy X)
    }

    // ---------------------------------------------------------
    // ★ BGM 골라 듣기 기능
    // ---------------------------------------------------------
    public void PlayBGM(int index)
    {
        // 인덱스 예외 처리
        if (bgmList == null || index < 0 || index >= bgmList.Length) return;

        if (audioSource != null)
        {
            // 이미 그 노래가 재생 중이면 끊지 않고 유지 (이어듣기)
            if (audioSource.isPlaying && audioSource.clip == bgmList[index])
            {
                return; 
            }

            audioSource.clip = bgmList[index];
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    public void StopMusic()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    // ---------------------------------------------------------
    // 발자국 소리 기능
    // ---------------------------------------------------------
    public void PlayFootstep()
    {
        if (footstepSound != null && audioSource != null)
        {
            StartCoroutine(FootstepCoroutine());
        }
    }

    private IEnumerator FootstepCoroutine()
    {
        for (int i = 0; i < 4; i++)
        {
            // PlayOneShot을 쓰면 BGM이나 다른 소리 위에 겹쳐서 재생됨
            audioSource.PlayOneShot(footstepSound);
            yield return new WaitForSeconds(delayBetweenSteps);
        }
    }
}