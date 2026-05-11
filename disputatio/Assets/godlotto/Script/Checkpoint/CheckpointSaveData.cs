using System;

public enum CheckpointType
{
    RoomUnlock,
    Basement,
    Minigame
}

[Serializable]
public class CheckpointSaveData
{
    public int version = 1;
    public string checkpointId;
    public CheckpointType checkpointType;
    public string unlockedRoomKey;
    public string resumeSceneName;
    public string resumeSpawnId;
    public string createdAtUtc;
    public int[] itemIds = new int[0];
    public BoolCheckpointEntry[] fungusBooleans = new BoolCheckpointEntry[0];
    public StringCheckpointEntry[] fungusStrings = new StringCheckpointEntry[0];
}

[Serializable]
public struct BoolCheckpointEntry
{
    public string key;
    public bool value;

    public BoolCheckpointEntry(string key, bool value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public struct StringCheckpointEntry
{
    public string key;
    public string value;

    public StringCheckpointEntry(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}
