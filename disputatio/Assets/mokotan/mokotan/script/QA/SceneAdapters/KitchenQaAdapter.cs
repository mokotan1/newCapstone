#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fungus;
using Godlotto.Interaction;
using Godlotto.QA.Developer;
using Godlotto.QA.Input;
using Godlotto.QA.Scenes;
using UnityEngine;

namespace Godlotto.QA.SceneAdapters
{
    /// <summary>
    /// Kitchen scene QA adapter. Faucet capabilities plus bottle→key exit contract
    /// (<see cref="SinkBeforeBottleFillPresetCapabilityId"/> … <see cref="ExitAssertCapabilityId"/>).
    /// Uses public seams only: <see cref="KitchenPuzzleState"/> setters,
    /// <see cref="KitchenInteractionController.OnInteraction"/>, <see cref="ItemPickup.PickUpDirect"/>,
    /// inventory AddItem / itemId probes. Never ForceSolve; never fake HaveMaidKey for PASS.
    /// </summary>
    public sealed class KitchenQaAdapter : IQaSceneAdapter, IQaApiInteractable
    {
        public const string FaucetTargetIdValue = "kitchen.sink.faucet";
        public const string ParretTargetIdValue = "kitchen.parret";
        public const string SinkDropzoneTargetIdValue = "kitchen.sink.dropzone";
        public const string MaidKeyTargetIdValue = "kitchen.maid-key";
        public const string BeforeFaucetPresetId = "before-faucet";
        public const string BeforeParretPresetId = "before-parret";
        public const string BeforeBottleFillPresetId = "before-bottle-fill";

        public const string FaucetPresetCapabilityId = "kitchen.faucet.preset.before-faucet";
        public const string FaucetClickCapabilityId = "kitchen.faucet.click";
        public const string FaucetProbeCapabilityId = "kitchen.faucet.probe";
        public const string FaucetAssertClickedCapabilityId = "kitchen.faucet.assert-clicked";
        public const string FaucetCaptureCapabilityId = "kitchen.faucet.capture";
        public const string FaucetResetCapabilityId = "kitchen.faucet.reset";

        public const string SinkBeforeBottleFillPresetCapabilityId = "kitchen.sink.preset.before-bottle-fill";
        public const string SinkFillBottleCapabilityId = "kitchen.sink.fill-bottle";
        public const string KeyProbeCapabilityId = "kitchen.key.probe";
        public const string KeyClickCapabilityId = "kitchen.key.click";
        public const string ExitAssertCapabilityId = "kitchen.exit.assert";

        public const int BottleItemId = 1;
        public const int MaidRoomKeyItemId = 8;
        public const string MaidRoomKeyAlias = "maid-room-key";
        public const string BottleItemName = "Bottle";
        public const string MaidRoomKeyItemName = "MaidRoom_Key";

        private static readonly QaTargetId FaucetTargetId = QaTargetId.Create(FaucetTargetIdValue);
        private static readonly QaTargetId ParretTargetId = QaTargetId.Create(ParretTargetIdValue);

        private static readonly IReadOnlyCollection<QaTargetId> DeclaredTargetIds =
            new List<QaTargetId>
            {
                FaucetTargetId,
                ParretTargetId,
                QaTargetId.Create(SinkDropzoneTargetIdValue),
                QaTargetId.Create(MaidKeyTargetIdValue)
            };

        private static readonly IReadOnlyCollection<string> DeclaredPresetIds =
            new List<string> { BeforeFaucetPresetId, BeforeParretPresetId, BeforeBottleFillPresetId };

        public string SceneName
        {
            get { return SceneNames.Kitchen; }
        }

        public IReadOnlyCollection<QaTargetId> TargetIds
        {
            get { return DeclaredTargetIds; }
        }

        public IReadOnlyCollection<string> PresetIds
        {
            get { return DeclaredPresetIds; }
        }

        /// <summary>
        /// Registers Kitchen faucet + bottle/key exit developer capabilities.
        /// </summary>
        public static void RegisterCapabilities(DeveloperQaCapabilityRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            string sceneId = SceneNames.Kitchen;
            var adapter = new KitchenQaAdapter();

            registry.Register(
                new DeveloperQaCapability(
                    FaucetPresetCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Preset,
                    "{}",
                    "{faucetClicked:bool}"),
                _ => MapPreset(adapter.ApplyPreset(BeforeFaucetPresetId)));

            registry.Register(
                new DeveloperQaCapability(
                    FaucetResetCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Recovery,
                    "{}",
                    "{faucetClicked:bool}"),
                _ => MapPreset(adapter.ApplyPreset(BeforeFaucetPresetId)));

            registry.Register(
                new DeveloperQaCapability(
                    FaucetClickCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool}"),
                _ => MapClick(adapter, FaucetTargetId));

            registry.Register(
                new DeveloperQaCapability(
                    FaucetProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{faucetClicked:bool,bottleDragged:bool}"),
                _ => MapSnapshot(adapter, assertClicked: false));

            registry.Register(
                new DeveloperQaCapability(
                    FaucetCaptureCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{faucetClicked:bool,bottleDragged:bool}"),
                _ => MapSnapshot(adapter, assertClicked: false));

            registry.Register(
                new DeveloperQaCapability(
                    FaucetAssertClickedCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{faucetClicked:bool,bottleDragged:bool}"),
                _ => MapSnapshot(adapter, assertClicked: true));

            registry.Register(
                new DeveloperQaCapability(
                    SinkBeforeBottleFillPresetCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Preset,
                    "{}",
                    "{hasBottle:bool,bottleDragged:bool,faucetClicked:bool,haveMaidKey:bool}"),
                _ => HandleBeforeBottleFillPreset(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    SinkFillBottleCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{filled:bool,bottleDragged:bool}"),
                _ => HandleFillBottle(adapter));

            registry.Register(
                new DeveloperQaCapability(
                    KeyProbeCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Probe,
                    "{}",
                    "{maidKeyActive:bool,haveMaidKey:bool,bottleDragged:bool,faucetClicked:bool}"),
                _ => HandleKeyProbe());

            registry.Register(
                new DeveloperQaCapability(
                    KeyClickCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Interaction,
                    "{}",
                    "{clicked:bool,haveMaidKey:bool}"),
                _ => HandleKeyClick());

            registry.Register(
                new DeveloperQaCapability(
                    ExitAssertCapabilityId,
                    sceneId,
                    DeveloperQaCapabilityKind.Assertion,
                    "{}",
                    "{haveMaidKey:bool,inventoryHasMaidRoomKey:bool}"),
                _ => HandleExitAssert());
        }

        public QaScenePresetResult ApplyPreset(string presetId)
        {
            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState == null
                && !string.Equals(presetId, BeforeBottleFillPresetId, StringComparison.Ordinal))
            {
                return QaScenePresetResult.Failed(
                    "KitchenPuzzleState not found in the active scene. This preset only works " +
                    "while the Kitchen scene is the active Play Mode scene.");
            }

            if (string.Equals(presetId, BeforeFaucetPresetId, StringComparison.Ordinal))
            {
                puzzleState.SetFaucetClicked(false);
                return QaScenePresetResult.Success("Kitchen faucet baseline reset (FaucetClicked=false).");
            }

            if (string.Equals(presetId, BeforeParretPresetId, StringComparison.Ordinal))
            {
                puzzleState.SetParretClicked(false);
                return QaScenePresetResult.Success("Kitchen parret baseline reset (ParretClicked=false).");
            }

            if (string.Equals(presetId, BeforeBottleFillPresetId, StringComparison.Ordinal))
            {
                DeveloperQaResult result = HandleBeforeBottleFillPreset(this);
                if (result.Code == DeveloperQaResultCode.Ok)
                {
                    return QaScenePresetResult.Success(result.Message);
                }

                return QaScenePresetResult.Failed(
                    string.IsNullOrEmpty(result.Message)
                        ? "Kitchen before-bottle-fill preset failed."
                        : result.Message);
            }

            return QaScenePresetResult.UnknownPreset(presetId);
        }

        public QaSceneSnapshot CaptureSnapshot()
        {
            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState != null)
            {
                puzzleState.HydrateFromFungus();
            }

            var values = new Dictionary<string, string>
            {
                ["puzzleStateFound"] = (puzzleState != null).ToString(),
                ["faucetClicked"] = puzzleState != null ? puzzleState.FaucetClicked.ToString() : "unknown",
                ["bottleDragged"] = puzzleState != null ? puzzleState.BottleDragged.ToString() : "unknown",
                ["hasBottle"] = puzzleState != null ? puzzleState.HasBottle.ToString() : "unknown",
                ["comeParret"] = puzzleState != null ? puzzleState.ComeParret.ToString() : "unknown",
                ["parretClicked"] = puzzleState != null ? puzzleState.ParretClicked.ToString() : "unknown"
            };

            return QaSceneSnapshot.Create(SceneName, DateTime.UtcNow, values);
        }

        /// <summary>
        /// Resolves a Kitchen target id to a pointer-capable <see cref="GameObject"/> for RealInput.
        /// </summary>
        public static GameObject TryResolveTargetGameObject(QaTargetId targetId)
        {
            return KitchenQaTargetResolver.TryResolve(targetId);
        }

        public bool TryClick(QaTargetId targetId, out string error)
        {
            KitchenInteractionController controller = ResolveController();
            if (controller == null)
            {
                error = "KitchenInteractionController not found in the active scene. This adapter " +
                    "only works while the Kitchen scene is the active Play Mode scene.";
                return false;
            }

            if (targetId == FaucetTargetId)
            {
                controller.OnInteraction(KitchenSinkInteractionGate.FaucetInteractionId);
                error = null;
                return true;
            }

            if (targetId == ParretTargetId)
            {
                controller.OnInteraction(KitchenParretInteractionGate.ParretInteractionId);
                error = null;
                return true;
            }

            error = "KitchenQaAdapter does not own target '" + targetId + "'.";
            return false;
        }

        public bool TryDrag(QaTargetId sourceTargetId, QaTargetId destinationTargetId, out string error)
        {
            error = "KitchenQaAdapter does not support drag interactions for target '" + sourceTargetId + "'.";
            return false;
        }

        public bool TryKey(QaTargetId targetId, string text, out string error)
        {
            error = "KitchenQaAdapter does not support key interactions for target '" + targetId + "'.";
            return false;
        }

        private static DeveloperQaResult HandleBeforeBottleFillPreset(KitchenQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenQaAdapter instance is required for before-bottle-fill.");
            }

            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenPuzzleState not found; before-bottle-fill requires the Kitchen Play Mode scene.");
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "InventoryManager not found; cannot grant Bottle for before-bottle-fill.");
            }

            Item bottle = ItemLookup.FindById(BottleItemId);
            if (bottle == null)
            {
                bottle = FindInventoryItemByName(inventory, BottleItemName);
            }

            if (bottle == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "Bottle item (id 1) not found via ItemLookup; cannot grant Bottle.");
            }

            if (!InventoryContainsItemId(inventory, BottleItemId)
                && !InventoryContainsItemName(inventory, BottleItemName))
            {
                inventory.AddItem(bottle);
            }

            Flowchart flowchart = FlowchartLocator.Find();
            if (flowchart != null)
            {
                EnsureBoolean(flowchart, FungusVariableKeys.GetBottle, true);
                // Clear exit flag for a clean bottle→key run; do not force-set true for PASS.
                EnsureBoolean(flowchart, FungusVariableKeys.HaveMaidKey, false);
            }

            puzzleState.SetBottleDragged(false);
            puzzleState.SetFaucetClicked(false);
            puzzleState.HydrateFromFungus();

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Kitchen before-bottle-fill: Bottle ensured; BottleDragged/FaucetClicked/HaveMaidKey cleared.",
                data: BuildKeyProbeData(puzzleState, flowchart));
        }

        private static DeveloperQaResult HandleFillBottle(KitchenQaAdapter adapter)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenQaAdapter instance is required for fills-bottle.");
            }

            KitchenInteractionController controller = ResolveController();
            if (controller == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenInteractionController not found; fills-bottle requires Kitchen Play Mode.");
            }

            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState != null)
            {
                EnsureBottleFlagSynced(puzzleState);
            }

            if (!IsBottlePresentForFill(puzzleState))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "Bottle not present (GetBottle/inventory/drag). Apply kitchen.sink.preset.before-bottle-fill first.",
                    data: new Dictionary<string, string>
                    {
                        ["filled"] = "False",
                        ["bottleDragged"] = puzzleState != null
                            ? puzzleState.BottleDragged.ToString()
                            : "unknown"
                    });
            }

            controller.OnInteraction(KitchenSinkInteractionGate.BottleDragInteractionId);

            puzzleState = ResolvePuzzleState();
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Kitchen fills-bottle dispatched OnInteraction(\"bottle_drag\").",
                data: new Dictionary<string, string>
                {
                    ["filled"] = "True",
                    ["bottleDragged"] = puzzleState != null
                        ? puzzleState.BottleDragged.ToString()
                        : "unknown"
                });
        }

        private static DeveloperQaResult HandleKeyProbe()
        {
            KitchenPuzzleState puzzleState = ResolvePuzzleState();
            if (puzzleState != null)
            {
                puzzleState.HydrateFromFungus();
            }

            Flowchart flowchart = FlowchartLocator.Find();
            Dictionary<string, string> data = BuildKeyProbeData(puzzleState, flowchart);
            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Kitchen key probe snapshot captured.",
                data: data);
        }

        private static DeveloperQaResult HandleKeyClick()
        {
            GameObject keyGo = KitchenQaTargetResolver.TryResolve(
                QaTargetId.Create(MaidKeyTargetIdValue));
            if (keyGo == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "MaidRoomKey not active/resolvable. Wait for FaucetClicked∧BottleDragged spawn, or probe first.",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "False",
                        ["haveMaidKey"] = ReadHaveMaidKey().ToString()
                    });
            }

            ItemPickup pickup = keyGo.GetComponent<ItemPickup>();
            if (pickup == null)
            {
                pickup = keyGo.GetComponentInChildren<ItemPickup>();
            }

            if (pickup == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "MaidRoomKey GameObject has no ItemPickup; cannot use PickUpDirect public API.",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "False",
                        ["haveMaidKey"] = ReadHaveMaidKey().ToString()
                    });
            }

            // Documented public path (ContextMenu PickUpDirect) — does not force-set HaveMaidKey.
            pickup.PickUpDirect();

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Kitchen key click via ItemPickup.PickUpDirect on MaidRoomKey.",
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "True",
                    ["haveMaidKey"] = ReadHaveMaidKey().ToString()
                });
        }

        private static DeveloperQaResult HandleExitAssert()
        {
            Flowchart flowchart = FlowchartLocator.Find();
            InventoryManager inventory = InventoryManager.Instance;
            bool haveMaidKey = false;
            bool flowchartFound = flowchart != null;
            if (flowchartFound)
            {
                haveMaidKey = flowchart.GetBooleanVariable(FungusVariableKeys.HaveMaidKey);
            }

            bool inventoryHasKey = InventoryContainsItemId(inventory, MaidRoomKeyItemId)
                || InventoryContainsItemName(inventory, MaidRoomKeyItemName)
                || InventoryContainsAlias(inventory, MaidRoomKeyAlias);

            var data = new Dictionary<string, string>
            {
                ["flowchartFound"] = flowchartFound.ToString(),
                ["haveMaidKey"] = flowchartFound ? haveMaidKey.ToString() : "unknown",
                ["inventoryHasMaidRoomKey"] = inventoryHasKey.ToString(),
                ["maidRoomKeyItemId"] = MaidRoomKeyItemId.ToString(),
                ["maidRoomKeyAlias"] = MaidRoomKeyAlias
            };

            if (!flowchartFound && inventory == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "Cannot assert kitchen exit: Flowchart(Variablemanager) and InventoryManager both missing.",
                    data: data);
            }

            // Exit contract: HaveMaidKey true AND/OR inventory contains itemId 8 (maid-room-key).
            if (!haveMaidKey && !inventoryHasKey)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "Kitchen exit assert failed: HaveMaidKey is false and inventory lacks MaidRoom_Key (id 8).",
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                "Kitchen exit assert passed (HaveMaidKey and/or maid-room-key inventory).",
                data: data);
        }

        private static DeveloperQaResult MapPreset(QaScenePresetResult presetResult)
        {
            if (presetResult != null && presetResult.IsSuccess)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    presetResult.Message,
                    data: new Dictionary<string, string>
                    {
                        ["faucetClicked"] = "False"
                    });
            }

            string message = presetResult != null && !string.IsNullOrEmpty(presetResult.Message)
                ? presetResult.Message
                : "Kitchen faucet preset could not be applied.";
            return new DeveloperQaResult(DeveloperQaResultCode.EnvironmentBlocked, message);
        }

        private static DeveloperQaResult MapClick(KitchenQaAdapter adapter, QaTargetId targetId)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenQaAdapter instance is required for faucet click.");
            }

            string error;
            if (adapter.TryClick(targetId, out error))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.Ok,
                    "Kitchen faucet click dispatched.",
                    data: new Dictionary<string, string>
                    {
                        ["clicked"] = "True"
                    });
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.EnvironmentBlocked,
                string.IsNullOrEmpty(error)
                    ? "Kitchen faucet click blocked (Kitchen scene unavailable)."
                    : error,
                data: new Dictionary<string, string>
                {
                    ["clicked"] = "False"
                });
        }

        private static DeveloperQaResult MapSnapshot(KitchenQaAdapter adapter, bool assertClicked)
        {
            if (adapter == null)
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.EnvironmentBlocked,
                    "KitchenQaAdapter instance is required for faucet snapshot.");
            }

            QaSceneSnapshot snapshot = adapter.CaptureSnapshot();
            var data = new Dictionary<string, string>();
            if (snapshot != null && snapshot.Values != null)
            {
                foreach (KeyValuePair<string, string> pair in snapshot.Values)
                {
                    data[pair.Key] = pair.Value;
                }
            }

            string faucetClicked;
            if (!data.TryGetValue("faucetClicked", out faucetClicked))
            {
                faucetClicked = "unknown";
            }

            if (assertClicked &&
                !string.Equals(faucetClicked, bool.TrueString, StringComparison.Ordinal))
            {
                return new DeveloperQaResult(
                    DeveloperQaResultCode.AssertionFailed,
                    "Expected faucetClicked=True but was '" + faucetClicked + "'.",
                    data: data);
            }

            return new DeveloperQaResult(
                DeveloperQaResultCode.Ok,
                assertClicked
                    ? "Kitchen faucet assert-clicked passed."
                    : "Kitchen faucet snapshot captured.",
                data: data);
        }

        private static Dictionary<string, string> BuildKeyProbeData(
            KitchenPuzzleState puzzleState,
            Flowchart flowchart)
        {
            bool maidKeyActive = KitchenQaTargetResolver.TryResolve(
                    QaTargetId.Create(MaidKeyTargetIdValue))
                != null;
            string haveMaidKey = flowchart != null
                ? flowchart.GetBooleanVariable(FungusVariableKeys.HaveMaidKey).ToString()
                : "unknown";

            return new Dictionary<string, string>
            {
                ["maidKeyActive"] = maidKeyActive.ToString(),
                ["haveMaidKey"] = haveMaidKey,
                ["bottleDragged"] = puzzleState != null
                    ? puzzleState.BottleDragged.ToString()
                    : "unknown",
                ["faucetClicked"] = puzzleState != null
                    ? puzzleState.FaucetClicked.ToString()
                    : "unknown",
                ["hasBottle"] = puzzleState != null
                    ? puzzleState.HasBottle.ToString()
                    : "unknown"
            };
        }

        private static void EnsureBottleFlagSynced(KitchenPuzzleState puzzleState)
        {
            if (puzzleState == null)
            {
                return;
            }

            puzzleState.HydrateFromFungus();
            if (puzzleState.HasBottle || KitchenSinkInteractionGate.PlayerHasBottle(puzzleState))
            {
                return;
            }

            if (!InventoryContainsItemId(InventoryManager.Instance, BottleItemId)
                && !InventoryContainsItemName(InventoryManager.Instance, BottleItemName))
            {
                return;
            }

            Flowchart flowchart = FlowchartLocator.Find();
            if (flowchart != null)
            {
                EnsureBoolean(flowchart, FungusVariableKeys.GetBottle, true);
            }

            puzzleState.HydrateFromFungus();
        }

        private static bool IsBottlePresentForFill(KitchenPuzzleState puzzleState)
        {
            if (KitchenSinkInteractionGate.PlayerHasBottle(puzzleState))
            {
                return true;
            }

            if (puzzleState != null && puzzleState.HasBottle)
            {
                return true;
            }

            return InventoryContainsItemId(InventoryManager.Instance, BottleItemId)
                || InventoryContainsItemName(InventoryManager.Instance, BottleItemName);
        }

        private static bool ReadHaveMaidKey()
        {
            Flowchart flowchart = FlowchartLocator.Find();
            return flowchart != null && flowchart.GetBooleanVariable(FungusVariableKeys.HaveMaidKey);
        }

        private static void EnsureBoolean(Flowchart flowchart, string key, bool value)
        {
            if (flowchart == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!flowchart.HasVariable(key))
            {
                var variable = flowchart.gameObject.AddComponent<BooleanVariable>();
                variable.Key = key;
                variable.Scope = VariableScope.Public;
                variable.Value = value;
                flowchart.Variables.Add(variable);
                return;
            }

            flowchart.SetBooleanVariable(key, value);
        }

        private static bool InventoryContainsItemId(InventoryManager inventory, int itemId)
        {
            if (inventory == null || inventory.Items == null)
            {
                return false;
            }

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                Item item = inventory.Items[i];
                if (item != null && item.itemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool InventoryContainsItemName(InventoryManager inventory, string itemName)
        {
            return FindInventoryItemByName(inventory, itemName) != null;
        }

        private static bool InventoryContainsAlias(InventoryManager inventory, string alias)
        {
            if (string.Equals(alias, MaidRoomKeyAlias, StringComparison.OrdinalIgnoreCase))
            {
                return InventoryContainsItemId(inventory, MaidRoomKeyItemId)
                    || InventoryContainsItemName(inventory, MaidRoomKeyItemName);
            }

            return false;
        }

        private static Item FindInventoryItemByName(InventoryManager inventory, string itemName)
        {
            if (inventory == null || inventory.Items == null || string.IsNullOrEmpty(itemName))
            {
                return null;
            }

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                Item item = inventory.Items[i];
                if (item != null
                    && string.Equals(item.itemName, itemName, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static KitchenInteractionController ResolveController()
        {
            return UnityEngine.Object.FindFirstObjectByType<KitchenInteractionController>();
        }

        private static KitchenPuzzleState ResolvePuzzleState()
        {
            return UnityEngine.Object.FindFirstObjectByType<KitchenPuzzleState>();
        }
    }
}
#endif
