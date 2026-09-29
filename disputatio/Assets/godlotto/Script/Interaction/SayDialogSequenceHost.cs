using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Fungus;
using Godlotto.Sequence;
using UnityEngine;
using AsyncTask = System.Threading.Tasks.Task;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Sequence say/wait를 Fungus SayDialog와 프레임 대기에 연결합니다.
    /// say는 플레이어 입력이 끝나기 전까지 다음 명령으로 넘어가지 않습니다.
    /// </summary>
    public sealed class SayDialogSequenceHost : MonoBehaviour, ISequenceHost
    {
        [SerializeField] SayDialog sayDialog;

        public AsyncTask WaitAsync(int milliseconds, CancellationToken cancellationToken)
        {
            if (milliseconds <= 0)
                return AsyncTask.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            Coroutine routine = StartCoroutine(WaitRoutine(milliseconds, tcs));
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (routine != null)
                        StopCoroutine(routine);
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            return tcs.Task;
        }

        public AsyncTask SayAsync(string speaker, string line, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(line))
                return AsyncTask.CompletedTask;

            SayDialog dialog = ResolveSayDialog();
            if (dialog == null)
            {
                GameLog.LogWarning("[SayDialogSequenceHost] SayDialog not found.");
                return AsyncTask.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            SayDialog.ActiveSayDialog = dialog;
            string name = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.Trim();
            dialog.SetCharacterName(name, Color.white);
            dialog.Say(
                line,
                clearPrevious: true,
                waitForInput: true,
                fadeWhenDone: false,
                stopVoiceover: true,
                waitForVO: false,
                voiceOverClip: null,
                onComplete: () => tcs.TrySetResult(true));

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            return tcs.Task;
        }

        IEnumerator WaitRoutine(int milliseconds, TaskCompletionSource<bool> tcs)
        {
            yield return new WaitForSecondsRealtime(milliseconds / 1000f);
            tcs.TrySetResult(true);
        }

        SayDialog ResolveSayDialog()
        {
            if (sayDialog != null)
                return sayDialog;

            SayDialog active = SayDialog.ActiveSayDialog;
            if (active != null)
                return active;

            return FindFirstObjectByType<SayDialog>();
        }

        public static bool EnableDebugLogging { get; set; }
    }
}
