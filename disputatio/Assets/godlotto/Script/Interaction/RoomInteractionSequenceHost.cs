using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.Sequence;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 씬 로컬 FlagStore와 Sequence 라우터. 전역 싱글톤이 아닙니다.
    /// wait/say가 있으면 재생을 비동기로 이어가고, 완료 시에만 outcome을 한 번 적용합니다.
    /// </summary>
    public sealed class RoomInteractionSequenceHost : MonoBehaviour
    {
        [SerializeField] SayDialogSequenceHost sayHost;
        [SerializeField] RoomInteractionController controller;

        readonly FlagStore flags = new FlagStore();
        readonly SequenceCatalog catalog = new SequenceCatalog();
        SequenceRouter router;
        bool catalogBuilt;
        CancellationTokenSource playCts;
        Task pendingPlay;
        CancellationToken pendingToken;
        int outcomesAppliedCount;

        public FlagStore Flags => flags;

        public int OutcomesAppliedCountForTests => outcomesAppliedCount;

        void Awake()
        {
            EnsureRouter();
        }

        void OnDisable()
        {
            CancelActivePlay();
        }

        public void RebuildCatalogFromController()
        {
            catalogBuilt = false;
            EnsureCatalog();
        }

        public bool TryPlay(string interactionId)
        {
            EnsureCatalog();
            try
            {
                CancelActivePlay();
                playCts = new CancellationTokenSource();
                CancellationToken token = playCts.Token;
                Task play = router.PlayAsync(interactionId, token);
                if (play.IsCompleted)
                {
                    return FinishCompletedPlay(play, applyOutcomes: true);
                }

                ObservePlay(play, token);
                return true;
            }
            catch (SequencePlayException ex) when (ex.Code == "unknown_route")
            {
                return false;
            }
        }

        public void RegisterForTests(string interactionId, SequenceDocument document, string startBlock)
        {
            router = null;
            catalog.Register(interactionId, document, startBlock);
            catalogBuilt = true;
            EnsureRouter();
        }

        /// <summary>
        /// EditMode does not pump coroutines; call after a gated host completes.
        /// </summary>
        internal void PumpPendingPlayForTests()
        {
            if (pendingPlay == null || !pendingPlay.IsCompleted)
                return;
            if (pendingToken.IsCancellationRequested)
            {
                pendingPlay = null;
                return;
            }

            FinishCompletedPlay(pendingPlay, applyOutcomes: true);
            pendingPlay = null;
        }

        internal static void ResetForTests()
        {
            SequencePlayHandlerForTests = null;
            HostForTests = null;
        }

        internal static System.Func<string, bool> SequencePlayHandlerForTests;

        internal static ISequenceHost HostForTests;

        void ObservePlay(Task play, CancellationToken token)
        {
            pendingPlay = play;
            pendingToken = token;
            StartCoroutine(AwaitPlayOnMainThread(play, token));
        }

        IEnumerator AwaitPlayOnMainThread(Task play, CancellationToken token)
        {
            while (!play.IsCompleted)
                yield return null;

            if (token.IsCancellationRequested)
            {
                if (ReferenceEquals(pendingPlay, play))
                    pendingPlay = null;
                yield break;
            }

            FinishCompletedPlay(play, applyOutcomes: true);
            if (ReferenceEquals(pendingPlay, play))
                pendingPlay = null;
        }

        bool FinishCompletedPlay(Task play, bool applyOutcomes)
        {
            if (play.IsCanceled)
                return false;

            if (play.IsFaulted)
            {
                if (play.Exception != null)
                {
                    foreach (var inner in play.Exception.InnerExceptions)
                    {
                        if (inner is SequencePlayException sequenceEx && sequenceEx.Code == "unknown_route")
                            return false;
                    }
                }

                play.GetAwaiter().GetResult();
            }

            if (applyOutcomes && controller != null)
            {
                controller.ApplySequenceOutcomes(flags);
                outcomesAppliedCount++;
            }

            return true;
        }

        void CancelActivePlay()
        {
            if (playCts == null)
                return;

            playCts.Cancel();
            playCts.Dispose();
            playCts = null;
            pendingPlay = null;
        }

        void EnsureRouter()
        {
            if (router != null)
                return;

            ISequenceHost host = HostForTests
                ?? (sayHost != null ? (ISequenceHost)sayHost : new NullSequenceHost());
            router = new SequenceRouter(catalog, flags, host, new SequenceInputGateLock());
        }

        void EnsureCatalog()
        {
            if (catalogBuilt)
                return;

            if (SequencePlayHandlerForTests != null)
            {
                catalogBuilt = true;
                return;
            }

            if (controller == null)
                controller = GetComponent<RoomInteractionController>();

            if (controller != null)
                controller.RegisterSequenceRoutes(catalog);

            catalogBuilt = true;
        }

        sealed class NullSequenceHost : ISequenceHost
        {
            public Task WaitAsync(int milliseconds, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task SayAsync(string speaker, string line, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
