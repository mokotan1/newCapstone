using System;
using Fungus;
using UnityEngine;

public class AudioBridge : MonoBehaviour
{
    public void CallPlayBGM(int index)
    {
        if (AudioController.instance != null)
            AudioController.instance.PlayBGM(index);
    }

    public void CallStopMusic()
    {
        WithControllerSilent(c => c.StopMusic());
    }

    public void CallPlaySFX(int index)
    {
        WithControllerSilent(c => c.PlaySFX(index));
    }

    public void CallPlayFootstep(int index)
    {
        WithControllerSilent(c => c.PlayFootstep(index));
    }

    public void CallPlayFootstepDefault()
    {
        WithControllerSilent(c => c.PlayFootstep(0));
    }

    private static void WithControllerSilent(Action<AudioController> action)
    {
        if (AudioController.instance != null)
            action(AudioController.instance);
    }
}
