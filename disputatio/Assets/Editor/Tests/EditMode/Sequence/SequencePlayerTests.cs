using Godlotto.Sequence;
using NUnit.Framework;

[TestFixture]
public class SequencePlayerTests
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
    public void SetBool_WritesFlag()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_bool", key = "lit", bool_value = true }));
        player.Play(doc, "start");
        Assert.IsTrue(flags.GetBool("lit"));
    }

    [Test]
    public void IfBool_TakesThenBlock()
    {
        var flags = new FlagStore();
        flags.SetBool("ok", true);
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "if_bool", key = "ok", then_block = "yes", else_block = "no" }),
            Block(
                "yes",
                new SequenceOp { command = "set_int", key = "n", int_value = 1 }),
            Block(
                "no",
                new SequenceOp { command = "set_int", key = "n", int_value = 0 }));
        player.Play(doc, "start");
        Assert.AreEqual(1, flags.GetInt("n"));
    }

    [Test]
    public void IfBool_TakesElseBlock()
    {
        var flags = new FlagStore();
        flags.SetBool("ok", false);
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "if_bool", key = "ok", then_block = "yes", else_block = "no" }),
            Block("yes", new SequenceOp { command = "set_string", key = "w", string_value = "y" }),
            Block("no", new SequenceOp { command = "set_string", key = "w", string_value = "n" }));
        player.Play(doc, "start");
        Assert.AreEqual("n", flags.GetString("w"));
    }

    [Test]
    public void UnknownCommand_Throws()
    {
        var player = new SequencePlayer(new FlagStore());
        SequenceDocument doc = Doc(
            Block("start", new SequenceOp { command = "explode", key = "x" }));
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
        Assert.AreEqual("invalid_document", ex.Code);
    }

    [Test]
    public void UnknownBlock_Throws()
    {
        var player = new SequencePlayer(new FlagStore());
        SequencePlayException ex = Assert.Throws<SequencePlayException>(
            () => player.Play(Doc(Block("start")), "missing"));
        Assert.AreEqual("unknown_block", ex.Code);
    }

    [Test]
    public void IfBool_Cycle_Throws()
    {
        var flags = new FlagStore();
        flags.SetBool("loop", true);
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "a",
                new SequenceOp { command = "if_bool", key = "loop", then_block = "b", else_block = "end" }),
            Block(
                "b",
                new SequenceOp { command = "if_bool", key = "loop", then_block = "a", else_block = "end" }),
            Block("end"));
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => player.Play(doc, "a"));
        Assert.AreEqual("cycle", ex.Code);
    }

    [Test]
    public void EmptyCommand_Throws()
    {
        var player = new SequencePlayer(new FlagStore());
        SequenceDocument doc = Doc(Block("start", new SequenceOp { command = "" }));
        Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
    }

    [Test]
    public void SetIntAndString_WriteFlags()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_int", key = "n", int_value = 4 },
                new SequenceOp { command = "set_string", key = "s", string_value = "hi" }));
        player.Play(doc, "start");
        Assert.AreEqual(4, flags.GetInt("n"));
        Assert.AreEqual("hi", flags.GetString("s"));
    }

    [Test]
    public void InvalidLaterCommand_DoesNotMutateEarlierFlags()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "set_bool", key = "touched", bool_value = true },
                new SequenceOp { command = "not_a_command", key = "x" }));
        Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
        Assert.IsFalse(flags.Has("touched"));
    }

    [Test]
    public void DuplicateBlockId_FailsBeforePlay()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block("start", new SequenceOp { command = "set_bool", key = "a", bool_value = true }),
            Block("start", new SequenceOp { command = "set_bool", key = "b", bool_value = true }));
        Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
        Assert.IsFalse(flags.Has("a"));
        Assert.IsFalse(flags.Has("b"));
    }

    [Test]
    public void MissingSchemaVersion_FailsBeforePlay()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        var doc = new SequenceDocument
        {
            schemaVersion = 0,
            blocks = new[] { Block("start") }
        };
        SequencePlayException ex = Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
        Assert.AreEqual("invalid_document", ex.Code);
        Assert.IsFalse(flags.Has("start"));
    }

    [Test]
    public void MissingThenBlockReference_FailsBeforePlay()
    {
        var flags = new FlagStore();
        flags.SetBool("ok", true);
        var player = new SequencePlayer(flags);
        SequenceDocument doc = Doc(
            Block(
                "start",
                new SequenceOp { command = "if_bool", key = "ok", then_block = "gone", else_block = "end" }),
            Block("end"));
        Assert.Throws<SequencePlayException>(() => player.Play(doc, "start"));
    }
}
