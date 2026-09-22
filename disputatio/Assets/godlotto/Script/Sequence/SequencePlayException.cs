using System;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayException : Exception
    {
        public SequencePlayException(string message) : base(message)
        {
        }
    }
}
