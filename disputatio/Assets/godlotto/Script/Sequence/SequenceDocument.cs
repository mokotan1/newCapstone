using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Godlotto.Sequence
{
    [Serializable]
    public sealed class SequenceDocument
    {
        public int schema_version = 1;
        public SequenceBlock[] blocks = Array.Empty<SequenceBlock>();

        public static SequenceDocument FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SequenceDocument();

            ValidateJsonShape(json);

            SequenceDocument doc;
            try
            {
                doc = JsonUtility.FromJson<SequenceDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new SequencePlayException("Could not parse sequence JSON: " + exception.Message);
            }
            if (doc == null)
                throw new SequencePlayException("Could not parse sequence JSON.");
            if (doc.blocks == null)
                doc.blocks = Array.Empty<SequenceBlock>();
            return doc;
        }

        static void ValidateJsonShape(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new SequencePlayException("Invalid sequence JSON: " + exception.Message);
            }

            ValidateOptionalType(root, "schema_version", JTokenType.Integer, "sequence document");

            JToken blocksToken = root["blocks"];
            if (blocksToken == null)
                return;
            if (blocksToken.Type != JTokenType.Array)
                throw TypeError("blocks", "sequence document", "an array");

            JArray blocks = (JArray)blocksToken;
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                if (!(blocks[blockIndex] is JObject block))
                    throw new SequencePlayException("Each sequence block must be an object.");

                string blockId = RequireString(block, "block_id", "sequence block");
                JToken commandsToken = block["commands"];
                if (commandsToken == null || commandsToken.Type != JTokenType.Array)
                    throw TypeError("commands", "block '" + blockId + "'", "an array");

                JArray commands = (JArray)commandsToken;
                for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                {
                    if (!(commands[commandIndex] is JObject command))
                    {
                        throw new SequencePlayException(
                            "Each command in block '" + blockId + "' must be an object.");
                    }

                    ValidateCommandShape(command, blockId);
                }
            }
        }

        static void ValidateCommandShape(JObject command, string blockId)
        {
            string commandName = RequireString(command, "command", "block '" + blockId + "'");
            switch (commandName)
            {
                case "set_bool":
                    RequireString(command, "key", "set_bool in block '" + blockId + "'");
                    RequireType(command, "bool_value", JTokenType.Boolean, "set_bool in block '" + blockId + "'");
                    return;
                case "set_int":
                    RequireString(command, "key", "set_int in block '" + blockId + "'");
                    RequireType(command, "int_value", JTokenType.Integer, "set_int in block '" + blockId + "'");
                    return;
                case "set_string":
                    RequireString(command, "key", "set_string in block '" + blockId + "'");
                    RequireString(command, "string_value", "set_string in block '" + blockId + "'");
                    return;
                case "if_bool":
                    RequireString(command, "key", "if_bool in block '" + blockId + "'");
                    RequireString(command, "then_block", "if_bool in block '" + blockId + "'");
                    RequireString(command, "else_block", "if_bool in block '" + blockId + "'");
                    return;
            }
        }

        static string RequireString(JObject source, string propertyName, string owner)
        {
            RequireType(source, propertyName, JTokenType.String, owner);
            return source[propertyName].Value<string>();
        }

        static void ValidateOptionalType(JObject source, string propertyName, JTokenType expectedType, string owner)
        {
            JToken token = source[propertyName];
            if (token != null && token.Type != expectedType)
                throw TypeError(propertyName, owner, expectedType.ToString().ToLowerInvariant());
        }

        static void RequireType(JObject source, string propertyName, JTokenType expectedType, string owner)
        {
            JToken token = source[propertyName];
            if (token == null || token.Type != expectedType)
                throw TypeError(propertyName, owner, expectedType.ToString().ToLowerInvariant());
        }

        static SequencePlayException TypeError(string propertyName, string owner, string expectedType)
        {
            return new SequencePlayException(
                "Property '" + propertyName + "' in " + owner + " must be " + expectedType + ".");
        }

        public bool TryGetBlock(string blockId, out SequenceBlock block)
        {
            block = null;
            if (string.IsNullOrWhiteSpace(blockId) || blocks == null)
                return false;

            for (int i = 0; i < blocks.Length; i++)
            {
                SequenceBlock candidate = blocks[i];
                if (candidate != null && string.Equals(candidate.block_id, blockId, StringComparison.Ordinal))
                {
                    block = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class SequenceBlock
    {
        public string block_id;
        public SequenceOp[] commands = Array.Empty<SequenceOp>();
    }

    [Serializable]
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
