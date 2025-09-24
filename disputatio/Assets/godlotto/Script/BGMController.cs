using UnityEngine;

public class BGMController : MonoBehaviour
{
    // Inspector 창에서 미리 BGM 파일을 연결해 둘 변수
    public AudioClip musicToPlay;

    private AudioSource bgmSource;

    void Awake()
    {
        bgmSource = GetComponent<AudioSource>();
    }

    // 파라미터가 없는 public 함수로 변경
    public void PlayMusic()
    {
        if (bgmSource != null && musicToPlay != null)
        {
            bgmSource.clip = musicToPlay;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }
}