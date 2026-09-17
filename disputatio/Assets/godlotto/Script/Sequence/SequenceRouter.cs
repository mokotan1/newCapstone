using System;

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
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new SequencePlayException("empty_key", "Interaction id must be non-empty.");

            SequenceRoute route;
            if (!catalog.TryGet(interactionId, out route))
            {
                throw new SequencePlayException(
                    "unknown_route",
                    "Unknown interaction id '" + interactionId + "'.");
            }

            session.Play(route.Document, route.StartBlock);
        }
    }
}
