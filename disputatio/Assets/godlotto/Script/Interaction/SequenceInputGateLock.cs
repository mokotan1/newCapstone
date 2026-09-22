using Godlotto.Sequence;

namespace Godlotto.Interaction
{
    public sealed class SequenceInputGateLock : ISequenceInputLock
    {
        public void Block(string reason)
        {
            InteractionInputGate.Block(reason);
        }

        public void Unblock(string reason)
        {
            InteractionInputGate.Unblock(reason);
        }
    }
}
