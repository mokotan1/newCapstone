using UnityEngine;

public class AudioBridge : MonoBehaviour
{
    // 펑구스에서 이 함수를 부르면 -> 1층에 있는 본체에게 연락함
    public void CallPlayBGM(int index)
    {
        // AudioController의 싱글톤 인스턴스가 살아있는지 확인
        if (AudioController.instance != null)
        {
            AudioController.instance.PlayBGM(index);
        }
        else
        {
            Debug.LogWarning("BGM 본체를 찾을 수 없습니다! (1층 씬에서 시작했나요?)");
        }
    }

    public void CallStopMusic()
    {
        if (AudioController.instance != null)
        {
            AudioController.instance.StopMusic();
        }
    }
}