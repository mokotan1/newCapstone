using System;

namespace Godlotto.Sequence
{
    public sealed class FlagSnapshot
    {
        public FlagBoolEntry[] Bools = Array.Empty<FlagBoolEntry>();
        public FlagIntEntry[] Ints = Array.Empty<FlagIntEntry>();
        public FlagStringEntry[] Strings = Array.Empty<FlagStringEntry>();
    }

    public struct FlagBoolEntry
    {
        public string Key;
        public bool Value;

        public FlagBoolEntry(string key, bool value)
        {
            Key = key;
            Value = value;
        }
    }

    public struct FlagIntEntry
    {
        public string Key;
        public int Value;

        public FlagIntEntry(string key, int value)
        {
            Key = key;
            Value = value;
        }
    }

    public struct FlagStringEntry
    {
        public string Key;
        public string Value;

        public FlagStringEntry(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
