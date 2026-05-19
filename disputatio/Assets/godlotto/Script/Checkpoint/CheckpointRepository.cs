using System;
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
}
