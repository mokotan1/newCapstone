using System.Collections.Generic;
using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequenceRouterTests
{
    static SequenceDocument Doc(params SequenceBlock[] blocks)
    {
        return new SequenceDocument
        {
            schemaVersion = SequenceLimits.CurrentSchemaVersion,
            blocks = blocks
        };
    }

    static SequenceBlock Block(string id, params SequenceOp[] ops)
    {
        return new SequenceBlock { id = id, commands = ops };
    }

    [Test]
    public void Play_RunsRegisteredSequence()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var input = new FakeInputLock();
        var catalog = new SequenceCatalog();
        catalog.Register(
            "door",
            Doc(Block("start", new SequenceOp { command = "set_bool", key = "open", bool_value = true })),
            "start");
        var router = new SequenceRouter(catalog, flags, host, input);

        router.Play("door");

        Assert.IsTrue(flags.GetBool("open"));
        Assert.IsFalse(input.IsBlocked);
        Assert.AreEqual(1, input.BlockCount);
    }

    [Test]
    public void Play_UnknownRoute_DoesNotLockOrMutate()
    {
        var flags = new FlagStore();
        var input = new FakeInputLock();
        var catalog = new SequenceCatalog();
        catalog.Register(
            "door",
            Doc(Block("start", new SequenceOp { command = "set_bool", key = "open", bool_value = true })),
            "start");
        var router = new SequenceRouter(catalog, flags, new FakeHost(), input);

        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => router.Play("window"));
        Assert.AreEqual("unknown_route", ex.Code);
        Assert.IsFalse(flags.Has("open"));
        Assert.AreEqual(0, input.BlockCount);
    }

    [Test]
    public void Register_InvalidDocument_DoesNotKeepRoute()
    {
        var catalog = new SequenceCatalog();
        var bad = Doc(
            Block("start", new SequenceOp { command = "explode", key = "x" }));

        Assert.Throws<SequencePlayException>(() => catalog.Register("door", bad, "start"));

        var flags = new FlagStore();
        var input = new FakeInputLock();
        var router = new SequenceRouter(catalog, flags, new FakeHost(), input);
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => router.Play("door"));
        Assert.AreEqual("unknown_route", ex.Code);
        Assert.AreEqual(0, input.BlockCount);
    }

    [Test]
    public void Register_DuplicateId_KeepsFirstRoute()
    {
        var catalog = new SequenceCatalog();
        catalog.Register(
            "door",
            Doc(Block("start", new SequenceOp { command = "set_int", key = "n", int_value = 1 })),
            "start");

        SequencePlayException ex = Assert.Throws<SequencePlayException>(
            () => catalog.Register(
                "door",
                Doc(Block("start", new SequenceOp { command = "set_int", key = "n", int_value = 9 })),
                "start"));
        Assert.AreEqual("invalid_document", ex.Code);

        var flags = new FlagStore();
        new SequenceRouter(catalog, flags, new FakeHost(), new FakeInputLock()).Play("door");
        Assert.AreEqual(1, flags.GetInt("n"));
    }

    [Test]
    public void Play_EmptyInteractionId_Throws()
    {
        var catalog = new SequenceCatalog();
        var router = new SequenceRouter(catalog, new FlagStore(), new FakeHost(), new FakeInputLock());
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => router.Play(" "));
        Assert.AreEqual("empty_key", ex.Code);
    }

    [Test]
    public void Play_DoesNotUseFungusCommandNamesAsFlags()
    {
        var flags = new FlagStore();
        var host = new FakeHost();
        var catalog = new SequenceCatalog();
        catalog.Register(
            "look",
            Doc(Block(
                "start",
                new SequenceOp { command = "say", key = "hero", string_value = "문이 있다." })),
            "start");
        var router = new SequenceRouter(catalog, flags, host, new FakeInputLock());

        router.Play("look");

        CollectionAssert.AreEqual(new[] { "say:hero:문이 있다." }, host.Events);
        Assert.IsFalse(flags.Has("hero"));
    }

    sealed class FakeHost : ISequenceHost
    {
        public readonly List<string> Events = new List<string>();

        public void Wait(int milliseconds)
        {
            Events.Add("wait:" + milliseconds);
        }

        public void Say(string speaker, string line)
        {
            Events.Add("say:" + speaker + ":" + line);
        }
    }

    sealed class FakeInputLock : ISequenceInputLock
    {
        public int BlockCount;
        public int UnblockCount;
        public bool IsBlocked
        {
            get { return BlockCount > UnblockCount; }
        }

        public void Block(string reason)
        {
            BlockCount++;
        }

        public void Unblock(string reason)
        {
            UnblockCount++;
        }
    }
}
