using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenInteractionControllerTests
{
    GameObject root;
    KitchenInteractionController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("KitchenTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<KitchenInteractionController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "door", fungusBlockName = "Door_Clicked" },
            new InteractionRoute { interactionId = "door_to_hall", fungusBlockName = "Door_toHall_Clicked" },
            new InteractionRoute { interactionId = "refrigerator", fungusBlockName = "refrigeratorClicked" },
            new InteractionRoute { interactionId = "trashbox", fungusBlockName = "TrashBox_Clicked" },
            new InteractionRoute { interactionId = "sink", fungusBlockName = "Sink" },
            new InteractionRoute { interactionId = "bottle", fungusBlockName = "Bottle_Clicked" },
            new InteractionRoute { interactionId = "burner", fungusBlockName = "burner" },
            new InteractionRoute { interactionId = "faucet", fungusBlockName = "Faucet" },
            new InteractionRoute { interactionId = "filled_bottle", fungusBlockName = "FilledBottle" },
            new InteractionRoute { interactionId = "bottle_drag", fungusBlockName = "Bottle_Dragged" },
            new InteractionRoute { interactionId = "food_drag", fungusBlockName = "Food_Dragged" },
            new InteractionRoute { interactionId = "fripan", fungusBlockName = "fripan" },
            new InteractionRoute { interactionId = "parret", fungusBlockName = "parret" },
        });

        RebuildLookupCaches(controller);
    }

    [TearDown]
    public void TearDown()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        foreach (var runner in Object.FindObjectsByType<DeferredClickCleanup>(FindObjectsSortMode.None))
            Object.DestroyImmediate(runner.gameObject);
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void OnInteraction_UnknownId_IsIgnored()
    {
        bool executed = false;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, __) =>
        {
            executed = true;
            return true;
        };

        controller.OnInteraction("missing");

        Assert.IsFalse(executed);
    }

    [TestCase("door", "Door_Clicked")]
    [TestCase("door_to_hall", "Door_toHall_Clicked")]
    [TestCase("refrigerator", "refrigeratorClicked")]
    [TestCase("trashbox", "TrashBox_Clicked")]
    [TestCase("sink", "Sink")]
    [TestCase("bottle", "Bottle_Clicked")]
    [TestCase("burner", "burner")]
    [TestCase("faucet", "Faucet")]
    [TestCase("filled_bottle", "FilledBottle")]
    [TestCase("bottle_drag", "Bottle_Dragged")]
    [TestCase("food_drag", "Food_Dragged")]
    [TestCase("fripan", "fripan")]
    [TestCase("parret", "parret")]
    public void OnInteraction_RouteId_ExecutesMappedBlock(string interactionId, string expectedBlock)
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction(interactionId);

        Assert.AreEqual(expectedBlock, executedBlock);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        typeof(RoomInteractionController)
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }

    static void RebuildLookupCaches(RoomInteractionController target)
    {
        typeof(RoomInteractionController)
            .GetMethod("BuildLookupCaches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }
}
