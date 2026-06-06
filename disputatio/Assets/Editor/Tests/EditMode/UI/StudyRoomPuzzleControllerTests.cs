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
