using UnityEngine;

public class RoomUnlockCheckpointTrigger : MonoBehaviour
{
    [SerializeField] private string unlockKey;
    [SerializeField] private string customCheckpointId;
    [SerializeField] private CheckpointType checkpointType = CheckpointType.RoomUnlock;
    [SerializeField] private string resumeSceneNameOverride;
    [SerializeField] private string resumeSpawnIdOverride = "room_start";

    public void SaveCheckpoint()
    {
        if (!string.IsNullOrEmpty(resumeSceneNameOverride))
        {
            string checkpointId = string.IsNullOrEmpty(customCheckpointId) ? unlockKey : customCheckpointId;
            RoomUnlockCheckpointService.SaveCustom(
                checkpointId,
                checkpointType,
                resumeSceneNameOverride,
                resumeSpawnIdOverride,
                unlockKey);
            return;
        }

        RoomUnlockCheckpointService.SaveRoomUnlock(unlockKey);
    }

    public void SaveCheckpointForUnlockKey(string key)
    {
        unlockKey = key;
        SaveCheckpoint();
    }
}
