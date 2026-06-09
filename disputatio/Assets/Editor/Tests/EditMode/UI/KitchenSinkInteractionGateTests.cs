using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenSinkInteractionGateTests
{
    GameObject root;
    KitchenPuzzleState state;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("KitchenSinkGateTest");
        state = root.AddComponent<KitchenPuzzleState>();
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ShouldExecuteFungusBlock_BottleDrag_BlockedWhenNoBottleOrAlreadyDragged()
    {
        InventorySlot.draggedItem = null;
        state.SetSinkFlagsForTests(hasBottle: false, bottleClicked: false, faucetClicked: false, bottleDragged: false);
        Assert.IsFalse(KitchenSinkInteractionGate.ShouldExecuteFungusBlock("bottle_drag", state));

        state.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: true, bottleDragged: true);
        Assert.IsFalse(KitchenSinkInteractionGate.ShouldExecuteFungusBlock("bottle_drag", state));

        state.SetSinkFlagsForTests(hasBottle: true, bottleClicked: false, faucetClicked: false, bottleDragged: false);
        Assert.IsTrue(KitchenSinkInteractionGate.ShouldExecuteFungusBlock("bottle_drag", state));
    }

    [Test]
    public void ShouldExecuteFungusBlock_BottleDrag_AllowsWhenDraggedBottleEvenIfHasBottleFlagStale()
    {
        var bottle = ScriptableObject.CreateInstance<Item>();
        bottle.itemName = "Bottle";
        InventorySlot.draggedItem = bottle;

        state.SetSinkFlagsForTests(hasBottle: false, bottleClicked: false, faucetClicked: false, bottleDragged: false);
        Assert.IsTrue(KitchenSinkInteractionGate.ShouldExecuteFungusBlock("bottle_drag", state));

        InventorySlot.draggedItem = null;
        Object.DestroyImmediate(bottle);
    }

    [TestCase("sink")]
    [TestCase("bottle")]
    [TestCase("faucet")]
    [TestCase("filled_bottle")]
    public void ShouldExecuteFungusBlock_SinkRouteClicks_AlwaysAllowFungusBranching(string interactionId)
    {
        state.SetSinkFlagsForTests(hasBottle: false, bottleClicked: false, faucetClicked: false, bottleDragged: true);
        Assert.IsTrue(KitchenSinkInteractionGate.ShouldExecuteFungusBlock(interactionId, state));
    }
}
