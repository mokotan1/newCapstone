using UnityEngine;

public class AudioBridge : MonoBehaviour
{
    // -----------------------------------------------------------
    // 1. 배경음(BGM) 관련 브릿지
    // -----------------------------------------------------------
    public void CallPlayBGM(int index)
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.PlayBGM(index);
        }
        else
        {
            Debug.LogWarning("AudioController(본체)가 없습니다!");
        }
    }

    public void CallStopMusic()
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.StopMusic();
        }
    }

    // -----------------------------------------------------------
    // 2. ★ [추가됨] 효과음(SFX) 관련 브릿지
    // -----------------------------------------------------------
    
    // 일반 효과음 (1번만 재생)
    // Fungus에서 Call Method -> CallPlaySFX(인덱스) 로 호출
    public void CallPlaySFX(int index)
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.PlaySFX(index);
        }
    }

    // 발자국/반복 효과음 (4번 반복)
    // Fungus에서 Call Method -> CallPlayFootstep(인덱스) 로 호출
    public void CallPlayFootstep(int index)
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.PlayFootstep(index);
        }
    }

    // (옵션) 펑구스에서 숫자 넣기 귀찮을 때 쓰는 '기본 발자국'용
    // Fungus에서 Call Method -> CallPlayFootstepDefault() 로 호출
    public void CallPlayFootstepDefault()
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.PlayFootstep(0); // 무조건 0번 재생
        }
    }
}