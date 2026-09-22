using System;
using System.Collections.Generic;

namespace Godlotto.Sequence
{
    public sealed class FlagStore
    {
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();
        readonly Dictionary<string, FlagValueType> types = new Dictionary<string, FlagValueType>();

        public bool Has(string key)
        {
            RequireKey(key);
            return bools.ContainsKey(key) || ints.ContainsKey(key) || strings.ContainsKey(key);
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            RequireKey(key);
            RequireType(key, FlagValueType.Bool);
            return bools.TryGetValue(key, out bool value) ? value : defaultValue;
        }

        public void SetBool(string key, bool value)
        {
            RequireKey(key);
            RegisterType(key, FlagValueType.Bool);
            bools[key] = value;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            RequireKey(key);
            RequireType(key, FlagValueType.Int);
            return ints.TryGetValue(key, out int value) ? value : defaultValue;
        }

        public void SetInt(string key, int value)
        {
            RequireKey(key);
            RegisterType(key, FlagValueType.Int);
            ints[key] = value;
        }

        public string GetString(string key, string defaultValue = "")
        {
            RequireKey(key);
            RequireType(key, FlagValueType.String);
            return strings.TryGetValue(key, out string value) ? value : defaultValue;
        }

        public void SetString(string key, string value)
        {
            RequireKey(key);
            RegisterType(key, FlagValueType.String);
            strings[key] = value ?? "";
        }

        public void Clear()
        {
            bools.Clear();
            ints.Clear();
            strings.Clear();
            types.Clear();
        }

        static void RequireKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Flag key must be non-empty.", nameof(key));
        }

        void RegisterType(string key, FlagValueType requestedType)
        {
            if (types.TryGetValue(key, out FlagValueType existingType))
            {
                if (existingType != requestedType)
                    throw TypeMismatch(key, existingType, requestedType);
                return;
            }

            types[key] = requestedType;
        }

        void RequireType(string key, FlagValueType requestedType)
        {
            if (types.TryGetValue(key, out FlagValueType existingType) && existingType != requestedType)
                throw TypeMismatch(key, existingType, requestedType);
        }

        static InvalidOperationException TypeMismatch(
            string key,
            FlagValueType existingType,
            FlagValueType requestedType)
        {
            return new InvalidOperationException(
                "Flag key '" + key + "' is registered as " + existingType +
                " and cannot be used as " + requestedType + ".");
        }

        enum FlagValueType
        {
            Bool,
            Int,
            String
        }
    }
}
