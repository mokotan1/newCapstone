using Fungus;
using Godlotto.Interaction;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BedRoomInteractionControllerTests
{
    GameObject root;
    BedRoomInteractionController controller;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        root = new GameObject("BedRoomTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<BedRoomInteractionController>();

        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "bookcase", fungusBlockName = "Bookcase_Clicked" },
            new InteractionRoute { interactionId = "safe", fungusBlockName = "Safe_Clicked" },
        });

        RebuildLookupCaches(controller);
    }

    [TearDown]
    public void TearDown()
    {
        RoomInteractionController.ResetStateForTests();
        FungusDialogueBridge.ResetForTests();
        ModalInputGate.ResetForTests();
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
    public void OnInteraction_KnownId_ExecutesMappedBlock()
    {
        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("bookcase");

        Assert.AreEqual("Bookcase_Clicked", executedBlock);
    }

    [Test]
    public void OnInteraction_UnlockId_ExecutesOnUnlockBlock()
    {
        SetPrivateField(controller, "routes", new[]
        {
            new InteractionRoute { interactionId = "unlock", fungusBlockName = "onUnlock" },
        });
        RebuildLookupCaches(controller);

        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction("unlock");

        Assert.AreEqual("onUnlock", executedBlock);
    }

    [Test]
    public void OnClosePanel_DeactivatesPanelAndResetsIsClicked()
    {
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);
        var panel = new GameObject("BookPanel");
        panel.SetActive(true);

        controller.OnClosePanel("bookpanel_backspace", panel);

        Assert.IsFalse(panel.activeSelf);
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void OnClosePanel_SafePanel_HidesSafeItemEffectWhenKeyFlagsSet()
    {
        var safePanel = new GameObject("SafePanel");
        var safeItemEffect = new GameObject("SafeItemEffect");
        safePanel.SetActive(true);
        safeItemEffect.SetActive(true);

        AddBooleanVariable(flowchart, "HavePrisonKey", true);
        AddBooleanVariable(flowchart, "HaveHolyGrail", true);
        SetPrivateField(controller, "safePanel", safePanel);
        SetPrivateField(controller, "safeItemEffect", safeItemEffect);

        controller.OnClosePanel("panel_backspace", safePanel);

        Assert.IsFalse(safePanel.activeSelf);
        Assert.IsFalse(safeItemEffect.activeSelf);
    }

    [Test]
    public void OnClosePanel_SafePanel_KeepsSafeItemEffectWhenKeyFlagsMissing()
    {
        var safePanel = new GameObject("SafePanel");
        var safeItemEffect = new GameObject("SafeItemEffect");
        safePanel.SetActive(true);
        safeItemEffect.SetActive(true);

        AddBooleanVariable(flowchart, "HavePrisonKey", false);
        AddBooleanVariable(flowchart, "HaveHolyGrail", true);
        SetPrivateField(controller, "safePanel", safePanel);
        SetPrivateField(controller, "safeItemEffect", safeItemEffect);

        controller.OnClosePanel("panel_backspace", safePanel);

        Assert.IsFalse(safePanel.activeSelf);
        Assert.IsTrue(safeItemEffect.activeSelf);
    }

    [Test]
    public void Awake_AddsModalScopeToSafePanelAndBlocksParrotBehindIt()
    {
        ModalInputGate.ResetForTests();
        var safePanel = new GameObject("SafePanel");
        safePanel.SetActive(false);
        var parrot = new GameObject("Parret");
        SetPrivateField(controller, "safePanel", safePanel);

        InvokeAwake(controller);
        safePanel.SetActive(true);

        Assert.IsNotNull(safePanel.GetComponent<ModalInputScope>());
        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);
        Assert.IsFalse(ModalInputGate.CanWorldClick(parrot));

        Object.DestroyImmediate(parrot);
        Object.DestroyImmediate(safePanel);
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

    static void InvokeAwake(BedRoomInteractionController target)
    {
        typeof(BedRoomInteractionController)
            .GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.DeclaredOnly)
            ?.Invoke(target, null);
    }
}
