using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class FlagStoreSnapshotTests
{
    [Test]
    public void ExportImport_RoundTripsTypedFlags()
    {
        var store = new FlagStore();
        store.SetBool("door_open", true);
        store.SetInt("count", 4);
        store.SetString("scene", "Kitchen");

        FlagSnapshot snapshot = store.Export();
        var restored = new FlagStore();
        restored.Import(snapshot);

        Assert.IsTrue(restored.GetBool("door_open"));
        Assert.AreEqual(4, restored.GetInt("count"));
        Assert.AreEqual("Kitchen", restored.GetString("scene"));
    }

    [Test]
    public void Import_ReplacesExistingFlags()
    {
        var store = new FlagStore();
        store.SetBool("old", true);
        store.SetInt("count", 1);

        var snapshot = new FlagSnapshot
        {
            Bools = new[] { new FlagBoolEntry("fresh", false) }
        };
        store.Import(snapshot);

        Assert.IsFalse(store.Has("old"));
        Assert.IsFalse(store.Has("count"));
        Assert.IsFalse(store.GetBool("fresh"));
    }

    [Test]
    public void Import_EmptyKey_DoesNotMutateStore()
    {
        var store = new FlagStore();
        store.SetBool("keep", true);

        var snapshot = new FlagSnapshot
        {
            Bools = new[] { new FlagBoolEntry(" ", true) }
        };

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => store.Import(snapshot));
        Assert.AreEqual("empty_key", ex.Code);
        Assert.IsTrue(store.GetBool("keep"));
    }

    [Test]
    public void Import_DuplicateKey_DoesNotMutateStore()
    {
        var store = new FlagStore();
        store.SetInt("keep", 9);

        var snapshot = new FlagSnapshot
        {
            Bools = new[] { new FlagBoolEntry("flag", true) },
            Ints = new[] { new FlagIntEntry("flag", 2) }
        };

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => store.Import(snapshot));
        Assert.AreEqual("type_mismatch", ex.Code);
        Assert.AreEqual(9, store.GetInt("keep"));
        Assert.IsFalse(store.Has("flag"));
    }

    [Test]
    public void Import_SameTypeDuplicate_DoesNotMutateStore()
    {
        var store = new FlagStore();
        store.SetString("keep", "ok");

        var snapshot = new FlagSnapshot
        {
            Strings = new[]
            {
                new FlagStringEntry("scene", "Kitchen"),
                new FlagStringEntry("scene", "Hall")
            }
        };

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => store.Import(snapshot));
        Assert.AreEqual("invalid_document", ex.Code);
        Assert.AreEqual("ok", store.GetString("keep"));
        Assert.IsFalse(store.Has("scene"));
    }

    [Test]
    public void Import_NullSnapshot_Throws()
    {
        var store = new FlagStore();
        store.SetBool("keep", true);
        Assert.Throws<System.ArgumentNullException>(() => store.Import(null));
        Assert.IsTrue(store.GetBool("keep"));
    }
}
