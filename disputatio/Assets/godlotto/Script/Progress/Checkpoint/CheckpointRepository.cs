using System;
using System.Collections.Generic;
using UnityEngine;

public static class CheckpointRepository
{
    private const string LatestCheckpointKey = "Checkpoint.Latest.v1";
    private const string LatestCheckpointIdKey = "Checkpoint.LatestId.v1";

    public static bool HasCheckpoint()
    {
        return PlayerPrefs.HasKey(LatestCheckpointKey) && TryLoad(out _);
    }

    public static void Save(CheckpointSaveData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (string.IsNullOrWhiteSpace(data.resumeSceneName))
            throw new ArgumentException("Checkpoint resumeSceneName must be non-empty.", nameof(data));

        RejectDuplicateOrBlankFungusKeys(data);

        if (data.version <= 0)
            data.version = 1;

        if (string.IsNullOrEmpty(data.createdAtUtc))
            data.createdAtUtc = DateTime.UtcNow.ToString("o");

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LatestCheckpointKey, json);

        if (!string.IsNullOrEmpty(data.checkpointId))
            PlayerPrefs.SetString(LatestCheckpointIdKey, data.checkpointId);

        PlayerPrefs.Save();
    }

    public static bool TryLoad(out CheckpointSaveData data)
    {
        data = null;

        string json = PlayerPrefs.GetString(LatestCheckpointKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            data = JsonUtility.FromJson<CheckpointSaveData>(json);
        }
        catch (ArgumentException ex)
        {
            GameLog.LogWarning("[CheckpointRepository] 체크포인트 JSON 파싱 실패: " + ex.Message);
            data = null;
            return false;
        }

        if (data == null || string.IsNullOrEmpty(data.resumeSceneName))
        {
            data = null;
            return false;
        }

        if (data.itemIds == null)
            data.itemIds = new int[0];
        if (data.fungusBooleans == null)
            data.fungusBooleans = new BoolCheckpointEntry[0];
        if (data.fungusIntegers == null)
            data.fungusIntegers = new IntCheckpointEntry[0];
        if (data.fungusStrings == null)
            data.fungusStrings = new StringCheckpointEntry[0];

        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(LatestCheckpointKey);
        PlayerPrefs.DeleteKey(LatestCheckpointIdKey);
        PlayerPrefs.Save();
    }

    static void RejectDuplicateOrBlankFungusKeys(CheckpointSaveData data)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        RejectKeys(seen, data.fungusBooleans, entry => entry.key, "bool");
        RejectKeys(seen, data.fungusIntegers, entry => entry.key, "int");
        RejectKeys(seen, data.fungusStrings, entry => entry.key, "string");
    }

    static void RejectKeys<T>(
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
