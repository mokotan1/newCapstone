using System;
using NUnit.Framework;

public class CheckpointSavePolicyTests
{
    [Test]
    public void ValidateForSave_RejectsNullData()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CheckpointSavePolicy.ValidateForSave(null));

        Assert.That(exception.ParamName, Is.EqualTo("data"));
    }

    [Test]
    public void ValidateForSave_RejectsWhitespaceResumeScene()
    {
        CheckpointSaveData data = ValidData();
        data.resumeSceneName = "  ";

        Assert.Throws<ArgumentException>(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    [Test]
    public void ValidateForSave_RejectsBlankFungusKey()
    {
        CheckpointSaveData data = ValidData();
        data.fungusBooleans = new[] { new BoolCheckpointEntry(" ", true) };

        Assert.Throws<ArgumentException>(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    [Test]
    public void ValidateForSave_RejectsFungusKeyWithTwoTypes()
    {
        CheckpointSaveData data = ValidData();
        data.fungusBooleans = new[] { new BoolCheckpointEntry("door", true) };
        data.fungusIntegers = new[] { new IntCheckpointEntry("door", 1) };

        Assert.Throws<ArgumentException>(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    [Test]
    public void ValidateForSave_RejectsRepeatedFungusKeyInOneArray()
    {
        CheckpointSaveData data = ValidData();
        data.fungusStrings = new[]
        {
            new StringCheckpointEntry("inventory", "1"),
            new StringCheckpointEntry("inventory", "2")
        };

        Assert.Throws<ArgumentException>(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    [Test]
    public void ValidateForSave_AcceptsNullFungusArrays()
    {
        CheckpointSaveData data = ValidData();
        data.fungusBooleans = null;
        data.fungusIntegers = null;
        data.fungusStrings = null;

        Assert.DoesNotThrow(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    [Test]
    public void ValidateForSave_AcceptsSequenceKeyOverlap()
    {
        CheckpointSaveData data = ValidData();
        data.fungusIntegers = new[] { new IntCheckpointEntry("door", 1) };
        data.sequenceBooleans = new[] { new BoolCheckpointEntry("door", true) };

        Assert.DoesNotThrow(() => CheckpointSavePolicy.ValidateForSave(data));
    }

    private static CheckpointSaveData ValidData()
    {
        return new CheckpointSaveData { resumeSceneName = "StudyRoom" };
    }
}
