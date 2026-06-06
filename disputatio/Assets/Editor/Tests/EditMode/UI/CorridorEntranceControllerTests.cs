using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CorridorEntranceControllerTests
{
    GameObject root;
    CorridorEntranceController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("CorridorEntranceTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<CorridorEntranceController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "door", fungusBlockName = "Door_Clicked" },
        });
        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome
            {
                blockName = "Go_Yes",
                loadScene = true,
                sceneName = SceneNames.BedRoom,
            },
            new BlockOutcome
            {
                blockName = "Go_No",
                resetIsClicked = true,
            },
            new BlockOutcome
            {
                blockName = "Select_Yes",
                goBack = true,
            },
        });

        RebuildLookupCaches(controller);
    }

    [TearDown]
    public void TearDown()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
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

    [Test]
    public void OnInteraction_KnownId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("door");

        Assert.AreEqual("Door_Clicked", executedBlock);
    }

    [Test]
    public void OnBlockEnd_LoadOutcome_RequestsSceneLoad()
    {
        string requestedScene = null;
        RoomInteractionController.SceneLoadHandlerForTests = sceneName =>
        {
            requestedScene = sceneName;
            return true;
        };

        var block = root.AddComponent<Block>();
        block.BlockName = "Go_Yes";

        controller.InvokeBlockEndForTests(block);

        Assert.AreEqual(SceneNames.BedRoom, requestedScene);
    }

    [Test]
    public void OnBlockEnd_GoBackOutcome_InvokesGoBackHandler()
    {
        bool goBackCalled = false;
        RoomInteractionController.GoBackHandlerForTests = () => goBackCalled = true;

        var block = root.AddComponent<Block>();
        block.BlockName = "Select_Yes";

        controller.InvokeBlockEndForTests(block);

        Assert.IsTrue(goBackCalled);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(target, value);
            return;
        }
    }

    static void RebuildLookupCaches(RoomInteractionController target)
    {
        typeof(RoomInteractionController)
            .GetMethod(
                "BuildLookupCaches",
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
            ?.Invoke(target, null);
    }
}
