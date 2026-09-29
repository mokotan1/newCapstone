using System;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.Sequence
{
    public sealed class SequenceRouter
    {
        readonly SequenceCatalog catalog;
        readonly SequenceSession session;

        public SequenceRouter(
            SequenceCatalog catalog,
            FlagStore flags,
            ISequenceHost host,
            ISequenceInputLock input)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            session = new SequenceSession(flags, host, input);
        }

        public void Play(string interactionId)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task play = PlayAsync(interactionId, cts.Token);
                if (!play.IsCompleted)
                {
                    cts.Cancel();
                    throw new SequencePlayException(
                        "async_required",
                        "wait/say require awaiting PlayAsync.");
                }

                play.GetAwaiter().GetResult();
            }
        }

        public Task PlayAsync(string interactionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new SequencePlayException("empty_key", "Interaction id must be non-empty.");

            SequenceRoute route;
            if (!catalog.TryGet(interactionId, out route))
            {
                throw new SequencePlayException(
                    "unknown_route",
                    "Unknown interaction id '" + interactionId + "'.");
            }

            return session.PlayAsync(route.Document, route.StartBlock, cancellationToken);
        }
    }
}
