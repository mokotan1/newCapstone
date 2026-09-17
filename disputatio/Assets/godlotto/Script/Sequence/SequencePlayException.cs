using System;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayException : Exception
    {
        public string Code { get; }

        public SequencePlayException(string message)
            : this("sequence_error", message)
        {
        }

        public SequencePlayException(string code, string message)
            : base(message)
        {
            Code = code ?? "sequence_error";
        }
    }
}
