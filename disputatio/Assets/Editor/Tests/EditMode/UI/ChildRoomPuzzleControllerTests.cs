using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ChildRoomPuzzleControllerTests
{
    GameObject root;
    ChildRoomPuzzleController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("ChildRoomTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<ChildRoomPuzzleController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "bedfloor", fungusBlockName = "Bedfloor_Clicked" },
            new InteractionRoute { interactionId = "drawer", fungusBlockName = "Drawer_Clicked" },
            new InteractionRoute { interactionId = "chest", fungusBlockName = "Chest_Clicked" },
            new InteractionRoute { interactionId = "table", fungusBlockName = "Table_Clicked" },
            new InteractionRoute { interactionId = "parrot", fungusBlockName = "Parrot_Clicked" },
            new InteractionRoute { interactionId = "button", fungusBlockName = "Button_Clicked" },
            new InteractionRoute { interactionId = "drawer_open", fungusBlockName = "DrawerOpen" },
            new InteractionRoute { interactionId = "drawer_close", fungusBlockName = "DrawerClose" },
            new InteractionRoute { interactionId = "seal5", fungusBlockName = "Drag_seal5" },
            new InteractionRoute { interactionId = "seal6", fungusBlockName = "Drag_seal6" },
            new InteractionRoute { interactionId = "seal7", fungusBlockName = "Drag_seal7" },
            new InteractionRoute { interactionId = "all_seals_complete", fungusBlockName = "allSealsComplete" },
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

    [TestCase("bedfloor", "Bedfloor_Clicked")]
    [TestCase("drawer", "Drawer_Clicked")]
    [TestCase("chest", "Chest_Clicked")]
    [TestCase("table", "Table_Clicked")]
    [TestCase("parrot", "Parrot_Clicked")]
    [TestCase("button", "Button_Clicked")]
    [TestCase("drawer_open", "DrawerOpen")]
    [TestCase("drawer_close", "DrawerClose")]
    [TestCase("seal5", "Drag_seal5")]
    [TestCase("seal6", "Drag_seal6")]
    [TestCase("seal7", "Drag_seal7")]
    [TestCase("all_seals_complete", "allSealsComplete")]
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
