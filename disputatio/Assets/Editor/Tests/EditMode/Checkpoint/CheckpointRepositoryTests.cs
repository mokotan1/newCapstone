using System;
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
            },
            sequenceBooleans = new[]
            {
                new BoolCheckpointEntry("door_open", true)
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
        Assert.That(loaded.sequenceBooleans.Length, Is.EqualTo(1));
        Assert.That(loaded.sequenceBooleans[0].key, Is.EqualTo("door_open"));
        Assert.That(loaded.sequenceBooleans[0].value, Is.True);
    }

    [Test]
    public void Save_DefaultsVersionAndTimestampBeforeRoundTrip()
    {
        var data = new CheckpointSaveData
        {
            version = 0,
            createdAtUtc = string.Empty,
            resumeSceneName = SceneNames.StudyRoom
        };

        CheckpointRepository.Save(data);

        Assert.That(data.version, Is.EqualTo(1));
        Assert.That(DateTime.TryParse(data.createdAtUtc, out _), Is.True);
        Assert.That(CheckpointRepository.TryLoad(out CheckpointSaveData loaded), Is.True);
        Assert.That(loaded.version, Is.EqualTo(1));
        Assert.That(loaded.createdAtUtc, Is.EqualTo(data.createdAtUtc));
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

    [Test]
    public void Save_WhenResumeSceneBlank_ThrowsAndKeepsPreviousCheckpoint()
    {
        CheckpointRepository.Save(ValidStudyRoomCheckpoint());
        CheckpointSaveData invalid = ValidStudyRoomCheckpoint();
        invalid.resumeSceneName = "  ";
        invalid.checkpointId = "should-not-replace";

        Assert.Throws<ArgumentException>(() => CheckpointRepository.Save(invalid));

        Assert.That(CheckpointRepository.TryLoad(out CheckpointSaveData loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_study_room"));
        Assert.That(loaded.resumeSceneName, Is.EqualTo(SceneNames.StudyRoom));
    }

    [Test]
    public void Save_WhenDataNull_ThrowsArgumentNullAndKeepsPreviousCheckpoint()
    {
        CheckpointRepository.Save(ValidStudyRoomCheckpoint());

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CheckpointRepository.Save(null));

        Assert.That(exception.ParamName, Is.EqualTo("data"));
        Assert.That(CheckpointRepository.TryLoad(out CheckpointSaveData loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_study_room"));
    }

    [Test]
    public void Save_WhenValidationFails_DoesNotApplyDefaultsToInput()
    {
        CheckpointSaveData invalid = ValidStudyRoomCheckpoint();
        invalid.version = 0;
        invalid.createdAtUtc = null;
        invalid.resumeSceneName = "  ";

        Assert.Throws<ArgumentException>(() => CheckpointRepository.Save(invalid));

        Assert.That(invalid.version, Is.EqualTo(0));
        Assert.That(invalid.createdAtUtc, Is.Null);
        Assert.That(CheckpointRepository.HasCheckpoint(), Is.False);
    }

    [Test]
    public void Save_WhenFungusKeyBlank_ThrowsAndDoesNotWrite()
    {
        CheckpointSaveData invalid = ValidStudyRoomCheckpoint();
        invalid.fungusBooleans = new[] { new BoolCheckpointEntry("  ", true) };

        Assert.Throws<ArgumentException>(() => CheckpointRepository.Save(invalid));
        Assert.That(CheckpointRepository.HasCheckpoint(), Is.False);
    }

    [Test]
    public void Save_WhenSameKeyHasTwoTypes_ThrowsAndKeepsPreviousCheckpoint()
    {
        CheckpointRepository.Save(ValidStudyRoomCheckpoint());
        CheckpointSaveData invalid = ValidStudyRoomCheckpoint();
        invalid.checkpointId = "should-not-replace";
        invalid.fungusBooleans = new[] { new BoolCheckpointEntry(FungusVariableKeys.ElectricOn, true) };
        invalid.fungusIntegers = new[] { new IntCheckpointEntry(FungusVariableKeys.ElectricOn, 1) };

        Assert.Throws<ArgumentException>(() => CheckpointRepository.Save(invalid));

        Assert.That(CheckpointRepository.TryLoad(out CheckpointSaveData loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_study_room"));
    }

    [Test]
    public void Save_WhenFungusKeyRepeatedInOneArray_ThrowsAndKeepsPreviousCheckpoint()
    {
        CheckpointRepository.Save(ValidStudyRoomCheckpoint());
        CheckpointSaveData invalid = ValidStudyRoomCheckpoint();
        invalid.checkpointId = "should-not-replace";
        invalid.fungusStrings = new[]
        {
            new StringCheckpointEntry(FungusVariableKeys.InventoryItemIds, "1"),
            new StringCheckpointEntry(FungusVariableKeys.InventoryItemIds, "2")
        };

        Assert.Throws<ArgumentException>(() => CheckpointRepository.Save(invalid));

        Assert.That(CheckpointRepository.TryLoad(out CheckpointSaveData loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_study_room"));
    }

    static CheckpointSaveData ValidStudyRoomCheckpoint()
    {
        return new CheckpointSaveData
        {
            version = 1,
            checkpointId = "unlock_study_room",
            checkpointType = CheckpointType.RoomUnlock,
            unlockedRoomKey = FungusVariableKeys.UsedStudyKey,
            resumeSceneName = SceneNames.StudyRoom,
            resumeSpawnId = "room_start"
        };
    }
}
