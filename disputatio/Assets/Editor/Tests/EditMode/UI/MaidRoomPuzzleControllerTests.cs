using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MaidRoomPuzzleControllerTests
{
    GameObject root;
    MaidRoomPuzzleController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("MaidRoomTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<MaidRoomPuzzleController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "cookbook", fungusBlockName = "CookBook_Clicked" },
            new InteractionRoute { interactionId = "puzzlebook", fungusBlockName = "PuzzleBook_Clicked" },
            new InteractionRoute { interactionId = "keyshelf", fungusBlockName = "KeyShelf_Clicked" },
            new InteractionRoute { interactionId = "drawer", fungusBlockName = "drawer" },
            new InteractionRoute { interactionId = "food", fungusBlockName = "food" },
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
    public void OnInteraction_CookBookId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("cookbook");

        Assert.AreEqual("CookBook_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_FoodId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("food");

        Assert.AreEqual("food", executedBlock);
    }

    [Test]
    public void OnInteraction_UnlockId_ExecutesUnlockSuccessBlock()
    {
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "unlock", fungusBlockName = "UnlockSuccess" },
        });
        RebuildLookupCaches(controller);

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
    public void OnBlockEnd_CookBookSelectNo_ResetsIsClicked()
    {
        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = "CookBook_SelectNo", resetIsClicked = true },
        });
        RebuildLookupCaches(controller);
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);

        var block = root.AddComponent<Block>();
        block.BlockName = "CookBook_SelectNo";

        controller.InvokeBlockEndForTests(block);

        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void OnBlockEnd_CookBookSelectYes_OpensPanelAndHidesDiary()
    {
        var cookbookPanel = new GameObject("CookBook_Panel");
        cookbookPanel.SetActive(false);
        var diaryPanel = new GameObject("Diary_Panel");
        diaryPanel.SetActive(true);

        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = "CookBook_SelectYes", openPanel = cookbookPanel },
        });
        SetPrivateField(controller, "diaryPanelToHideOnBookOpen", diaryPanel);
        RebuildLookupCaches(controller);

        var block = root.AddComponent<Block>();
        block.BlockName = "CookBook_SelectYes";

        controller.InvokeBlockEndForTests(block);

        Assert.IsTrue(cookbookPanel.activeSelf);
        Assert.IsFalse(diaryPanel.activeSelf);
    }

    [Test]
    public void OnBlockEnd_PuzzleBookSelectYes_OpensPuzzlePanelAndHidesDiary()
    {
        var puzzlePanel = new GameObject("PuzzlePanel");
        puzzlePanel.SetActive(false);
        var diaryPanel = new GameObject("Diary_Panel");
        diaryPanel.SetActive(true);

        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = "PuzzleBook_SelectYes", openPanel = puzzlePanel },
        });
        SetPrivateField(controller, "diaryPanelToHideOnBookOpen", diaryPanel);
        RebuildLookupCaches(controller);

        var block = root.AddComponent<Block>();
        block.BlockName = "PuzzleBook_SelectYes";

        controller.InvokeBlockEndForTests(block);

        Assert.IsTrue(puzzlePanel.activeSelf);
        Assert.IsFalse(diaryPanel.activeSelf);
    }

    [Test]
    public void OnClosePanel_DeactivatesPanelAndResetsIsClicked()
    {
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);
        var panel = new GameObject("CookBook_Panel");
        panel.SetActive(true);

        SetPrivateField(controller, "panelCloses", new[]
        {
            new PanelCloseBinding { panelCloseId = "cookbook_panel_backspace", panel = panel },
        });
        RebuildLookupCaches(controller);

        controller.OnClosePanel("cookbook_panel_backspace", panel);

        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        target.Variables.Add(variable);
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
