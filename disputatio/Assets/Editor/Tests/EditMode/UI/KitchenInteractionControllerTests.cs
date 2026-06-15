using System.Linq;
using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenInteractionControllerTests
{
    GameObject root;
    KitchenInteractionController controller;
    KitchenPuzzleState puzzleState;
    KitchenSinkWaterDisplay sinkWaterDisplay;
    Flowchart flowchart;
    GameObject water;
    GameObject faucetClosed;
    GameObject faucetOpen;

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
        puzzleState = root.AddComponent<KitchenPuzzleState>();
        controller = root.AddComponent<KitchenInteractionController>();

        var displayPanel = new GameObject(KitchenSinkWaterDisplayPolicy.SinkPanelName);
        displayPanel.transform.SetParent(root.transform, false);
        var overlayGo = new GameObject(KitchenSinkWaterDisplayPolicy.OverlayChildName);
        overlayGo.transform.SetParent(displayPanel.transform, false);
        var overlayCanvas = overlayGo.AddComponent<Canvas>();
        var faucetClosedGo = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetClosedName);
        var faucetOpenGo = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetOpenName);
        var waterGo = new GameObject(KitchenSinkWaterDisplayPolicy.WaterRootName);
        faucetClosedGo.transform.SetParent(displayPanel.transform, false);
        faucetOpenGo.transform.SetParent(overlayGo.transform, false);
        waterGo.transform.SetParent(overlayGo.transform, false);
        faucetClosed = faucetClosedGo;
        faucetOpen = faucetOpenGo;
        water = waterGo;

        sinkWaterDisplay = displayPanel.AddComponent<KitchenSinkWaterDisplay>();
        sinkWaterDisplay.SetReferencesForTests(
            new GameObject(KitchenSinkWaterDisplayPolicy.BackgroundChildName),
            overlayCanvas,
            faucetClosed,
            faucetOpen,
            water);

        puzzleState.SetFlowchartForTests(flowchart);
        controller.SetSinkWaterDisplayForTests(sinkWaterDisplay);
        AddBooleanVariable(flowchart, FungusVariableKeys.BottleClicked, false);
        AddBooleanVariable(flowchart, FungusVariableKeys.FaucetClicked, false);
        AddBooleanVariable(flowchart, FungusVariableKeys.BottleDragged, false);
        SetPrivateField(controller, "flowchart", flowchart);
        SetPrivateField(controller, "puzzleState", puzzleState);
        SetPrivateField(controller, "routes", KitchenSceneMigrationSpecs.AllInteractionRoutes()
            .Select(route => new InteractionRoute
            {
                interactionId = route.InteractionId,
                fungusBlockName = route.BlockName,
            })
            .ToArray());

        RebuildLookupCaches(controller);
    }

    [TearDown]
    public void TearDown()
    {
        InventorySlot.draggedItem = null;
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

    [TestCaseSource(nameof(AllMigrationRouteCases))]
    public void OnInteraction_RouteId_ExecutesMappedBlock(string interactionId, string expectedBlock)
    {
        if (interactionId == KitchenSinkInteractionGate.BottleDragInteractionId)
        {
            puzzleState.SetSinkFlagsForTests(
                hasBottle: true,
                bottleClicked: false,
                faucetClicked: false,
                bottleDragged: false);
        }

        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        controller.OnInteraction(interactionId);

        Assert.AreEqual(expectedBlock, executedBlock);
    }

    [Test]
    public void OnInteraction_BottleDrag_WhenAlreadyDragged_DoesNotExecuteBlock()
    {
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: true, bottleDragged: true);

        bool executed = false;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, __) =>
        {
            executed = true;
            return true;
        };

        controller.OnInteraction("bottle_drag");

        Assert.IsFalse(executed);
    }

    [Test]
    public void OnInteraction_BottleDrag_WhenEligible_ExecutesBlockAndCommitsState()
    {
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: false, faucetClicked: false, bottleDragged: false);

        string executedBlock = null;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, blockName) =>
        {
            executedBlock = blockName;
            return true;
        };

        SetPrivateField(controller, "blockOutcomes", new[]
        {
            new BlockOutcome { blockName = KitchenSinkInteractionGate.BottleDraggedBlockName },
        });
        RebuildLookupCaches(controller);

        controller.OnInteraction("bottle_drag");
        controller.InvokeBlockEndForTests(CreateBlock(flowchart, KitchenSinkInteractionGate.BottleDraggedBlockName));

        Assert.AreEqual(KitchenSinkInteractionGate.BottleDraggedBlockName, executedBlock);
        Assert.IsTrue(puzzleState.BottleDragged);
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.BottleDragged));
        Assert.IsFalse(water.activeSelf);
        Assert.IsTrue(faucetClosed.activeSelf);
        Assert.IsFalse(faucetOpen.activeSelf);
    }

    [Test]
    public void PrepareInteractionExecution_Faucet_DoesNotSyncWaterBeforeBlockCompletion()
    {
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: false, bottleDragged: false);

        InvokePrepareInteractionExecution(
            KitchenSinkInteractionGate.FaucetInteractionId,
            KitchenSinkInteractionGate.FaucetBlockName);

        Assert.IsFalse(water.activeSelf);
        Assert.IsTrue(faucetClosed.activeSelf);
        Assert.IsFalse(faucetOpen.activeSelf);
    }

    [Test]
    public void OnBlockEnd_Faucet_SyncsWaterDisplayFromPuzzleState()
    {
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: false, bottleDragged: false);

        controller.InvokeBlockEndForTests(CreateBlock(flowchart, KitchenSinkInteractionGate.FaucetBlockName));

        Assert.IsTrue(puzzleState.FaucetClicked);
        Assert.IsTrue(water.activeSelf);
        Assert.IsFalse(faucetClosed.activeSelf);
        Assert.IsTrue(faucetOpen.activeSelf);
    }

    [Test]
    public void OnBlockEnd_BottleDragged_TurnsWaterDisplayOff()
    {
        puzzleState.SetSinkFlagsForTests(hasBottle: true, bottleClicked: true, faucetClicked: true, bottleDragged: false);
        sinkWaterDisplay.SyncFromFaucetClicked(true);

        controller.InvokeBlockEndForTests(CreateBlock(flowchart, KitchenSinkInteractionGate.BottleDraggedBlockName));

        Assert.IsTrue(puzzleState.BottleDragged);
        Assert.IsFalse(water.activeSelf);
        Assert.IsTrue(faucetClosed.activeSelf);
        Assert.IsFalse(faucetOpen.activeSelf);
    }

    [Test]
    public void ShouldProcessWorldClickBinding_SuppressesFripanAndBurner_WhenFripanPanelOpen()
    {
        var registryGo = new GameObject("Registry");
        var registry = registryGo.AddComponent<KitchenPanelRegistry>();
        var fripanPanel = CreatePanel("firpan_Panel", active: true);
        SetRegistryField(registry, "fripanPanel", fripanPanel);
        SetPrivateField(controller, "panelRegistry", registry);

        var fripanBinding = new WorldClickBinding { interactionId = "fripan" };
        var burnerBinding = new WorldClickBinding { interactionId = "burner" };
        var doorBinding = new WorldClickBinding { interactionId = "door" };

        Assert.IsFalse(InvokeShouldProcessWorldClickBinding(fripanBinding));
        Assert.IsFalse(InvokeShouldProcessWorldClickBinding(burnerBinding));
        Assert.IsTrue(InvokeShouldProcessWorldClickBinding(doorBinding));

        fripanPanel.SetActive(false);
        Assert.IsTrue(InvokeShouldProcessWorldClickBinding(fripanBinding));

        Object.DestroyImmediate(registryGo);
        Object.DestroyImmediate(fripanPanel);
    }

    bool InvokeShouldProcessWorldClickBinding(WorldClickBinding binding)
    {
        var method = typeof(KitchenInteractionController).GetMethod(
            "ShouldProcessWorldClickBinding",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (bool)method.Invoke(controller, new object[] { binding, Vector2.zero });
    }

    [Test]
    public void OnClosePanel_Backspace_ClosesAllRegistryPanels_WithoutFungusBlock()
    {
        var registryGo = new GameObject("Registry");
        var registry = registryGo.AddComponent<KitchenPanelRegistry>();

        var fripanPanel = CreatePanel("firpan_Panel", active: true);
        var sinkPanel = CreatePanel("Sink_Pannel", active: true);
        var burnerPanel = CreatePanel("burner", active: true);
        var parrotPanel = CreatePanel("Parret", active: true);
        var bottlePanel = CreatePanel("Bottle", active: true);

        SetRegistryField(registry, "fripanPanel", fripanPanel);
        SetRegistryField(registry, "sinkPanel", sinkPanel);
        SetRegistryField(registry, "burnerPanel", burnerPanel);
        SetRegistryField(registry, "parrotPanel", parrotPanel);
        SetRegistryField(registry, "bottlePanel", bottlePanel);
        SetPrivateField(controller, "panelRegistry", registry);

        bool fungusExecuted = false;
        FungusDialogueBridge.ExecuteBlockHandlerForTests = (_, __) =>
        {
            fungusExecuted = true;
            return true;
        };

        controller.OnClosePanel("panel_backspace", fripanPanel);

        Assert.IsFalse(fungusExecuted);
        Assert.IsFalse(fripanPanel.activeSelf);
        Assert.IsFalse(sinkPanel.activeSelf);
        Assert.IsFalse(burnerPanel.activeSelf);
        Assert.IsFalse(parrotPanel.activeSelf);
        Assert.IsFalse(bottlePanel.activeSelf);

        Object.DestroyImmediate(registryGo);
        Object.DestroyImmediate(fripanPanel);
        Object.DestroyImmediate(sinkPanel);
        Object.DestroyImmediate(burnerPanel);
        Object.DestroyImmediate(parrotPanel);
        Object.DestroyImmediate(bottlePanel);
    }

    static object[] AllMigrationRouteCases =>
        KitchenSceneMigrationSpecs.AllInteractionRoutes()
            .Select(route => new object[] { route.InteractionId, route.BlockName })
            .ToArray();

    [Test]
    public void MigrationSpecs_AllInteractionRoutes_CoverEveryMigratedFungusBlock()
    {
        var routedBlocks = new System.Collections.Generic.HashSet<string>(
            KitchenSceneMigrationSpecs.AllInteractionRoutes().Select(route => route.BlockName),
            System.StringComparer.Ordinal);

        foreach (string blockName in KitchenSceneMigrationSpecs.MigratedFungusBlockNames)
        {
            Assert.IsTrue(
                routedBlocks.Contains(blockName),
                $"KitchenSceneMigrationSpecs.AllInteractionRoutes() should include '{blockName}'.");
        }
    }

    static GameObject CreatePanel(string name, bool active)
    {
        var go = new GameObject(name);
        go.SetActive(active);
        return go;
    }

    static void SetRegistryField(KitchenPanelRegistry registry, string fieldName, GameObject panel)
    {
        typeof(KitchenPanelRegistry)
            .GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(registry, panel);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? typeof(RoomInteractionController).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        field?.SetValue(target, value);
    }

    static void RebuildLookupCaches(RoomInteractionController target)
    {
        typeof(RoomInteractionController)
            .GetMethod("BuildLookupCaches", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }

    static Block CreateBlock(Flowchart targetFlowchart, string blockName)
    {
        var block = targetFlowchart.gameObject.AddComponent<Block>();
        block.BlockName = blockName;
        return block;
    }

    void InvokePrepareInteractionExecution(string interactionId, string blockName)
    {
        typeof(KitchenInteractionController).GetMethod(
            "PrepareInteractionExecution",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.Invoke(controller, new object[] { interactionId, blockName });
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
