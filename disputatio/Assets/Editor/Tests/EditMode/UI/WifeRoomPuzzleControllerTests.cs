using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class WifeRoomPuzzleControllerTests
{
    GameObject root;
    WifeRoomPuzzleController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("WifeRoomTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<WifeRoomPuzzleController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "wallclock", fungusBlockName = "Wallclock_Clicked" },
            new InteractionRoute { interactionId = "drawer", fungusBlockName = "Drawer_Clicked" },
            new InteractionRoute { interactionId = "down_drawer", fungusBlockName = "DownDrawer_Clicked" },
        });
        SetPrivateField(controller, "panelCloses", new[]
        {
            new PanelCloseBinding { panelCloseId = "wallclock_backspace", panel = null },
            new PanelCloseBinding { panelCloseId = "drawer_backspace", panel = null },
            new PanelCloseBinding { panelCloseId = "lock_backspace", panel = null },
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
    public void OnInteraction_WallclockId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("wallclock");

        Assert.AreEqual("Wallclock_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_DownDrawerId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("down_drawer");

        Assert.AreEqual("DownDrawer_Clicked", executedBlock);
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
    public void OnClosePanel_DeactivatesPanelAndResetsIsClicked()
    {
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);
        var panel = new GameObject("WallclockPanel");
        panel.SetActive(true);

        controller.OnClosePanel("wallclock_backspace", panel);

        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void OnInteraction_BackId_ExecutesBackSpaceClickedBlock()
    {
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "back", fungusBlockName = "BackSpace_Clicked" },
        });
        RebuildLookupCaches(controller);

        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("back");

        Assert.AreEqual("BackSpace_Clicked", executedBlock);
    }

    [Test]
    public void OnBlockEnd_SelectYes_InvokesGoBackHandler()
    {
        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = "Select_Yes", goBack = true },
        });
        RebuildLookupCaches(controller);

        bool goBackCalled = false;
        RoomInteractionController.GoBackHandlerForTests = () => goBackCalled = true;

        var block = root.AddComponent<Block>();
        block.BlockName = "Select_Yes";

        controller.InvokeBlockEndForTests(block);

        Assert.IsTrue(goBackCalled);
    }

    [Test]
    public void OnBlockEnd_SelectNo_ResetsIsClicked()
    {
        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = "Select_No", resetIsClicked = true },
        });
        RebuildLookupCaches(controller);

        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);

        var block = root.AddComponent<Block>();
        block.BlockName = "Select_No";

        controller.InvokeBlockEndForTests(block);

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
