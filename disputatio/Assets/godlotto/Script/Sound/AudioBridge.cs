using Fungus;
using UnityEngine;

public class AudioBridge : MonoBehaviour
{
    public void CallPlayBGM(int index)
    {
        if (AudioController.Instance != null)
            AudioController.Instance.PlayBGM(index);
    }

    public void CallStopMusic()
    {
        if (AudioController.Instance != null)
            AudioController.Instance.StopMusic();
    }

    public void CallPlaySFX(int index)
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(index);
    }

    public void CallPlayFootstep(int index)
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlayFootstep(index);
    }

    public void CallPlayFootstepDefault()
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlayFootstep(0);
    }
}
