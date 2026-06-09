using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenPuzzleStateTests
{
    GameObject root;
    Flowchart flowchart;
    KitchenPuzzleState state;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("KitchenPuzzleStateTest");
        flowchart = root.AddComponent<Flowchart>();
        state = root.AddComponent<KitchenPuzzleState>();
        state.SetFlowchartForTests(flowchart);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ApplyBlockCompletion_BottleClicked_SetsFlagOnlyWhenHasBottle()
    {
        state.SetSinkFlagsForTests(hasBottle: false, bottleClicked: false, faucetClicked: false, bottleDragged: false);

        state.ApplyBlockCompletion(KitchenSinkInteractionGate.BottleClickedBlockName);

        Assert.IsFalse(state.BottleClicked);
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.BottleClicked));

        state.SetSinkFlagsForTests(hasBottle: true, bottleClicked: false, faucetClicked: false, bottleDragged: false);
        state.ApplyBlockCompletion(KitchenSinkInteractionGate.BottleClickedBlockName);

        Assert.IsTrue(state.BottleClicked);
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.BottleClicked));
    }

    [Test]
    public void ApplyBlockCompletion_FaucetAndBottleDragged_MirrorToFlowchart()
    {
        state.SetSinkFlagsForTests(hasBottle: true, bottleClicked: false, faucetClicked: false, bottleDragged: false);

        state.ApplyBlockCompletion(KitchenSinkInteractionGate.FaucetBlockName);
        Assert.IsTrue(state.FaucetClicked);
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.FaucetClicked));

        state.ApplyBlockCompletion(KitchenSinkInteractionGate.BottleDraggedBlockName);
        Assert.IsTrue(state.BottleDragged);
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.BottleDragged));
    }

    [Test]
    public void MirrorSinkFlagsToFlowchart_SyncsKitchenOwnedFlags()
    {
        state.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: false, bottleDragged: true);

        state.MirrorSinkFlagsToFlowchart(flowchart);

        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.BottleClicked));
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.FaucetClicked));
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.BottleDragged));
    }
}
