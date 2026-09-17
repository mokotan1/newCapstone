using System;
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

        public FlagSnapshot Export()
        {
            return new FlagSnapshot
            {
                Bools = ExportBools(),
                Ints = ExportInts(),
                Strings = ExportStrings()
            };
        }

        public void Import(FlagSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var nextKinds = new Dictionary<string, FlagKind>();
            var nextBools = new Dictionary<string, bool>();
            var nextInts = new Dictionary<string, int>();
            var nextStrings = new Dictionary<string, string>();

            CollectBools(snapshot.Bools, nextKinds, nextBools);
            CollectInts(snapshot.Ints, nextKinds, nextInts);
            CollectStrings(snapshot.Strings, nextKinds, nextStrings);

            kinds.Clear();
            bools.Clear();
            ints.Clear();
            strings.Clear();
            foreach (KeyValuePair<string, FlagKind> pair in nextKinds)
                kinds[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, bool> pair in nextBools)
                bools[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, int> pair in nextInts)
                ints[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, string> pair in nextStrings)
                strings[pair.Key] = pair.Value;
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

        FlagBoolEntry[] ExportBools()
        {
            var entries = new FlagBoolEntry[bools.Count];
            int i = 0;
            foreach (KeyValuePair<string, bool> pair in bools)
                entries[i++] = new FlagBoolEntry(pair.Key, pair.Value);
            return entries;
        }

        FlagIntEntry[] ExportInts()
        {
            var entries = new FlagIntEntry[ints.Count];
            int i = 0;
            foreach (KeyValuePair<string, int> pair in ints)
                entries[i++] = new FlagIntEntry(pair.Key, pair.Value);
            return entries;
        }

        FlagStringEntry[] ExportStrings()
        {
            var entries = new FlagStringEntry[strings.Count];
            int i = 0;
            foreach (KeyValuePair<string, string> pair in strings)
                entries[i++] = new FlagStringEntry(pair.Key, pair.Value);
            return entries;
        }

        static void CollectBools(
            FlagBoolEntry[] entries,
            Dictionary<string, FlagKind> nextKinds,
            Dictionary<string, bool> nextBools)
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                string key = entries[i].Key;
                BindNext(nextKinds, key, FlagKind.Bool);
                nextBools[key] = entries[i].Value;
            }
        }

        static void CollectInts(
            FlagIntEntry[] entries,
            Dictionary<string, FlagKind> nextKinds,
            Dictionary<string, int> nextInts)
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                string key = entries[i].Key;
                BindNext(nextKinds, key, FlagKind.Int);
                nextInts[key] = entries[i].Value;
            }
        }

        static void CollectStrings(
            FlagStringEntry[] entries,
            Dictionary<string, FlagKind> nextKinds,
            Dictionary<string, string> nextStrings)
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Length; i++)
            {
                string key = entries[i].Key;
                BindNext(nextKinds, key, FlagKind.String);
                nextStrings[key] = entries[i].Value ?? "";
            }
        }

        static void BindNext(Dictionary<string, FlagKind> nextKinds, string key, FlagKind kind)
        {
            RequireKey(key);
            FlagKind existing;
            if (nextKinds.TryGetValue(key, out existing))
            {
                if (existing != kind)
                {
                    throw new SequencePlayException(
                        "type_mismatch",
                        "Flag '" + key + "' is " + existing + ", not " + kind + ".");
                }

                throw new SequencePlayException(
                    "invalid_document",
                    "Duplicate flag '" + key + "'.");
            }

            nextKinds[key] = kind;
        }
    }
}
