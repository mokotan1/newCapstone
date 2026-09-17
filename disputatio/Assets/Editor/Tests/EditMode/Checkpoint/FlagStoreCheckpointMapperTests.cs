using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class FlagStoreCheckpointMapperTests
{
    [Test]
    public void Capture_WritesSequenceArrays_NotFungusArrays()
    {
        var data = new CheckpointSaveData
        {
            fungusBooleans = new[] { new BoolCheckpointEntry(FungusVariableKeys.ElectricOn, true) }
        };
        var flags = new FlagStore();
        flags.SetBool("door_open", true);
        flags.SetInt("count", 3);
        flags.SetString("scene", "Kitchen");

        FlagStoreCheckpointMapper.Capture(data, flags);

        Assert.AreEqual(1, data.fungusBooleans.Length);
        Assert.AreEqual(FungusVariableKeys.ElectricOn, data.fungusBooleans[0].key);
        Assert.AreEqual(1, data.sequenceBooleans.Length);
        Assert.AreEqual("door_open", data.sequenceBooleans[0].key);
        Assert.IsTrue(data.sequenceBooleans[0].value);
        Assert.AreEqual(1, data.sequenceIntegers.Length);
        Assert.AreEqual("count", data.sequenceIntegers[0].key);
        Assert.AreEqual(3, data.sequenceIntegers[0].value);
        Assert.AreEqual(1, data.sequenceStrings.Length);
        Assert.AreEqual("scene", data.sequenceStrings[0].key);
        Assert.AreEqual("Kitchen", data.sequenceStrings[0].value);
    }

    [Test]
    public void Capture_SkipsTransientKeys()
    {
        var data = new CheckpointSaveData();
        var flags = new FlagStore();
        flags.SetBool(FungusVariableKeys.IsClicked, true);
        flags.SetBool(FungusVariableKeys.WindowClicked, true);
        flags.SetBool("keep", true);
        flags.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, 2);

        FlagStoreCheckpointMapper.Capture(data, flags);

        Assert.AreEqual(1, data.sequenceBooleans.Length);
        Assert.AreEqual("keep", data.sequenceBooleans[0].key);
        Assert.AreEqual(0, data.sequenceIntegers.Length);
        Assert.AreEqual(0, data.fungusBooleans.Length);
    }

    [Test]
    public void Restore_LoadsSequenceArrays_IgnoresFungusArrays()
    {
        var data = new CheckpointSaveData
        {
            fungusBooleans = new[] { new BoolCheckpointEntry(FungusVariableKeys.ElectricOn, true) },
            sequenceBooleans = new[] { new BoolCheckpointEntry("door_open", true) },
            sequenceIntegers = new[] { new IntCheckpointEntry("count", 8) }
        };
        var flags = new FlagStore();
        flags.SetBool("runtime", true);

        FlagStoreCheckpointMapper.Restore(data, flags);

        Assert.IsFalse(flags.Has("runtime"));
        Assert.IsTrue(flags.GetBool("door_open"));
        Assert.AreEqual(8, flags.GetInt("count"));
        Assert.IsFalse(flags.Has(FungusVariableKeys.ElectricOn));
    }

    [Test]
    public void Restore_InvalidSequenceEntry_DoesNotMutateStore()
    {
        var data = new CheckpointSaveData
        {
            sequenceBooleans = new[] { new BoolCheckpointEntry("keep", true) },
            sequenceIntegers = new[] { new IntCheckpointEntry("keep", 1) }
        };
        var flags = new FlagStore();
        flags.SetString("existing", "ok");

        SequencePlayException ex = Assert.Throws<SequencePlayException>(
            () => FlagStoreCheckpointMapper.Restore(data, flags));
        Assert.AreEqual("type_mismatch", ex.Code);
        Assert.AreEqual("ok", flags.GetString("existing"));
        Assert.IsFalse(flags.Has("keep"));
    }

    [Test]
    public void Restore_SkipsTransientKeys()
    {
        var data = new CheckpointSaveData
        {
            sequenceBooleans = new[]
            {
                new BoolCheckpointEntry(FungusVariableKeys.IsClicked, true),
                new BoolCheckpointEntry("keep", true)
            }
        };
        var flags = new FlagStore();

        FlagStoreCheckpointMapper.Restore(data, flags);

        Assert.IsFalse(flags.Has(FungusVariableKeys.IsClicked));
        Assert.IsTrue(flags.GetBool("keep"));
    }
}
