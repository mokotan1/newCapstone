using System;
using System.Collections.Generic;

public static class CheckpointSavePolicy
{
    public static void ValidateForSave(CheckpointSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (string.IsNullOrWhiteSpace(data.resumeSceneName))
            throw new ArgumentException("Checkpoint resumeSceneName must be non-empty.", nameof(data));

        RejectDuplicateOrBlankFungusKeys(data);
    }

    private static void RejectDuplicateOrBlankFungusKeys(CheckpointSaveData data)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        RejectKeys(seen, data.fungusBooleans, entry => entry.key, "bool");
        RejectKeys(seen, data.fungusIntegers, entry => entry.key, "int");
        RejectKeys(seen, data.fungusStrings, entry => entry.key, "string");
    }

    private static void RejectKeys<T>(
        Dictionary<string, string> seen,
        T[] entries,
        Func<T, string> keyOf,
        string typeName)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Length; i++)
        {
            string key = keyOf(entries[i]);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Checkpoint fungus key must be non-empty.", "data");

            if (seen.TryGetValue(key, out string existingType))
            {
                throw new ArgumentException(
                    "Checkpoint fungus key '" + key + "' is already registered as " + existingType + ".",
                    "data");
            }

            seen[key] = typeName;
        }
    }
}
