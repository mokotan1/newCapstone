using System;

namespace Godlotto.Sequence
{
    public sealed class SequenceDocument
    {
        public int schemaVersion;
        public SequenceBlock[] blocks;

        public bool TryGetBlock(string id, out SequenceBlock block)
        {
            block = null;
            if (string.IsNullOrEmpty(id) || blocks == null)
                return false;
            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock candidate = blocks[i];
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                {
                    block = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class SequenceBlock
    {
        public string id;
        public SequenceOp[] commands;
    }

    public sealed class SequenceOp
    {
        public string command;
        public string key;
        public bool bool_value;
        public int int_value;
        public string string_value;
        public string then_block;
        public string else_block;
    }
}
