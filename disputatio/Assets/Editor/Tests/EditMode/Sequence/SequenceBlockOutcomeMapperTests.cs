using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequenceBlockOutcomeMapperTests
{
    [Test]
    public void ShouldGoBack_WhenFlagUnset_ReturnsFalse()
    {
        var flags = new FlagStore();
        Assert.IsFalse(SequenceBlockOutcomeMapper.ShouldGoBack(flags));
    }

    [Test]
    public void TryGetLoadScene_WhenSet_ReturnsSceneAndClearRemoves()
    {
        var flags = new FlagStore();
        flags.SetString(SequenceBlockOutcomeMapper.LoadSceneKey, "Hall");

        Assert.IsTrue(SequenceBlockOutcomeMapper.TryGetLoadScene(flags, out string scene));
        Assert.AreEqual("Hall", scene);

        SequenceBlockOutcomeMapper.Clear(flags);
        Assert.IsFalse(SequenceBlockOutcomeMapper.TryGetLoadScene(flags, out _));
    }

    [Test]
    public void IsEphemeralOutcomeKey_RecognizesReservedKeys()
    {
        Assert.IsTrue(SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(SequenceBlockOutcomeMapper.GoBackKey));
        Assert.IsTrue(SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey(SequenceBlockOutcomeMapper.LoadSceneKey));
        Assert.IsFalse(SequenceBlockOutcomeMapper.IsEphemeralOutcomeKey("quest.flag"));
    }

    [Test]
    public void ShouldGoBack_WhenTrue_AndClearResets()
    {
        var flags = new FlagStore();
        flags.SetBool(SequenceBlockOutcomeMapper.GoBackKey, true);

        Assert.IsTrue(SequenceBlockOutcomeMapper.ShouldGoBack(flags));
        SequenceBlockOutcomeMapper.Clear(flags);
        Assert.IsFalse(SequenceBlockOutcomeMapper.ShouldGoBack(flags));
    }
}
