using Fungus;
using NUnit.Framework;
using UnityEngine;

public class CheckpointServiceTests
{
    private GameObject variableManagerObject;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(variableManagerObject);
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

    [Test]
    public void RefreshLatestProgressSnapshot_PreservesCheckpointDestination_WhenRuntimeManagersAreMissing()
    {
        CheckpointRepository.Save(new CheckpointSaveData
        {
            checkpointId = "unlock_wife_room",
            checkpointType = CheckpointType.RoomUnlock,
            unlockedRoomKey = FungusVariableKeys.UsedWifeKey,
            resumeSceneName = SceneNames.WifeRoom,
            resumeSpawnId = "room_start",
            itemIds = new[] { 5, 8 }
        });

        Assert.That(CheckpointLoadCoordinator.RefreshLatestProgressSnapshot(), Is.True);

        Assert.That(CheckpointRepository.TryLoad(out var loaded), Is.True);
        Assert.That(loaded.checkpointId, Is.EqualTo("unlock_wife_room"));
        Assert.That(loaded.resumeSceneName, Is.EqualTo(SceneNames.WifeRoom));
        Assert.That(loaded.resumeSpawnId, Is.EqualTo("room_start"));
        Assert.That(loaded.itemIds, Is.EquivalentTo(new[] { 5, 8 }));
    }

    [Test]
    public void Apply_DoesNotRestoreTransientClickFlags()
    {
        Flowchart flowchart = CreateVariableManagerFlowchart();
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, false);
        AddBooleanVariable(flowchart, FungusVariableKeys.WindowClicked, false);

        ProgressSnapshotApplier.Apply(new CheckpointSaveData
        {
            resumeSceneName = SceneNames.BedRoom,
            fungusBooleans = new[]
            {
                new BoolCheckpointEntry(FungusVariableKeys.IsClicked, true),
                new BoolCheckpointEntry(FungusVariableKeys.WindowClicked, true)
            }
        });

        Assert.That(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked), Is.False);
        Assert.That(flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked), Is.False);
    }

    private Flowchart CreateVariableManagerFlowchart()
    {
        variableManagerObject = new GameObject("Variablemanager");
        return variableManagerObject.AddComponent<Flowchart>();
    }

    private static void AddBooleanVariable(Flowchart flowchart, string key, bool value)
    {
        BooleanVariable variable = flowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        flowchart.Variables.Add(variable);
    }
}
