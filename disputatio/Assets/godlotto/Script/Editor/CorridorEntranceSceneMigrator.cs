using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using ObjectClickedHandler = Fungus.ObjectClicked;

public static class CorridorEntranceSceneMigrator
{
    const string ControllerTypeName = "CorridorEntranceController, Assembly-CSharp";

    static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/Mokotan/First Floor/Hall_playerble.unity",
        "Assets/Scenes/Mokotan/First Floor/1floorRight/PrisonEntrance.unity",
        "Assets/Scenes/Mokotan/First Floor/1floorRight/MaidEntrance.unity",
        "Assets/Scenes/Mokotan/First Floor/1floorRight/StudyEntrance.unity",
        "Assets/Scenes/Mokotan/Second Floor/BedEntrance.unity",
        "Assets/Scenes/Mokotan/Second Floor/ChildEntrance.unity",
        "Assets/Scenes/Mokotan/Second Floor/TutorEntrance.unity",
        "Assets/Scenes/Mokotan/Second Floor/WifeEntrance.unity",
    };

    static readonly string[] EnterBlockNames =
    {
        "EnterWifeRoom",
        "EnterTutorRoom",
        "EnterChildRoom",
        "EnterBedRoom",
        "EnterMaidRoom",
        "EnterStudyRoom",
        "EnterPrison",
        "EnterBasement",
    };

    [MenuItem("Tools/godlotto/Migrate Corridor Entrance Scenes")]
    public static void MigrateAllTargetScenes()
    {
        int changed = 0;
        foreach (string scenePath in TargetScenePaths)
        {
            try
            {
                if (MigrateScene(scenePath))
                    changed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CorridorEntranceSceneMigrator] Failed to migrate '{scenePath}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CorridorEntranceSceneMigrator] Migrated {changed}/{TargetScenePaths.Length} scene(s).");
        VerifyTargetScenes();
    }

    [MenuItem("Tools/godlotto/Verify Corridor Entrance Migration")]
    public static void VerifyTargetScenes()
    {
        int violations = 0;
        foreach (string scenePath in TargetScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            violations += CountDirectEnterBlockExecuteCalls(scene);
        }

        if (violations == 0)
            Debug.Log("[CorridorEntranceSceneMigrator] Verification passed: no direct ExecuteBlock calls to Enter* blocks.");
        else
            Debug.LogError($"[CorridorEntranceSceneMigrator] Verification failed: {violations} direct ExecuteBlock call(s) remain.");
    }

    static bool MigrateScene(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string sceneName = scene.name;
        if (!SceneMigrationRegistry.TryGet(sceneName, out SceneMigrationSpec spec))
        {
            Debug.LogWarning($"[CorridorEntranceSceneMigrator] No spec for scene '{sceneName}' ({scenePath}).");
            return false;
        }

        Flowchart flowchart = FindSceneComponents<Flowchart>(scene)
            .FirstOrDefault(fc => fc.GetComponents<Block>().Length > 0);
        if (flowchart == null)
        {
            Debug.LogError($"[CorridorEntranceSceneMigrator] Flowchart not found in {scenePath}");
            return false;
        }

        var controller = flowchart.GetComponent<CorridorEntranceController>();
        if (controller == null)
            controller = flowchart.gameObject.AddComponent<CorridorEntranceController>();

        BackNavigator backNavigator = FindSceneComponents<BackNavigator>(scene).FirstOrDefault();

        WorldClickTarget resolvedBackTarget = TryResolveBackWorldClick(flowchart, scene);
        bool backInteractionWired = CanWireBackInteraction(spec, resolvedBackTarget, scene);

        ApplyControllerConfig(controller, flowchart, backNavigator, spec, scene, resolvedBackTarget);
        DisconnectObjectClickedHandlers(flowchart, BuildDisconnectBlockNames(spec, backInteractionWired));
        DisableLoadSceneInBlocks(flowchart, spec.DisableLoadSceneBlockNames);
        DisableGoBackCommandsInBlocks(flowchart, spec.DisableGoBackInvokeBlockNames);
        RewireDropZoneUnlockEvents(scene, controller, spec.UnlockInteractionId, spec.UnlockBlockName);
        RewireUiExecuteBlockCalls(scene, controller, spec.UiExecuteBlockRewires);
        if (spec.RewireRibbonBackToConfirmationMenu)
            RewireRibbonBackButtons(scene, controller);

        if (!backInteractionWired && HasRoute(spec, "back"))
        {
            Debug.LogWarning(
                $"[CorridorEntranceSceneMigrator] {sceneName}: back route kept but BackSpace_Clicked handler was not disconnected (no collider/ribbon wiring).");
        }

        EditorUtility.SetDirty(flowchart.gameObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CorridorEntranceSceneMigrator] Migrated {sceneName}");
        return true;
    }

    static void ApplyControllerConfig(
        CorridorEntranceController controller,
        Flowchart flowchart,
        BackNavigator backNavigator,
        SceneMigrationSpec spec,
        UnityEngine.SceneManagement.Scene scene,
        WorldClickTarget resolvedBackTarget)
    {
        var so = new SerializedObject(controller);
        so.FindProperty("flowchart").objectReferenceValue = flowchart;
        so.FindProperty("backNavigator").objectReferenceValue = backNavigator;
        so.FindProperty("enableDebugLogging").boolValue = false;

        WorldClickTarget[] worldClickTargets = MergeWorldClickTargets(spec.WorldClickTargets, resolvedBackTarget);
        var worldClicks = so.FindProperty("worldClicks");
        worldClicks.arraySize = worldClickTargets.Length;
        for (int i = 0; i < worldClickTargets.Length; i++)
        {
            WorldClickTarget target = worldClickTargets[i];
            SerializedProperty element = worldClicks.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = target.InteractionId;

            GameObject go = FindGameObjectInScene(scene, target.GameObjectName);
            Collider2D collider = go != null ? go.GetComponent<Collider2D>() : null;
            Clickable2D clickable = go != null ? go.GetComponent<Clickable2D>() : null;
            element.FindPropertyRelative("collider").objectReferenceValue = collider;
            element.FindPropertyRelative("clickable").objectReferenceValue = clickable;

            if (go == null || (collider == null && clickable == null))
            {
                Debug.LogWarning(
                    $"[CorridorEntranceSceneMigrator] Missing collider/clickable for '{target.GameObjectName}' ({target.InteractionId}) in {scene.name}.");
            }
        }

        WriteRoutes(so.FindProperty("routes"), spec.Routes);
        WriteOutcomes(so.FindProperty("blockOutcomes"), spec.Outcomes);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static WorldClickTarget[] MergeWorldClickTargets(WorldClickTarget[] configured, WorldClickTarget resolvedBackTarget)
    {
        if (resolvedBackTarget == null)
            return configured;

        var merged = new List<WorldClickTarget>();
        bool replacedBack = false;
        foreach (WorldClickTarget target in configured)
        {
            if (target.InteractionId == "back")
            {
                merged.Add(resolvedBackTarget);
                replacedBack = true;
                continue;
            }

            merged.Add(target);
        }

        if (!replacedBack)
            merged.Add(resolvedBackTarget);

        return merged.ToArray();
    }

    static void WriteRoutes(SerializedProperty routesProp, InteractionRouteSpec[] routes)
    {
        routesProp.arraySize = routes.Length;
        for (int i = 0; i < routes.Length; i++)
        {
            SerializedProperty element = routesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = routes[i].InteractionId;
            element.FindPropertyRelative("fungusBlockName").stringValue = routes[i].BlockName;
        }
    }

    static void WriteOutcomes(SerializedProperty outcomesProp, BlockOutcomeSpec[] outcomes)
    {
        outcomesProp.arraySize = outcomes.Length;
        for (int i = 0; i < outcomes.Length; i++)
        {
            BlockOutcomeSpec outcome = outcomes[i];
            SerializedProperty element = outcomesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("blockName").stringValue = outcome.BlockName;
            element.FindPropertyRelative("resetIsClicked").boolValue = outcome.ResetIsClicked;
            element.FindPropertyRelative("loadScene").boolValue = outcome.LoadScene;
            element.FindPropertyRelative("sceneName").stringValue = outcome.SceneName ?? string.Empty;
            element.FindPropertyRelative("goBack").boolValue = outcome.GoBack;
        }
    }

    static void DisconnectObjectClickedHandlers(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
            return;

        var blockNameSet = new HashSet<string>(blockNames);
        foreach (Block block in flowchart.GetComponents<Block>())
        {
            if (!blockNameSet.Contains(block.BlockName))
                continue;

            block._EventHandler = null;
            EditorUtility.SetDirty(block);

            foreach (ObjectClickedHandler handler in flowchart.GetComponents<ObjectClickedHandler>())
            {
                if (!handler || GetParentBlock(handler) != block)
                    continue;

                handler.enabled = false;
                EditorUtility.SetDirty(handler);
            }

            foreach (MonoBehaviour handler in flowchart.GetComponents<MonoBehaviour>())
            {
                if (!handler || handler.GetType().Name != "GuardedObjectClicked")
                    continue;
                if (GetParentBlock(handler) != block)
                    continue;

                handler.enabled = false;
                EditorUtility.SetDirty(handler);
            }
        }
    }

    static Block GetParentBlock(MonoBehaviour handler)
    {
        if (!handler)
            return null;

        var so = new SerializedObject(handler);
        return so.FindProperty("parentBlock").objectReferenceValue as Block;
    }

    static void DisableLoadSceneInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null)
            return;

        var targets = new HashSet<string>(blockNames);
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (!IsLiveSceneObject(command, flowchart.gameObject.scene) || command.ParentBlock == null)
                continue;
            if (!targets.Contains(command.ParentBlock.BlockName))
                continue;
            if (command.GetType().Name != "LoadScene")
                continue;

            SetComponentEnabled(command, false);
        }
    }

    static void DisableGoBackCommandsInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null)
            return;

        var targets = new HashSet<string>(blockNames);
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (!IsLiveSceneObject(command, flowchart.gameObject.scene) || command.ParentBlock == null)
                continue;
            if (!targets.Contains(command.ParentBlock.BlockName))
                continue;
            if (!IsGoBackCommand(command))
                continue;

            SetComponentEnabled(command, false);
        }
    }

    static bool IsGoBackCommand(Command command)
    {
        if (!command)
            return false;

        try
        {
            string typeName = command.GetType().Name;
            var so = new SerializedObject(command);

            if (typeName == "CallMethod")
            {
                SerializedProperty methodName = so.FindProperty("methodName");
                return methodName != null && methodName.stringValue == "GoBack";
            }

            if (typeName == "InvokeMethod")
            {
                SerializedProperty targetMethod = so.FindProperty("targetMethod");
                return targetMethod != null && targetMethod.stringValue == "GoBack";
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    static WorldClickTarget TryResolveBackWorldClick(Flowchart flowchart, UnityEngine.SceneManagement.Scene scene)
    {
        Block backBlock = flowchart.GetComponents<Block>()
            .FirstOrDefault(block => block.BlockName == "BackSpace_Clicked");
        if (backBlock == null)
            return null;

        foreach (ObjectClickedHandler handler in flowchart.GetComponents<ObjectClickedHandler>())
        {
            if (GetParentBlock(handler) != backBlock)
                continue;

            var handlerSo = new SerializedObject(handler);
            Clickable2D clickable = handlerSo.FindProperty("clickableObject").objectReferenceValue as Clickable2D;
            if (clickable == null)
                continue;

            return new WorldClickTarget
            {
                InteractionId = "back",
                GameObjectName = clickable.gameObject.name,
            };
        }

        foreach (MonoBehaviour handler in flowchart.GetComponents<MonoBehaviour>())
        {
            if (handler == null || handler.GetType().Name != "GuardedObjectClicked")
                continue;
            if (GetParentBlock(handler) != backBlock)
                continue;

            var handlerSo = new SerializedObject(handler);
            Clickable2D clickable = handlerSo.FindProperty("clickableObject").objectReferenceValue as Clickable2D;
            if (clickable == null)
                continue;

            return new WorldClickTarget
            {
                InteractionId = "back",
                GameObjectName = clickable.gameObject.name,
            };
        }

        foreach (string candidate in BackClickObjectNameCandidates)
        {
            GameObject go = FindGameObjectInScene(scene, candidate);
            if (go == null)
                continue;
            if (go.GetComponent<Collider2D>() == null && go.GetComponent<Clickable2D>() == null)
                continue;

            return new WorldClickTarget { InteractionId = "back", GameObjectName = candidate };
        }

        return null;
    }

    static readonly string[] BackClickObjectNameCandidates =
    {
        "BackSpace",
        "Backspace",
        "BackspaceNameplate",
        "Back_Space",
    };

    static bool CanWireBackInteraction(SceneMigrationSpec spec, WorldClickTarget resolvedBackTarget, UnityEngine.SceneManagement.Scene scene)
    {
        if (resolvedBackTarget != null)
            return true;

        if (!spec.RewireRibbonBackToConfirmationMenu)
            return false;

        return FindSceneComponents<BackNavigator>(scene).Length > 0;
    }

    static string[] BuildDisconnectBlockNames(SceneMigrationSpec spec, bool backInteractionWired)
    {
        var names = new List<string>(spec.DisconnectBlockNames);
        if (!backInteractionWired)
            names.RemoveAll(name => name == "BackSpace_Clicked");
        else if (HasRoute(spec, "back") && !names.Contains("BackSpace_Clicked"))
            names.Add("BackSpace_Clicked");

        return names.ToArray();
    }

    static bool HasRoute(SceneMigrationSpec spec, string interactionId) =>
        spec.Routes.Any(route => route.InteractionId == interactionId);

    static void SetComponentEnabled(Component component, bool enabled)
    {
        if (!component)
            return;

        try
        {
            var so = new SerializedObject(component);
            SerializedProperty enabledProp = so.FindProperty("m_Enabled");
            if (enabledProp == null)
                return;

            enabledProp.boolValue = enabled;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[CorridorEntranceSceneMigrator] Could not toggle '{component.GetType().Name}' on '{component.gameObject.name}': {ex.Message}");
        }
    }

    static void RewireDropZoneUnlockEvents(
        UnityEngine.SceneManagement.Scene scene,
        CorridorEntranceController controller,
        string interactionId,
        string unlockBlockName)
    {
        if (string.IsNullOrWhiteSpace(interactionId) || string.IsNullOrWhiteSpace(unlockBlockName))
            return;

        foreach (WorldItemDropZone dropZone in FindSceneComponents<WorldItemDropZone>(scene))
        {
            if (!DropZoneUnlockTargetsBlock(dropZone, unlockBlockName))
                continue;

            var dropZoneSo = new SerializedObject(dropZone);
            SetPersistentStringUnityEvent(
                dropZoneSo,
                "onUnlock",
                controller,
                nameof(CorridorEntranceController.OnInteraction),
                interactionId);
            dropZoneSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dropZone);
        }
    }

    static bool DropZoneUnlockTargetsBlock(WorldItemDropZone dropZone, string unlockBlockName)
    {
        var so = new SerializedObject(dropZone);
        SerializedProperty calls = so.FindProperty("onUnlock.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                continue;
            if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue != unlockBlockName)
                continue;

            return true;
        }

        return false;
    }

    static void RewireRibbonBackButtons(
        UnityEngine.SceneManagement.Scene scene,
        CorridorEntranceController controller)
    {
        foreach (BackNavigator navigator in FindSceneComponents<BackNavigator>(scene))
        {
            foreach (UnityEngine.UI.Button button in navigator.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (button.onClick.GetPersistentTarget(i) is not BackNavigator)
                        continue;
                    if (button.onClick.GetPersistentMethodName(i) != nameof(BackNavigator.GoBack))
                        continue;

                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }

                var buttonSo = new SerializedObject(button);
                SetPersistentStringUnityEvent(
                    buttonSo,
                    "m_OnClick",
                    controller,
                    nameof(CorridorEntranceController.OnInteraction),
                    "back");
                buttonSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(button);
            }
        }
    }

    static void SetPersistentStringUnityEvent(
        SerializedObject owner,
        string unityEventPropertyPath,
        UnityEngine.Object target,
        string methodName,
        string stringArg)
    {
        SerializedProperty unityEventProperty = owner.FindProperty(unityEventPropertyPath);
        SerializedProperty calls = unityEventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");
        calls.ClearArray();
        calls.arraySize = 1;

        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = ControllerTypeName;
        call.FindPropertyRelative("m_MethodName").stringValue = methodName;
        call.FindPropertyRelative("m_Mode").enumValueIndex = 5;
        call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = stringArg;
        call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
    }

    static void RewireUiExecuteBlockCalls(
        UnityEngine.SceneManagement.Scene scene,
        CorridorEntranceController controller,
        UiExecuteBlockRewire[] rewires)
    {
        if (rewires == null || rewires.Length == 0)
            return;

        foreach (UiExecuteBlockRewire rewire in rewires)
        {
            GameObject go = FindGameObjectInScene(scene, rewire.GameObjectName);
            if (go == null)
                continue;

            foreach (UnityEngine.UI.Button button in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var buttonSo = new SerializedObject(button);
                SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                for (int i = calls.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                        continue;

                    string blockArg = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                    if (blockArg != rewire.OldBlockName)
                        continue;

                    calls.DeleteArrayElementAtIndex(i);
                    SetPersistentStringUnityEvent(
                        buttonSo,
                        "m_OnClick",
                        controller,
                        nameof(CorridorEntranceController.OnInteraction),
                        rewire.InteractionId);
                    buttonSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(button);
                }
            }
        }
    }

    static int CountDirectEnterBlockExecuteCalls(UnityEngine.SceneManagement.Scene scene)
    {
        int count = 0;
        var enterBlocks = new HashSet<string>(EnterBlockNames);

        foreach (WorldItemDropZone dropZone in FindSceneComponents<WorldItemDropZone>(scene))
        {
            count += CountExecuteBlockCallsInUnityEvent(
                new SerializedObject(dropZone),
                "onUnlock",
                enterBlocks,
                scene.name,
                dropZone);
        }

        foreach (UnityEngine.UI.Button button in FindSceneComponents<UnityEngine.UI.Button>(scene))
        {
            count += CountExecuteBlockCallsInUnityEvent(
                new SerializedObject(button),
                "m_OnClick",
                enterBlocks,
                scene.name,
                button);
        }

        return count;
    }

    static T[] FindSceneComponents<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(component => component != null && component.gameObject != null && component.gameObject.scene == scene)
            .ToArray();

    static bool IsLiveSceneObject(Component component, UnityEngine.SceneManagement.Scene scene) =>
        component != null && component.gameObject != null && component.gameObject.scene == scene;

    static int CountExecuteBlockCallsInUnityEvent(
        SerializedObject owner,
        string unityEventPropertyPath,
        HashSet<string> enterBlocks,
        string sceneName,
        UnityEngine.Object context)
    {
        SerializedProperty calls = owner.FindProperty(unityEventPropertyPath)?.FindPropertyRelative("m_PersistentCalls.m_Calls");
        if (calls == null)
            return 0;

        int count = 0;
        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                continue;

            string blockArg = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
            if (!enterBlocks.Contains(blockArg))
                continue;

            count++;
            Debug.LogError(
                $"[CorridorEntranceSceneMigrator] {sceneName}: {context.GetType().Name} on '{((Component)context).gameObject.name}' still calls ExecuteBlock('{blockArg}').",
                context);
        }

        return count;
    }

    static GameObject FindGameObjectInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }

        return null;
    }

    sealed class SceneMigrationSpec
    {
        public WorldClickTarget[] WorldClickTargets = Array.Empty<WorldClickTarget>();
        public InteractionRouteSpec[] Routes = Array.Empty<InteractionRouteSpec>();
        public BlockOutcomeSpec[] Outcomes = Array.Empty<BlockOutcomeSpec>();
        public string[] DisconnectBlockNames = Array.Empty<string>();
        public string[] DisableLoadSceneBlockNames = Array.Empty<string>();
        public string[] DisableGoBackInvokeBlockNames = Array.Empty<string>();
        public string UnlockInteractionId;
        public string UnlockBlockName;
        public UiExecuteBlockRewire[] UiExecuteBlockRewires = Array.Empty<UiExecuteBlockRewire>();
        public bool RewireRibbonBackToConfirmationMenu;
    }

    sealed class WorldClickTarget
    {
        public string InteractionId;
        public string GameObjectName;
    }

    sealed class InteractionRouteSpec
    {
        public string InteractionId;
        public string BlockName;
    }

    sealed class BlockOutcomeSpec
    {
        public string BlockName;
        public bool ResetIsClicked;
        public bool LoadScene;
        public string SceneName;
        public bool GoBack;
    }

    sealed class UiExecuteBlockRewire
    {
        public string GameObjectName;
        public string OldBlockName;
        public string InteractionId;
    }

    static class SceneMigrationRegistry
    {
        static readonly Dictionary<string, SceneMigrationSpec> Specs = BuildSpecs();

        public static bool TryGet(string sceneName, out SceneMigrationSpec spec) => Specs.TryGetValue(sceneName, out spec);

        static Dictionary<string, SceneMigrationSpec> BuildSpecs()
        {
            var specs = new Dictionary<string, SceneMigrationSpec>(StringComparer.Ordinal);

            specs["StudyEntrance"] = BuildRoomEntrance(
                doorObject: "Study_Door",
                doorBlock: "StudyRoom_Clicked",
                unlockBlock: "EnterStudyRoom",
                roomScene: SceneNames.StudyRoom,
                goYesBlock: "GoStudyRoom_Yes",
                goNoBlock: "GoStudyRoom_No");

            specs["MaidEntrance"] = BuildRoomEntrance(
                doorObject: "MaidRoom_Door",
                doorBlock: "MaidRoom_Clicked",
                unlockBlock: "EnterMaidRoom",
                roomScene: SceneNames.MaidRoom,
                goYesBlock: "GoMaidRoom_Yes",
                goNoBlock: "GoMadeRoom_No",
                extraOutcomes: new[]
                {
                    Outcome("selectYes", goBack: true),
                    Outcome("selectNo", reset: true),
                },
                disableGoBackInvokeBlockNames: new[] { "selectYes" });

            specs["BedEntrance"] = BuildSecondFloorEntrance("Bed_Door", "BedRoom_Clicked", "EnterBedRoom", SceneNames.BedRoom);
            specs["ChildEntrance"] = BuildSecondFloorEntrance("Child_Door", "ChildRoom_Clicked", "EnterChildRoom", SceneNames.ChildRoom);
            specs["TutorEntrance"] = BuildSecondFloorEntrance("TutorRoom_Door", "TutorRoom_Clicked", "EnterTutorRoom", SceneNames.TutorRoom);
            specs["WifeEntrance"] = BuildSecondFloorEntrance("Wife_Door", "WifeRoom_Clicked", "EnterWifeRoom", SceneNames.WifeRoom);

            specs["PrisonEntrance"] = new SceneMigrationSpec
            {
                WorldClickTargets = new[]
                {
                    Target("lock", "Lock"),
                },
                Routes = new[]
                {
                    Route("lock", "Lock_Clicked"),
                    Route("back", "BackSpace_Clicked"),
                    Route("unlock", "EnterPrison"),
                },
                Outcomes = new[]
                {
                    Outcome("StudyRoom", load: true, scene: SceneNames.StudyRoom),
                    Outcome("Hall_RightCross", load: true, scene: "Hall_RightCross"),
                    Outcome("GoPrison_Yes", load: true, scene: "Prison"),
                    Outcome("Enter_Yes", load: true, scene: "Prison"),
                    Outcome("GoPrison_No", reset: true),
                    Outcome("Enter_No", reset: true),
                    Outcome("SelectNo", reset: true),
                },
                DisconnectBlockNames = new[] { "Lock_Clicked" },
                DisableLoadSceneBlockNames = new[] { "StudyRoom", "Hall_RightCross", "GoPrison_Yes", "Enter_Yes" },
                UnlockInteractionId = "unlock",
                UnlockBlockName = "EnterPrison",
                RewireRibbonBackToConfirmationMenu = true,
            };

            specs["Hall_playerble"] = new SceneMigrationSpec
            {
                WorldClickTargets = new[]
                {
                    Target("right", "Go_Right"),
                    Target("left", "Go_Left"),
                    Target("stair", "Go_2floor"),
                    Target("basement", "BasementDoor"),
                    Target("map", "FeildMap"),
                },
                Routes = new[]
                {
                    Route("right", "Right_Clicked"),
                    Route("left", "Left_Clicked"),
                    Route("stair", "stair_Clicked"),
                    Route("basement", "BasementDoor_Clicked"),
                    Route("map", "Map"),
                    Route("unlock", "EnterBasement"),
                },
                Outcomes = new[]
                {
                    Outcome("Right_Clicked", load: true, scene: SceneNames.HallRight),
                    Outcome("Left_Clicked", load: true, scene: "Hall_Left"),
                    Outcome("Yes", load: true, scene: "2floorMainHall"),
                    Outcome("IsPlayedAnimation", load: true, scene: "Hall_animate"),
                    Outcome("selectYes", load: true, scene: "BetaEnd"),
                    Outcome("EnterYes", load: true, scene: "BetaEnd"),
                    Outcome("selectNo", reset: true),
                    Outcome("EnterNo", reset: true),
                    Outcome("No", reset: true),
                },
                DisconnectBlockNames = new[] { "Right_Clicked", "Left_Clicked", "stair_Clicked", "BasementDoor_Clicked", "Map" },
                DisableLoadSceneBlockNames = new[] { "Right_Clicked", "Left_Clicked", "Yes", "IsPlayedAnimation", "selectYes", "EnterYes" },
                UnlockInteractionId = "unlock",
                UnlockBlockName = "EnterBasement",
            };

            return specs;
        }

        static SceneMigrationSpec BuildRoomEntrance(
            string doorObject,
            string doorBlock,
            string unlockBlock,
            string roomScene,
            string goYesBlock,
            string goNoBlock,
            BlockOutcomeSpec[] extraOutcomes = null,
            string[] disableGoBackInvokeBlockNames = null)
        {
            var outcomes = new List<BlockOutcomeSpec>
            {
                Outcome(goYesBlock, load: true, scene: roomScene),
                Outcome("EnterYes", load: true, scene: roomScene),
                Outcome(goNoBlock, reset: true),
                Outcome("EnterNo", reset: true),
            };
            if (extraOutcomes != null)
                outcomes.AddRange(extraOutcomes);

            return new SceneMigrationSpec
            {
                WorldClickTargets = new[] { Target("door", doorObject) },
                Routes = new[]
                {
                    Route("door", doorBlock),
                    Route("unlock", unlockBlock),
                },
                Outcomes = outcomes.ToArray(),
                DisconnectBlockNames = new[] { doorBlock },
                DisableLoadSceneBlockNames = new[] { goYesBlock, "EnterYes" },
                DisableGoBackInvokeBlockNames = disableGoBackInvokeBlockNames ?? Array.Empty<string>(),
                UnlockInteractionId = "unlock",
                UnlockBlockName = unlockBlock,
            };
        }

        static SceneMigrationSpec BuildSecondFloorEntrance(
            string doorObject,
            string doorBlock,
            string unlockBlock,
            string roomScene)
        {
            return new SceneMigrationSpec
            {
                WorldClickTargets = new[] { Target("door", doorObject) },
                Routes = new[]
                {
                    Route("door", doorBlock),
                    Route("unlock", unlockBlock),
                    Route("back", "BackSpace_Clicked"),
                },
                Outcomes = new[]
                {
                    Outcome("Go_Yes", load: true, scene: roomScene),
                    Outcome("EnterYes", load: true, scene: roomScene),
                    Outcome("Go_No", reset: true),
                    Outcome("EnterNo", reset: true),
                    Outcome("Select_No", reset: true),
                    Outcome("Select_Yes", goBack: true),
                },
                DisconnectBlockNames = new[] { doorBlock },
                DisableLoadSceneBlockNames = new[] { "Go_Yes", "EnterYes" },
                DisableGoBackInvokeBlockNames = new[] { "Select_Yes" },
                UnlockInteractionId = "unlock",
                UnlockBlockName = unlockBlock,
                RewireRibbonBackToConfirmationMenu = true,
            };
        }

        static WorldClickTarget Target(string interactionId, string gameObjectName) =>
            new WorldClickTarget { InteractionId = interactionId, GameObjectName = gameObjectName };

        static InteractionRouteSpec Route(string interactionId, string blockName) =>
            new InteractionRouteSpec { InteractionId = interactionId, BlockName = blockName };

        static BlockOutcomeSpec Outcome(
            string blockName,
            bool reset = false,
            bool load = false,
            string scene = null,
            bool goBack = false) =>
            new BlockOutcomeSpec
            {
                BlockName = blockName,
                ResetIsClicked = reset,
                LoadScene = load,
                SceneName = scene,
                GoBack = goBack,
            };
    }
}
