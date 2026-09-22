using UnityEngine;

public static class RoomUnlockCheckpointService
{
    public static bool SaveRoomUnlock(string unlockKey)
    {
        if (!RoomCheckpointDefinition.TryGetByUnlockKey(unlockKey, out var definition))
        {
            GameLog.LogWarning("[Checkpoint] 알 수 없는 방 해금 키라 체크포인트를 저장하지 않았습니다: " + unlockKey);
            return false;
        }

        SaveRoomUnlock(definition);
        return true;
    }

    public static void SaveRoomUnlock(RoomCheckpointDefinition definition)
    {
        if (definition == null)
            return;

        var data = new CheckpointSaveData
        {
            checkpointId = definition.CheckpointId,
            checkpointType = definition.CheckpointType,
            unlockedRoomKey = definition.UnlockKey,
            resumeSceneName = definition.ResumeSceneName,
            resumeSpawnId = definition.ResumeSpawnId
        };

        ProgressSnapshotCollector.Populate(data);
        CheckpointRepository.Save(data);
        GameLog.Log("[Checkpoint] 최신 체크포인트 저장: " + data.checkpointId + " -> " + data.resumeSceneName);
    }
}
