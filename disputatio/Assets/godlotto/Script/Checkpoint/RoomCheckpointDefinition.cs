using System;

public sealed class RoomCheckpointDefinition
{
    private static readonly RoomCheckpointDefinition[] Definitions =
    {
        new RoomCheckpointDefinition("unlock_kitchen", FungusVariableKeys.ElectricOn, SceneNames.Kitchen, "room_start", CheckpointType.RoomUnlock, 10),
        new RoomCheckpointDefinition("unlock_study_room", FungusVariableKeys.UsedStudyKey, SceneNames.StudyRoom, "room_start", CheckpointType.RoomUnlock, 20),
        new RoomCheckpointDefinition("unlock_maid_room", FungusVariableKeys.UsedMaidKey, SceneNames.MaidRoom, "room_start", CheckpointType.RoomUnlock, 30),
        new RoomCheckpointDefinition("unlock_tutor_room", FungusVariableKeys.UsedTutorKey, SceneNames.TutorRoom, "room_start", CheckpointType.RoomUnlock, 40),
        new RoomCheckpointDefinition("unlock_child_room", FungusVariableKeys.UsedChildKey, SceneNames.ChildRoom, "room_start", CheckpointType.RoomUnlock, 50),
        new RoomCheckpointDefinition("unlock_wife_room", FungusVariableKeys.UsedWifeKey, SceneNames.WifeRoom, "room_start", CheckpointType.RoomUnlock, 60),
        new RoomCheckpointDefinition("unlock_bed_room", FungusVariableKeys.UsedBedKey, SceneNames.BedRoom, "room_start", CheckpointType.RoomUnlock, 70),
    };

    public string CheckpointId { get; }
    public string UnlockKey { get; }
    public string ResumeSceneName { get; }
    public string ResumeSpawnId { get; }
    public CheckpointType CheckpointType { get; }
    public int Order { get; }

    private RoomCheckpointDefinition(
        string checkpointId,
        string unlockKey,
        string resumeSceneName,
        string resumeSpawnId,
        CheckpointType checkpointType,
        int order)
    {
        CheckpointId = checkpointId;
        UnlockKey = unlockKey;
        ResumeSceneName = resumeSceneName;
        ResumeSpawnId = resumeSpawnId;
        CheckpointType = checkpointType;
        Order = order;
    }

    public static bool TryGetByUnlockKey(string unlockKey, out RoomCheckpointDefinition definition)
    {
        definition = null;

        if (string.IsNullOrEmpty(unlockKey))
            return false;

        for (int i = 0; i < Definitions.Length; i++)
        {
            if (string.Equals(Definitions[i].UnlockKey, unlockKey, StringComparison.Ordinal))
            {
                definition = Definitions[i];
                return true;
            }
        }

        return false;
    }
}
