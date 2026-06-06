using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class StudyRoomPuzzleControllerTests
{
    GameObject root;
    StudyRoomPuzzleController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("StudyRoomTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<StudyRoomPuzzleController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "cardstack", fungusBlockName = "CardStack_Clicked" },
            new InteractionRoute { interactionId = "diary", fungusBlockName = "Diary_Clicked" },
            new InteractionRoute { interactionId = "bible", fungusBlockName = "Bible_Clicked" },
            new InteractionRoute { interactionId = "bookcase1", fungusBlockName = "BookCase1_Clicked" },
            new InteractionRoute { interactionId = "unlock", fungusBlockName = "UnlockSuccess" },
        });
        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome
            {
                blockName = "BookCase1_Clicked",
                loadScene = true,
                sceneName = "BookCase1",
            },
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

    [Test]
    public void OnInteraction_CardStackId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("cardstack");

        Assert.AreEqual("CardStack_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_DiaryId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("diary");

        Assert.AreEqual("Diary_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_BibleId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("bible");

        Assert.AreEqual("Bible_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_BookCase1Id_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("bookcase1");

        Assert.AreEqual("BookCase1_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_UnlockId_ExecutesUnlockSuccessBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("unlock");

        Assert.AreEqual("UnlockSuccess", executedBlock);
    }

    [Test]
    public void OnBlockEnd_BookCase1LoadOutcome_RequestsSceneLoad()
    {
        string requestedScene = null;
        RoomInteractionController.SceneLoadHandlerForTests = sceneName =>
        {
            requestedScene = sceneName;
            return true;
        };

        var block = root.AddComponent<Block>();
        block.BlockName = "BookCase1_Clicked";

        controller.InvokeBlockEndForTests(block);

        Assert.AreEqual("BookCase1", requestedScene);
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
