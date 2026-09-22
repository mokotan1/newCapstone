using Godlotto.Sequence;
using NUnit.Framework;
using System.Text;

[TestFixture]
public class SequencePlayerTests
{
    const string TwoBlockJson =
        "{\"schema_version\":1,\"blocks\":["
        + "{\"block_id\":\"start\",\"commands\":["
        + "{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true},"
        + "{\"command\":\"set_int\",\"key\":\"CorrectAnswerCount\",\"int_value\":3},"
        + "{\"command\":\"set_string\",\"key\":\"SceneName\",\"string_value\":\"Kitchen\"}"
        + "]},"
        + "{\"block_id\":\"other\",\"commands\":[]}"
        + "]}";

    const string BranchJson =
        "{\"blocks\":["
        + "{\"block_id\":\"gate\",\"commands\":["
        + "{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"yes\",\"else_block\":\"no\"}"
        + "]},"
        + "{\"block_id\":\"yes\",\"commands\":[{\"command\":\"set_int\",\"key\":\"path\",\"int_value\":1}]},"
        + "{\"block_id\":\"no\",\"commands\":[{\"command\":\"set_int\",\"key\":\"path\",\"int_value\":2}]}"
        + "]}";

    [Test]
    public void FromJson_ReadsBlockIdAndCommand()
    {
        SequenceDocument doc = SequenceDocument.FromJson(TwoBlockJson);
        Assert.That(doc.schema_version, Is.EqualTo(1));
        Assert.That(doc.TryGetBlock("start", out SequenceBlock block), Is.True);
        Assert.That(block.commands[0].command, Is.EqualTo("set_bool"));
        Assert.That(block.commands[0].key, Is.EqualTo("GetBottle"));
        Assert.That(block.commands[0].bool_value, Is.True);
    }

    [Test]
    public void FromJson_Empty_ReturnsEmptyDocument()
    {
        SequenceDocument doc = SequenceDocument.FromJson("");
        Assert.That(doc.blocks.Length, Is.EqualTo(0));
    }

    [Test]
    public void Play_SetBool_WritesFlagStore()
    {
        var flags = new FlagStore();
        var player = new SequencePlayer(flags);
        player.Play(SequenceDocument.FromJson(TwoBlockJson), "start");
        Assert.That(flags.GetBool("GetBottle"), Is.True);
        Assert.That(flags.GetInt("CorrectAnswerCount"), Is.EqualTo(3));
        Assert.That(flags.GetString("SceneName"), Is.EqualTo("Kitchen"));
    }

    [Test]
    public void Play_UnknownCommand_Throws()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"x\",\"commands\":[{\"command\":\"explode\"}]}]}";
        var player = new SequencePlayer(new FlagStore());
        var ex = Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(json), "x"));
        StringAssert.Contains("explode", ex.Message);
        StringAssert.Contains("x", ex.Message);
    }

    [Test]
    public void Play_MissingBlock_Throws()
    {
        var player = new SequencePlayer(new FlagStore());
        Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(TwoBlockJson), "nope"));
    }

    [Test]
    public void Play_IfBool_TakesThenWhenTrue()
    {
        var flags = new FlagStore();
        flags.SetBool("GetBottle", true);
        new SequencePlayer(flags).Play(SequenceDocument.FromJson(BranchJson), "gate");
        Assert.That(flags.GetInt("path"), Is.EqualTo(1));
    }

    [Test]
    public void Play_IfBool_TakesElseWhenFalse()
    {
        var flags = new FlagStore();
        new SequencePlayer(flags).Play(SequenceDocument.FromJson(BranchJson), "gate");
        Assert.That(flags.GetInt("path"), Is.EqualTo(2));
    }

    [Test]
    public void Play_IfBool_Cycle_Throws()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"loop\",\"commands\":["
            + "{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"loop\",\"else_block\":\"loop\"}"
            + "]}]}";
        var player = new SequencePlayer(new FlagStore());
        Assert.Throws<SequencePlayException>(
            () => player.Play(SequenceDocument.FromJson(json), "loop"));
    }

    [Test]
    public void Play_UnsupportedSchemaVersion_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"schema_version\":2,\"blocks\":[{\"block_id\":\"start\",\"commands\":["
            + "{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_DuplicateBlockId_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":["
            + "{\"block_id\":\"start\",\"commands\":[{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}]},"
            + "{\"block_id\":\"start\",\"commands\":[]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_SetCommandWithoutKey_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"start\",\"commands\":["
            + "{\"command\":\"set_bool\",\"bool_value\":true}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_SetBoolWithStringValue_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"start\",\"commands\":["
            + "{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":\"true\"}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_BrokenReferenceInUnvisitedBlock_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":["
            + "{\"block_id\":\"start\",\"commands\":[{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}]},"
            + "{\"block_id\":\"unused\",\"commands\":[{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"missing\",\"else_block\":\"missing\"}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_CycleInUnvisitedBlock_RejectsDocumentBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":["
            + "{\"block_id\":\"start\",\"commands\":[{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}]},"
            + "{\"block_id\":\"loop\",\"commands\":[{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"loop\",\"else_block\":\"loop\"}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_TooDeepBranchChain_RejectsDocumentBeforeMutatingFlags()
    {
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(() => new SequencePlayer(flags).Play(
            SequenceDocument.FromJson(BuildBranchChainJson(65)), "block-0"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_TooManyCommands_RejectsDocumentBeforeMutatingFlags()
    {
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(() => new SequencePlayer(flags).Play(
            SequenceDocument.FromJson(BuildSetBoolJson(1025)), "start"));

        Assert.That(flags.Has("GetBottle"), Is.False);
    }

    [Test]
    public void Play_ConflictingFlagTypesInOneDocument_RejectsBeforeMutatingFlags()
    {
        const string json =
            "{\"blocks\":[{\"block_id\":\"start\",\"commands\":["
            + "{\"command\":\"set_bool\",\"key\":\"shared\",\"bool_value\":true},"
            + "{\"command\":\"set_int\",\"key\":\"shared\",\"int_value\":1}]}]}";
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(
            () => new SequencePlayer(flags).Play(SequenceDocument.FromJson(json), "start"));

        Assert.That(flags.Has("shared"), Is.False);
    }

    [Test]
    public void Play_TooDeepSharedSuffix_RejectsDocument()
    {
        var flags = new FlagStore();

        Assert.Throws<SequencePlayException>(() => new SequencePlayer(flags).Play(
            SequenceDocument.FromJson(BuildSharedSuffixDepthJson(63)), "chain-0"));

        Assert.That(flags.Has("branch"), Is.False);
    }

    static string BuildBranchChainJson(int blockCount)
    {
        var json = new StringBuilder("{\"blocks\":[");
        for (int i = 0; i < blockCount; i++)
        {
            if (i > 0)
                json.Append(',');

            json.Append("{\"block_id\":\"block-").Append(i).Append("\",\"commands\":[");
            if (i < blockCount - 1)
            {
                json.Append("{\"command\":\"if_bool\",\"key\":\"GetBottle\",\"then_block\":\"block-")
                    .Append(i + 1)
                    .Append("\",\"else_block\":\"block-")
                    .Append(i + 1)
                    .Append("\"}");
            }

            json.Append("]}");
        }

        return json.Append("]}").ToString();
    }

    static string BuildSetBoolJson(int commandCount)
    {
        var json = new StringBuilder("{\"blocks\":[{\"block_id\":\"start\",\"commands\":[");
        for (int i = 0; i < commandCount; i++)
        {
            if (i > 0)
                json.Append(',');

            json.Append("{\"command\":\"set_bool\",\"key\":\"GetBottle\",\"bool_value\":true}");
        }

        return json.Append("]}]}").ToString();
    }

    static string BuildSharedSuffixDepthJson(int chainBlockCount)
    {
        var json = new StringBuilder("{\"blocks\":["
            + "{\"block_id\":\"shared-0\",\"commands\":[{\"command\":\"if_bool\",\"key\":\"branch\",\"then_block\":\"shared-1\",\"else_block\":\"shared-1\"}]},"
            + "{\"block_id\":\"shared-1\",\"commands\":[]}");

        for (int i = 0; i < chainBlockCount; i++)
        {
            string next = i == chainBlockCount - 1 ? "shared-0" : "chain-" + (i + 1);
            json.Append(",{\"block_id\":\"chain-").Append(i).Append("\",\"commands\":[")
                .Append("{\"command\":\"if_bool\",\"key\":\"branch\",\"then_block\":\"")
                .Append(next)
                .Append("\",\"else_block\":\"")
                .Append(next)
                .Append("\"}]}");
        }

        return json.Append("]}").ToString();
    }
}
