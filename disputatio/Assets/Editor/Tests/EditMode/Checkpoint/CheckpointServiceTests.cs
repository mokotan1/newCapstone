using NUnit.Framework;

public class CheckpointServiceTests
{
    [TearDown]
    public void TearDown()
    {
        CheckpointRepository.Clear();
    }

    [Test]
    public void SaveRoomUnlock_WritesCheckpointForUnlockKey()
    {
        Assert.That(RoomUnlockCheckpointService.SaveRoomUnlock(FungusVariableKeys.UsedWifeKey), Is.True);

        Assert.That(CheckpointRepository.TryLoad(out var loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_wife_room"));
        Assert.That(loaded.resumeSceneName, Is.EqualTo(SceneNames.WifeRoom));
        Assert.That(loaded.unlockedRoomKey, Is.EqualTo(FungusVariableKeys.UsedWifeKey));
    }

    [Test]
    public void SaveRoomUnlock_ReturnsFalseForUnknownUnlockKey()
    {
        Assert.That(RoomUnlockCheckpointService.SaveRoomUnlock("UnknownUnlockKey"), Is.False);
        Assert.That(CheckpointRepository.HasCheckpoint(), Is.False);
    }

    [Test]
    public void LoadCoordinator_UsesFallbackScene_WhenNoCheckpointExists()
    {
        Assert.That(CheckpointLoadCoordinator.GetResumeSceneOrFallback(SceneNames.MainScene), Is.EqualTo(SceneNames.MainScene));
    }

    [Test]
    public void LoadCoordinator_UsesCheckpointScene_WhenCheckpointExists()
    {
        RoomUnlockCheckpointService.SaveRoomUnlock(FungusVariableKeys.UsedBedKey);

        Assert.That(CheckpointLoadCoordinator.GetResumeSceneOrFallback(SceneNames.MainScene), Is.EqualTo(SceneNames.BedRoom));
    }
}
