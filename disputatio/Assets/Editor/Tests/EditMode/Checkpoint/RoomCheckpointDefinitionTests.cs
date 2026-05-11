using NUnit.Framework;

public class RoomCheckpointDefinitionTests
{
    [Test]
    public void TryGetByUnlockKey_ReturnsRoomInteriorScene()
    {
        Assert.That(RoomCheckpointDefinition.TryGetByUnlockKey(FungusVariableKeys.UsedTutorKey, out var definition), Is.True);
        Assert.That(definition.CheckpointId, Is.EqualTo("unlock_tutor_room"));
        Assert.That(definition.ResumeSceneName, Is.EqualTo(SceneNames.TutorRoom));
        Assert.That(definition.CheckpointType, Is.EqualTo(CheckpointType.RoomUnlock));
    }

    [Test]
    public void ProgressSnapshotPolicy_ExcludesSettingsAndPuzzleMidStateKeys()
    {
        Assert.That(ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(SettingPlayerPrefsKeys.BgmVolume), Is.False);
        Assert.That(ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey("Dial_CurrentAngle"), Is.False);
        Assert.That(ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey("SnapState_ChildRoom_Horse1"), Is.False);
        Assert.That(ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey("No40_FirstDeathLinePlayed"), Is.True);
    }
}
