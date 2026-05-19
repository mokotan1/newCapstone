using NUnit.Framework;
using UnityEngine;

public class CheckpointRepositoryTests
{
    [TearDown]
    public void TearDown()
    {
        CheckpointRepository.Clear();
    }

    [Test]
    public void HasCheckpoint_ReturnsFalse_WhenNoCheckpointSaved()
    {
        CheckpointRepository.Clear();

        Assert.That(CheckpointRepository.HasCheckpoint(), Is.False);
    }

    [Test]
    public void SaveAndTryLoad_RoundTripsLatestCheckpoint()
    {
        var data = new CheckpointSaveData
        {
            version = 1,
            checkpointId = "unlock_study_room",
            checkpointType = CheckpointType.RoomUnlock,
            unlockedRoomKey = FungusVariableKeys.UsedStudyKey,
            resumeSceneName = SceneNames.StudyRoom,
            resumeSpawnId = "room_start",
            itemIds = new[] { 3, 7 },
            fungusIntegers = new[]
            {
                new IntCheckpointEntry(ItemAcquisitionTracker.FungusVariableKey, 42)
            }
        };

        CheckpointRepository.Save(data);

        Assert.That(CheckpointRepository.HasCheckpoint(), Is.True);
        Assert.That(CheckpointRepository.TryLoad(out var loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_study_room"));
        Assert.That(loaded.checkpointType, Is.EqualTo(CheckpointType.RoomUnlock));
        Assert.That(loaded.unlockedRoomKey, Is.EqualTo(FungusVariableKeys.UsedStudyKey));
        Assert.That(loaded.resumeSceneName, Is.EqualTo(SceneNames.StudyRoom));
        Assert.That(loaded.itemIds, Is.EquivalentTo(new[] { 3, 7 }));
        Assert.That(loaded.fungusIntegers.Length, Is.EqualTo(1));
        Assert.That(loaded.fungusIntegers[0].key, Is.EqualTo(ItemAcquisitionTracker.FungusVariableKey));
        Assert.That(loaded.fungusIntegers[0].value, Is.EqualTo(42));
    }

    [Test]
    public void Clear_RemovesCheckpointButPreservesSettings()
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, 0.25f);
        CheckpointRepository.Save(new CheckpointSaveData
        {
            checkpointId = "unlock_child_room",
            checkpointType = CheckpointType.RoomUnlock,
            resumeSceneName = SceneNames.ChildRoom
        });

        CheckpointRepository.Clear();

        Assert.That(CheckpointRepository.HasCheckpoint(), Is.False);
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume), Is.EqualTo(0.25f).Within(0.001f));
    }
}
