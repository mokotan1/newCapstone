using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class FlagStoreTests
{
    [Test]
    public void SetBool_RoundTrips()
    {
        var store = new FlagStore();
        store.SetBool("door_open", true);
        Assert.IsTrue(store.GetBool("door_open"));
        Assert.IsTrue(store.Has("door_open"));
    }

    [Test]
    public void SetInt_RoundTrips()
    {
        var store = new FlagStore();
        store.SetInt("count", 3);
        Assert.AreEqual(3, store.GetInt("count"));
    }

    [Test]
    public void SetString_RoundTrips()
    {
        var store = new FlagStore();
        store.SetString("scene", "Kitchen");
        Assert.AreEqual("Kitchen", store.GetString("scene"));
    }

    [Test]
    public void EmptyKey_Throws()
    {
        var store = new FlagStore();
        Assert.Throws<SequencePlayException>(() => store.SetBool(" ", true));
        Assert.Throws<SequencePlayException>(() => store.GetBool(""));
    }

    [Test]
    public void Has_IsFalse_ForUnknownKey()
    {
        var store = new FlagStore();
        Assert.IsFalse(store.Has("missing"));
    }

    [Test]
    public void SetInt_AfterSetBool_SameKey_Throws()
    {
        var store = new FlagStore();
        store.SetBool("flag", true);
        Assert.Throws<SequencePlayException>(() => store.SetInt("flag", 1));
        Assert.IsTrue(store.GetBool("flag"));
    }

    [Test]
    public void GetBool_AfterSetInt_Throws_NotFalse()
    {
        var store = new FlagStore();
        store.SetInt("n", 2);
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => store.GetBool("n"));
        Assert.AreEqual("type_mismatch", ex.Code);
    }

    [Test]
    public void GetBool_MissingKey_Throws_NotFalse()
    {
        var store = new FlagStore();
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => store.GetBool("nope"));
        Assert.AreEqual("missing_key", ex.Code);
    }
}
