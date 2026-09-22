using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class SequencePlayer
    {
        const int SupportedSchemaVersion = 1;
        const int MaximumBranchDepth = 64;
        const int MaximumCommandCount = 1024;

        readonly FlagStore flags;

        public SequencePlayer(FlagStore flags)
        {
            this.flags = flags ?? throw new ArgumentNullException(nameof(flags));
        }

        public void Play(SequenceDocument document, string blockId)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            ValidateDocument(document);
            PlayBlock(document, blockId, new HashSet<string>(StringComparer.Ordinal));
        }

        static void ValidateDocument(SequenceDocument document)
        {
            if (document.schema_version != SupportedSchemaVersion)
            {
                throw new SequencePlayException(
                    "Unsupported schema_version '" + document.schema_version +
                    "'. Expected '" + SupportedSchemaVersion + "'.");
            }

            SequenceBlock[] blocks = document.blocks ?? Array.Empty<SequenceBlock>();
            var blocksById = new Dictionary<string, SequenceBlock>(StringComparer.Ordinal);
            var branchesByBlock = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var flagTypesByKey = new Dictionary<string, SequenceValueType>(StringComparer.Ordinal);
            int commandCount = 0;

            for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
            {
                SequenceBlock block = blocks[blockIndex];
                if (block == null || string.IsNullOrWhiteSpace(block.block_id))
                    throw new SequencePlayException("Every sequence block must have a non-empty block_id.");
                if (!blocksById.TryAdd(block.block_id, block))
                    throw new SequencePlayException("Duplicate block_id '" + block.block_id + "'.");

                SequenceOp[] commands = block.commands;
                if (commands == null)
                    throw new SequencePlayException("Block '" + block.block_id + "' is missing commands.");

                commandCount += commands.Length;
                if (commandCount > MaximumCommandCount)
                {
                    throw new SequencePlayException(
                        "Sequence has more than " + MaximumCommandCount + " commands.");
                }

                var branches = new List<string>();
                branchesByBlock.Add(block.block_id, branches);
                for (int commandIndex = 0; commandIndex < commands.Length; commandIndex++)
                {
                    SequenceOp command = commands[commandIndex];
                    ValidateCommand(block.block_id, command, branches, flagTypesByKey);
                }
            }

            foreach (KeyValuePair<string, List<string>> entry in branchesByBlock)
            {
                for (int branchIndex = 0; branchIndex < entry.Value.Count; branchIndex++)
                {
                    string targetBlockId = entry.Value[branchIndex];
                    if (!blocksById.ContainsKey(targetBlockId))
                    {
                        throw new SequencePlayException(
                            "Block '" + entry.Key + "' references missing block_id '" + targetBlockId + "'.");
                    }
                }
            }

            ValidateBranchGraph(branchesByBlock);
        }

        static void ValidateCommand(
            string blockId,
            SequenceOp command,
            List<string> branches,
            Dictionary<string, SequenceValueType> flagTypesByKey)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.command))
                throw new SequencePlayException("Block '" + blockId + "' contains an empty command.");

            switch (command.command)
            {
                case "set_bool":
                    RequireKey(blockId, command);
                    RegisterFlagType(command.key, SequenceValueType.Bool, flagTypesByKey, blockId, command.command);
                    return;
                case "set_int":
                    RequireKey(blockId, command);
                    RegisterFlagType(command.key, SequenceValueType.Int, flagTypesByKey, blockId, command.command);
                    return;
                case "set_string":
                    RequireKey(blockId, command);
                    RegisterFlagType(command.key, SequenceValueType.String, flagTypesByKey, blockId, command.command);
                    if (command.string_value == null)
                    {
                        throw new SequencePlayException(
                            "set_string in block '" + blockId + "' is missing string_value.");
                    }
                    return;
                case "if_bool":
                    RequireKey(blockId, command);
                    RegisterFlagType(command.key, SequenceValueType.Bool, flagTypesByKey, blockId, command.command);
                    if (string.IsNullOrWhiteSpace(command.then_block) ||
                        string.IsNullOrWhiteSpace(command.else_block))
                    {
                        throw new SequencePlayException(
                            "if_bool in block '" + blockId + "' requires then_block and else_block.");
                    }

                    branches.Add(command.then_block);
                    branches.Add(command.else_block);
                    return;
                default:
                    throw new SequencePlayException(
                        "Unknown command '" + command.command + "' in block '" + blockId + "'.");
            }
        }

        static void RegisterFlagType(
            string key,
            SequenceValueType requestedType,
            Dictionary<string, SequenceValueType> flagTypesByKey,
            string blockId,
            string commandName)
        {
            if (flagTypesByKey.TryGetValue(key, out SequenceValueType existingType))
            {
                if (existingType != requestedType)
                {
                    throw new SequencePlayException(
                        "Command '" + commandName + "' in block '" + blockId + "' uses key '" + key +
                        "' as " + requestedType + " after it was used as " + existingType + ".");
                }

                return;
            }

            flagTypesByKey.Add(key, requestedType);
        }

        static void RequireKey(string blockId, SequenceOp command)
        {
            if (string.IsNullOrWhiteSpace(command.key))
            {
                throw new SequencePlayException(
                    "Command '" + command.command + "' in block '" + blockId + "' requires a non-empty key.");
            }
        }

        static void ValidateBranchGraph(Dictionary<string, List<string>> branchesByBlock)
        {
            var completed = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (string blockId in branchesByBlock.Keys)
            {
                DetectCycle(blockId, branchesByBlock, completed, visiting);
            }

            var maximumDepthByBlock = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string blockId in branchesByBlock.Keys)
            {
                int maximumDepth = CalculateMaximumDepth(blockId, branchesByBlock, maximumDepthByBlock);
                if (maximumDepth > MaximumBranchDepth)
                {
                    throw new SequencePlayException(
                        "Sequence branch depth exceeds " + MaximumBranchDepth + " at block '" + blockId + "'.");
                }
            }
        }

        static void DetectCycle(
            string blockId,
            Dictionary<string, List<string>> branchesByBlock,
            HashSet<string> completed,
            HashSet<string> visiting)
        {
            if (completed.Contains(blockId))
                return;
            if (!visiting.Add(blockId))
                throw new SequencePlayException("Cycle detected at block '" + blockId + "'.");

            List<string> branches = branchesByBlock[blockId];
            for (int index = 0; index < branches.Count; index++)
            {
                DetectCycle(branches[index], branchesByBlock, completed, visiting);
            }

            visiting.Remove(blockId);
            completed.Add(blockId);
        }

        static int CalculateMaximumDepth(
            string blockId,
            Dictionary<string, List<string>> branchesByBlock,
            Dictionary<string, int> maximumDepthByBlock)
        {
            if (maximumDepthByBlock.TryGetValue(blockId, out int cachedDepth))
                return cachedDepth;

            int maximumDepth = 1;
            List<string> branches = branchesByBlock[blockId];
            for (int index = 0; index < branches.Count; index++)
            {
                maximumDepth = Math.Max(
                    maximumDepth,
                    1 + CalculateMaximumDepth(branches[index], branchesByBlock, maximumDepthByBlock));
            }

            maximumDepthByBlock.Add(blockId, maximumDepth);
            return maximumDepth;
        }

        enum SequenceValueType
        {
            Bool,
            Int,
            String
        }

        void PlayBlock(SequenceDocument document, string blockId, HashSet<string> stack)
        {
            if (string.IsNullOrEmpty(blockId))
                throw new SequencePlayException("block_id must be non-empty.");
            if (!stack.Add(blockId))
                throw new SequencePlayException("Cycle detected at block '" + blockId + "'.");
            if (!document.TryGetBlock(blockId, out SequenceBlock block))
                throw new SequencePlayException("Unknown block_id '" + blockId + "'.");

            SequenceOp[] ops = block.commands ?? Array.Empty<SequenceOp>();
            for (int i = 0; i < ops.Length; i++)
            {
                SequenceOp op = ops[i];
                if (op == null || string.IsNullOrEmpty(op.command))
                    throw new SequencePlayException("Empty command in block '" + blockId + "'.");
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
                            "if_bool in '" + blockId + "' missing then_block/else_block.");
                    PlayBlock(document, next, stack);
                    return;
                default:
                    throw new SequencePlayException(
                        "Unknown command '" + op.command + "' in block '" + blockId + "'.");
            }
        }
    }
}
