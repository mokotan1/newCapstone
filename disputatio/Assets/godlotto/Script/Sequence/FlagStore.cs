using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class FlagStore
    {
        enum FlagKind
        {
            Bool,
            Int,
            String
        }

        readonly Dictionary<string, FlagKind> kinds = new Dictionary<string, FlagKind>();
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();

        public bool Has(string key)
        {
            RequireKey(key);
            return kinds.ContainsKey(key);
        }

        public bool GetBool(string key)
        {
            RequireKind(key, FlagKind.Bool);
            return bools[key];
        }

        public void SetBool(string key, bool value)
        {
            Bind(key, FlagKind.Bool);
            bools[key] = value;
        }

        public int GetInt(string key)
        {
            RequireKind(key, FlagKind.Int);
            return ints[key];
        }

        public void SetInt(string key, int value)
        {
            Bind(key, FlagKind.Int);
            ints[key] = value;
        }

        public string GetString(string key)
        {
            RequireKind(key, FlagKind.String);
            return strings[key];
        }

        public void SetString(string key, string value)
        {
            Bind(key, FlagKind.String);
            strings[key] = value ?? "";
        }

        void Bind(string key, FlagKind kind)
        {
            RequireKey(key);
            FlagKind existing;
            if (kinds.TryGetValue(key, out existing) && existing != kind)
            {
                throw new SequencePlayException(
                    "type_mismatch",
                    "Flag '" + key + "' is " + existing + ", not " + kind + ".");
            }

            kinds[key] = kind;
        }

        void RequireKind(string key, FlagKind kind)
        {
            RequireKey(key);
            FlagKind existing;
            if (!kinds.TryGetValue(key, out existing))
            {
                throw new SequencePlayException("missing_key", "Flag '" + key + "' is not set.");
            }

            if (existing != kind)
            {
                throw new SequencePlayException(
                    "type_mismatch",
                    "Flag '" + key + "' is " + existing + ", not " + kind + ".");
            }
        }

        static void RequireKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new SequencePlayException("empty_key", "Flag key must be non-empty.");
        }
    }
}
