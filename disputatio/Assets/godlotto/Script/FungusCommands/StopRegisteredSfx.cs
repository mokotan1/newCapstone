using Fungus;
using UnityEngine;

[CommandInfo("Audio",
             "Stop Registered SFX",
             "Stops active SFX instances started from AudioController.sfxList.")]
[AddComponentMenu("")]
public class StopRegisteredSfx : Command
{
    [SerializeField] private int sfxIndex;
    [SerializeField] private bool stopAllSfx;

    public int SfxIndex => sfxIndex;
    public bool StopAllSfx => stopAllSfx;

    public void SetStopTarget(int index, bool stopAll)
    {
        sfxIndex = index;
        stopAllSfx = stopAll;
    }

    public override void OnEnter()
    {
        if (AudioController.Instance != null)
        {
            if (stopAllSfx)
                AudioController.Instance.StopAllSFX();
            else
                AudioController.Instance.StopSFX(sfxIndex);
        }

        Continue();
    }

    public override string GetSummary()
    {
        return stopAllSfx ? "Stop All SFX" : $"Stop SFX Index {sfxIndex}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(242, 209, 176, 255);
    }
}
