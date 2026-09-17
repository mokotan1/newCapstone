using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public static class SequenceValidator
    {
        static readonly HashSet<string> AllowedCommands = new HashSet<string>(StringComparer.Ordinal)
        {
            "set_bool",
            "set_int",
            "set_string",
            "if_bool"
        };

        public static void Validate(SequenceDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (document.schemaVersion != SequenceLimits.CurrentSchemaVersion)
            {
                throw new SequencePlayException(
                    "invalid_document",
                    "schemaVersion must be " + SequenceLimits.CurrentSchemaVersion + ".");
            }

            SequenceBlock[] blocks = document.blocks ?? Array.Empty<SequenceBlock>();
            if (blocks.Length > SequenceLimits.MaxBlockCount)
            {
                throw new SequencePlayException("invalid_document", "Too many blocks.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            int commandCount = 0;
            var ifEdges = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock block = blocks[i];
                if (block == null || string.IsNullOrEmpty(block.id))
                    throw new SequencePlayException("invalid_document", "Block id must be non-empty.");
                if (!ids.Add(block.id))
                    throw new SequencePlayException("invalid_document", "Duplicate block_id '" + block.id + "'.");
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock block = blocks[i];
                SequenceOp[] ops = block.commands ?? Array.Empty<SequenceOp>();
                commandCount += ops.Length;
                if (commandCount > SequenceLimits.MaxCommandsPerDocument)
                    throw new SequencePlayException("invalid_document", "Too many commands.");

                for (int c = 0; c < ops.Length; c++)
                {
                    SequenceOp op = ops[c];
                    if (op == null || string.IsNullOrEmpty(op.command))
                    {
                        throw new SequencePlayException(
                            "invalid_document",
                            "Empty command in block '" + block.id + "'.");
                    }

                    if (!AllowedCommands.Contains(op.command))
                    {
                        throw new SequencePlayException(
                            "invalid_document",
                            "Unknown command '" + op.command + "'.");
                    }

                    ValidateOp(block.id, op, ids, ifEdges);
                }
            }

            AssertNoCycle(ifEdges);
        }

        static void ValidateOp(
            string blockId,
            SequenceOp op,
            HashSet<string> ids,
            Dictionary<string, List<string>> ifEdges)
        {
            switch (op.command)
            {
                case "set_bool":
                case "set_int":
                case "set_string":
                    if (string.IsNullOrWhiteSpace(op.key))
                    {
                        throw new SequencePlayException(
                            "invalid_document",
                            op.command + " in '" + blockId + "' missing key.");
                    }

                    return;
                case "if_bool":
                    if (string.IsNullOrWhiteSpace(op.key)
                        || string.IsNullOrEmpty(op.then_block)
                        || string.IsNullOrEmpty(op.else_block))
                    {
                        throw new SequencePlayException(
                            "invalid_document",
                            "if_bool in '" + blockId + "' missing key/then_block/else_block.");
                    }

                    RequireBlock(ids, op.then_block);
                    RequireBlock(ids, op.else_block);
                    AddEdge(ifEdges, blockId, op.then_block);
                    AddEdge(ifEdges, blockId, op.else_block);
                    return;
                default:
                    throw new SequencePlayException("invalid_document", "Unknown command '" + op.command + "'.");
            }
        }

        static void RequireBlock(HashSet<string> ids, string blockId)
        {
            if (!ids.Contains(blockId))
            {
                throw new SequencePlayException(
                    "invalid_document",
                    "Unknown block_id '" + blockId + "'.");
            }
        }

        static void AddEdge(Dictionary<string, List<string>> edges, string from, string to)
        {
            List<string> list;
            if (!edges.TryGetValue(from, out list))
            {
                list = new List<string>();
                edges[from] = list;
            }

            list.Add(to);
        }

        static void AssertNoCycle(Dictionary<string, List<string>> edges)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string node in edges.Keys)
                Dfs(node, edges, visiting, visited, 0);
        }

        static void Dfs(
            string node,
            Dictionary<string, List<string>> edges,
            HashSet<string> visiting,
            HashSet<string> visited,
            int depth)
        {
            if (depth > SequenceLimits.MaxIfDepth)
                throw new SequencePlayException("invalid_document", "if_bool depth exceeded.");
            if (visited.Contains(node))
                return;
            if (!visiting.Add(node))
                throw new SequencePlayException("cycle", "Cycle detected at block '" + node + "'.");

            List<string> next;
            if (edges.TryGetValue(node, out next))
            {
                for (int i = 0; i < next.Count; i++)
                    Dfs(next[i], edges, visiting, visited, depth + 1);
            }

            visiting.Remove(node);
            visited.Add(node);
        }
    }
}
