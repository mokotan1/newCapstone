using System;

namespace Godlotto.Sequence
{
    public sealed class SequenceSession
    {
        readonly SequencePlayer player;
        readonly ISequenceInputLock input;

        public SequenceSession(FlagStore flags, ISequenceHost host, ISequenceInputLock input)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            player = new SequencePlayer(flags, host);
        }

        public void Play(SequenceDocument document, string blockId)
        {
            SequenceValidator.Validate(document);
            input.Block(SequenceLimits.InputLockReason);
            try
            {
                player.Play(document, blockId);
            }
            finally
            {
                input.Unblock(SequenceLimits.InputLockReason);
            }
        }
    }
}
