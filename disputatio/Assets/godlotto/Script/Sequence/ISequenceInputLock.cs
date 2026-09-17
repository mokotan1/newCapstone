namespace Godlotto.Sequence
{
    public interface ISequenceInputLock
    {
        void Block(string reason);

        void Unblock(string reason);
    }
}
