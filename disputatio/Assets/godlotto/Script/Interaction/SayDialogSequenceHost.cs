using Fungus;
using Godlotto.Sequence;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Sequence say 명령을 Fungus SayDialog에 출력합니다. wait는 프레임 블로킹 없이 즉시 반환합니다.
    /// </summary>
    public sealed class SayDialogSequenceHost : MonoBehaviour, ISequenceHost
    {
        [SerializeField] SayDialog sayDialog;

        public void Wait(int milliseconds)
        {
            if (milliseconds > 0 && EnableDebugLogging)
            {
                GameLog.Log(
                    "[SayDialogSequenceHost] wait " + milliseconds
                    + "ms skipped (sync host; use coroutine runner later).");
            }
        }

        public void Say(string speaker, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            SayDialog dialog = ResolveSayDialog();
            if (dialog == null)
            {
                GameLog.LogWarning("[SayDialogSequenceHost] SayDialog not found.");
                return;
            }

            SayDialog.ActiveSayDialog = dialog;
            string name = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim();
            dialog.SetCharacterName(name, Color.white);
            dialog.Say(line, clearPrevious: true, waitForInput: false, fadeWhenDone: false, stopVoiceover: true, waitForVO: false, voiceOverClip: null, onComplete: null);
        }

        SayDialog ResolveSayDialog()
        {
            if (sayDialog != null)
                return sayDialog;

            SayDialog active = SayDialog.ActiveSayDialog;
            if (active != null)
                return active;

            SayDialog found = FindFirstObjectByType<SayDialog>();
            return found;
        }

        public static bool EnableDebugLogging { get; set; }
    }
}
