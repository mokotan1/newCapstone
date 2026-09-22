using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayer
    {
        readonly FlagStore flags;
        readonly ISequenceHost host;

        public SequencePlayer(FlagStore flags)
            : this(flags, null)
        {
        }

        public SequencePlayer(FlagStore flags, ISequenceHost host)
        {
            this.flags = flags ?? throw new ArgumentNullException(nameof(flags));
            this.host = host;
        }

        public void Play(SequenceDocument document, string blockId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            SequenceValidator.Validate(document);
            if (host == null && NeedsHost(document))
            {
                throw new SequencePlayException(
                    "async_required",
                    "wait/say require a sequence host.");
            }

            PlayBlock(document, blockId, new HashSet<string>(StringComparer.Ordinal));
        }

        void PlayBlock(SequenceDocument document, string blockId, HashSet<string> stack)
        {
            if (string.IsNullOrEmpty(blockId))
                throw new SequencePlayException("unknown_block", "block_id must be non-empty.");
            if (!stack.Add(blockId))
                throw new SequencePlayException("cycle", "Cycle detected at block '" + blockId + "'.");
            if (!document.TryGetBlock(blockId, out SequenceBlock block))
                throw new SequencePlayException("unknown_block", "Unknown block_id '" + blockId + "'.");

            SequenceOp[] ops = block.commands ?? Array.Empty<SequenceOp>();
            for (int i = 0; i < ops.Length; i++)
            {
                SequenceOp op = ops[i];
                if (op == null || string.IsNullOrEmpty(op.command))
                    throw new SequencePlayException("invalid_document", "Empty command in block '" + blockId + "'.");
                Execute(document, blockId, op, stack);
            }

            stack.Remove(blockId);
        }

        void Execute(SequenceDocument document, string blockId, SequenceOp op, HashSet<string> stack)
        {
            switch (op.command)
            {
                case "set_bool":
                    flags.SetBool(op.key, op.bool_value);
                    return;
                case "set_int":
                    flags.SetInt(op.key, op.int_value);
                    return;
                case "set_string":
                    flags.SetString(op.key, op.string_value);
                    return;
                case "if_bool":
                    string next = flags.GetBool(op.key) ? op.then_block : op.else_block;
                    if (string.IsNullOrEmpty(next))
                        throw new SequencePlayException(
                            "invalid_document",
                            "if_bool in '" + blockId + "' missing then_block/else_block.");
                    PlayBlock(document, next, stack);
                    return;
                case "wait":
                    host.Wait(op.int_value);
                    return;
                case "say":
                    host.Say(op.key ?? "", op.string_value);
                    return;
                default:
                    throw new SequencePlayException("invalid_document", "Unknown command '" + op.command + "'.");
            }
        }

        static bool NeedsHost(SequenceDocument document)
        {
            SequenceBlock[] blocks = document.blocks ?? Array.Empty<SequenceBlock>();
            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock block = blocks[i];
                if (block == null || block.commands == null)
                    continue;
                for (int c = 0; c < block.commands.Length; c++)
                {
                    SequenceOp op = block.commands[c];
                    if (op == null)
                        continue;
                    if (string.Equals(op.command, "wait", StringComparison.Ordinal)
                        || string.Equals(op.command, "say", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
