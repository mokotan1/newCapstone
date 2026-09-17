using Godlotto.Sequence;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 씬 로컬 FlagStore와 Sequence 라우터. 전역 싱글톤이 아닙니다.
    /// </summary>
    public sealed class RoomInteractionSequenceHost : MonoBehaviour
    {
        [SerializeField] SayDialogSequenceHost sayHost;
        [SerializeField] RoomInteractionController controller;

        readonly FlagStore flags = new FlagStore();
        readonly SequenceCatalog catalog = new SequenceCatalog();
        SequenceRouter router;
        bool catalogBuilt;

        public FlagStore Flags => flags;

        void Awake()
        {
            EnsureRouter();
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
                router.Play(interactionId);
                if (controller != null)
                    controller.ApplySequenceOutcomes(flags);
                return true;
            }
            catch (SequencePlayException ex) when (ex.Code == "unknown_route")
            {
                return false;
            }
        }

        public void RegisterForTests(string interactionId, SequenceDocument document, string startBlock)
        {
            catalog.Register(interactionId, document, startBlock);
            catalogBuilt = true;
            EnsureRouter();
        }

        internal static void ResetForTests()
        {
            SequencePlayHandlerForTests = null;
        }

        internal static System.Func<string, bool> SequencePlayHandlerForTests;

        void EnsureRouter()
        {
            if (router != null)
                return;

            ISequenceHost host = sayHost != null ? sayHost : new NullSequenceHost();
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
            public void Wait(int milliseconds)
            {
            }

            public void Say(string speaker, string line)
            {
            }
        }
    }
}
