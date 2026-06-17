using Fungus;
using UnityEngine;

[CommandInfo("Audio",
             "Play Registered SFX",
             "Plays an SFX clip from SfxController.sfxList by index.")]
[AddComponentMenu("")]
public class PlayRegisteredSfx : Command
{
    [SerializeField] private int sfxIndex;

    public int SfxIndex => sfxIndex;

    public void SetSfxIndex(int index)
    {
        sfxIndex = Mathf.Max(0, index);
    }

    public override void OnEnter()
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(sfxIndex);

        Continue();
    }

    public override string GetSummary()
    {
        return $"SFX Index {sfxIndex}";
    }

    public override Color GetButtonColor()
    {
        return new Color32(242, 209, 176, 255);
    }
}
