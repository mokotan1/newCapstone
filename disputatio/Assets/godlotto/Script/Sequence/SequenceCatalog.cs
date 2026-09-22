using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class SequenceCatalog
    {
        readonly Dictionary<string, SequenceRoute> routes =
            new Dictionary<string, SequenceRoute>(StringComparer.Ordinal);

        public void Register(string interactionId, SequenceDocument document, string startBlock)
        {
            RequireInteractionId(interactionId);
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            SequenceValidator.Validate(document);
            if (string.IsNullOrEmpty(startBlock) || !document.TryGetBlock(startBlock, out _))
            {
                throw new SequencePlayException(
                    "unknown_block",
                    "Unknown start block '" + startBlock + "'.");
            }

            if (routes.ContainsKey(interactionId))
            {
                throw new SequencePlayException(
                    "invalid_document",
                    "Duplicate interaction id '" + interactionId + "'.");
            }

            routes[interactionId] = new SequenceRoute(document, startBlock);
        }

        public bool TryGet(string interactionId, out SequenceRoute route)
        {
            route = null;
            if (string.IsNullOrWhiteSpace(interactionId))
                return false;
            return routes.TryGetValue(interactionId, out route);
        }

        static void RequireInteractionId(string interactionId)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
                throw new SequencePlayException("empty_key", "Interaction id must be non-empty.");
        }
    }

    public sealed class SequenceRoute
    {
        public SequenceDocument Document { get; }
        public string StartBlock { get; }

        public SequenceRoute(SequenceDocument document, string startBlock)
        {
            Document = document;
            StartBlock = startBlock;
        }
    }
}
