using Fungus;
using UnityEngine;

[CommandInfo("Audio",
             "Play Random Registered SFX",
             "Plays one random SFX clip from AudioController.sfxList by index.")]
[AddComponentMenu("")]
public class PlayRandomRegisteredSfx : Command
{
    [SerializeField] private int[] sfxIndices = new int[0];

    public int[] SfxIndices => sfxIndices;

    public void SetSfxIndices(int[] indices)
    {
        sfxIndices = indices ?? new int[0];
    }

    public override void OnEnter()
    {
        if (AudioController.Instance != null && sfxIndices != null && sfxIndices.Length > 0)
        {
            int selectedIndex = sfxIndices[Random.Range(0, sfxIndices.Length)];
            AudioController.Instance.PlaySFX(selectedIndex);
        }

        Continue();
    }

    public override string GetSummary()
    {
        if (sfxIndices == null || sfxIndices.Length == 0)
            return "Random SFX: empty";

        return $"Random SFX [{string.Join(", ", sfxIndices)}]";
    }

    public override Color GetButtonColor()
    {
        return new Color32(242, 209, 176, 255);
    }
}
