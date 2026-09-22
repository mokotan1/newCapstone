namespace Godlotto.Sequence
{
    public interface ISequenceHost
    {
        void Wait(int milliseconds);

        void Say(string speaker, string line);
    }
}
