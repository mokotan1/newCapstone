using System;
using System.Threading;
using System.Threading.Tasks;

namespace Godlotto.Sequence
{
    public sealed class SequenceSession
    {
        readonly SequencePlayer player;
        readonly ISequenceInputLock input;
        int inputHoldCount;

        public SequenceSession(FlagStore flags, ISequenceHost host, ISequenceInputLock input)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            player = new SequencePlayer(flags, host);
        }

        public void Play(SequenceDocument document, string blockId)
        {
            using (var cts = new CancellationTokenSource())
            {
                Task play = PlayAsync(document, blockId, cts.Token);
                if (!play.IsCompleted)
                {
                    cts.Cancel();
                    // Sync API must not leave the gate held if cancel completion is deferred.
                    ReleaseInputLock();
                    throw new SequencePlayException(
                        "async_required",
                        "wait/say require awaiting PlayAsync.");
                }

                play.GetAwaiter().GetResult();
            }
        }

        public async Task PlayAsync(
            SequenceDocument document,
            string blockId,
            CancellationToken cancellationToken = default)
        {
            SequenceValidator.Validate(document);
            AcquireInputLock();
            try
            {
                await player.PlayAsync(document, blockId, cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                ReleaseInputLock();
            }
        }

        void AcquireInputLock()
        {
            if (inputHoldCount++ == 0)
                input.Block(SequenceLimits.InputLockReason);
        }

        void ReleaseInputLock()
        {
            if (inputHoldCount <= 0)
                return;
            if (--inputHoldCount == 0)
                input.Unblock(SequenceLimits.InputLockReason);
        }
    }
}
