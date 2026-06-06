using System.Collections.Generic;
using Fungus;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GlassMenuTests
{
    GameObject root;
    GlassMenu command;
    Block blockA;
    Block blockB;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("GlassMenuCmdRoot");
        root.AddComponent<Flowchart>();
        command = root.AddComponent<GlassMenu>();
        blockA = root.AddComponent<Block>();
        blockA.BlockName = "BlockA";
        blockB = root.AddComponent<Block>();
        blockB.BlockName = "BlockB";

        SetPrivate(command, "options", new List<GlassMenuOption>
        {
            new GlassMenuOption { text = "A", targetBlock = blockA, interactable = true },
            new GlassMenuOption { text = "B", targetBlock = blockB, interactable = true },
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void GetConnectedBlocks_ReturnsAllTargets()
    {
        var connected = new List<Block>();
        command.GetConnectedBlocks(ref connected);

        Assert.Contains(blockA, connected);
        Assert.Contains(blockB, connected);
        Assert.AreEqual(2, connected.Count);
    }

    [Test]
    public void GetSummary_ReportsOptionCount()
    {
        Assert.AreEqual("2 options", command.GetSummary());
    }

    [Test]
    public void GetSummary_NoOptions_ReportsError()
    {
        SetPrivate(command, "options", new List<GlassMenuOption>());
        StringAssert.Contains("Error", command.GetSummary());
    }

    [Test]
    public void MayCallBlock_TrueOnlyForTargets()
    {
        var unrelated = root.AddComponent<Block>();
        unrelated.BlockName = "Unrelated";

        Assert.IsTrue(((IBlockCaller)command).MayCallBlock(blockA));
        Assert.IsFalse(((IBlockCaller)command).MayCallBlock(unrelated));
    }

    static void SetPrivate(object t, string name, object value)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f.SetValue(t, value);
    }
}
