#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading;
using System.Threading.Tasks;
using Fungus;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Advances Fungus Say/Writer input across real player-loop frames.
    /// Sync busy-waits cannot do this: they block Update so Writer never progresses.
    /// </summary>
    public static class DeveloperQaFungusSayPump
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

        private const string HostObjectName = "DeveloperQaFungusSayAutoAdvanceHost";

        /// <summary>
        /// Calls <see cref="Writer.OnNextLineEvent"/> on the active Say dialog when it is
        /// writing or waiting for click. Returns how many writers were advanced.
        /// </summary>
        public static int TryAdvanceActiveWriters()
        {
            int advanced = 0;
            SayDialog say = SayDialog.ActiveSayDialog;
            if (say == null)
            {
                say = SayDialog.GetSayDialog();
            }

            if (say == null || !say.gameObject.activeInHierarchy)
            {
                return 0;
            }

            Writer writer = say.GetComponentInChildren<Writer>(true);
            if (writer != null && (writer.IsWriting || writer.IsWaitingForInput))
            {
                writer.OnNextLineEvent();
                advanced++;
            }

            return advanced;
        }

        /// <summary>
        /// Polls <paramref name="predicate"/> each frame, advancing Say between yields.
        /// </summary>
        public static async Task<bool> WaitUntilAsync(
            Func<bool> predicate,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            DeveloperQaFungusSayAutoAdvanceHost host = null;
            if (Application.isPlaying)
            {
                host = EnsureAutoAdvanceHost();
                host.enabled = true;
            }

            DateTime deadlineUtc = DateTime.UtcNow + timeout;
            try
            {
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return false;
                    }

                    if (predicate())
                    {
                        return true;
                    }

                    if (DateTime.UtcNow >= deadlineUtc)
                    {
                        return false;
                    }

                    TryAdvanceActiveWriters();
                    await Task.Yield();
                }
            }
            finally
            {
                if (host != null)
                {
                    host.enabled = false;
                }
            }
        }

        private static DeveloperQaFungusSayAutoAdvanceHost EnsureAutoAdvanceHost()
        {
            DeveloperQaFungusSayAutoAdvanceHost existing =
                UnityEngine.Object.FindFirstObjectByType<DeveloperQaFungusSayAutoAdvanceHost>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(HostObjectName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<DeveloperQaFungusSayAutoAdvanceHost>();
        }
    }

    /// <summary>
    /// Play Mode host that advances Fungus Say every Update while enabled.
    /// Complements <see cref="DeveloperQaFungusSayPump.WaitUntilAsync"/> frame yields.
    /// </summary>
    public sealed class DeveloperQaFungusSayAutoAdvanceHost : MonoBehaviour
    {
        private void Update()
        {
            DeveloperQaFungusSayPump.TryAdvanceActiveWriters();
        }
    }
}
#endif
