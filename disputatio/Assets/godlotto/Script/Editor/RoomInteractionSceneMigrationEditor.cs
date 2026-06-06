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

/// <summary>
/// 룸 씬 Fungus 이관용 공통 에디터 유틸 (CorridorEntranceSceneMigrator와 대칭 API).
/// </summary>
public static class RoomInteractionSceneMigrationEditor
{
    public const string RoomControllerTypeName = "Godlotto.Interaction.RoomInteractionController, Assembly-CSharp";
    public const string BedRoomControllerTypeName = "Godlotto.Interaction.BedRoomInteractionController, Assembly-CSharp";

    public static void WriteRoutes(SerializedProperty routesProp, InteractionRouteSpec[] routes)
    {
        routesProp.arraySize = routes.Length;
        for (int i = 0; i < routes.Length; i++)
        {
            SerializedProperty element = routesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = routes[i].InteractionId;
            element.FindPropertyRelative("fungusBlockName").stringValue = routes[i].BlockName;
        }
    }

    public static void WriteOutcomes(SerializedProperty outcomesProp, BlockOutcomeSpec[] outcomes)
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

    public static void WritePanelCloses(SerializedProperty panelClosesProp, PanelCloseSpec[] panelCloses)
    {
        panelClosesProp.arraySize = panelCloses.Length;
        for (int i = 0; i < panelCloses.Length; i++)
        {
            PanelCloseSpec spec = panelCloses[i];
            SerializedProperty element = panelClosesProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("panelCloseId").stringValue = spec.PanelCloseId;
            element.FindPropertyRelative("panel").objectReferenceValue = spec.Panel;
        }
    }

    public static void WirePanelBackspaceCloser(
        PanelBackspaceCloser closer,
        RoomInteractionController controller,
        string panelCloseInteractionId)
    {
        if (closer == null || controller == null)
            return;

        var so = new SerializedObject(closer);
        so.FindProperty("interactionController").objectReferenceValue = controller;
        so.FindProperty("panelCloseInteractionId").stringValue = panelCloseInteractionId ?? string.Empty;
        so.FindProperty("executeBlockName").stringValue = string.Empty;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(closer);
    }

    public static void ApplyWorldClickBindings(
        SerializedProperty worldClicksProp,
        UnityEngine.SceneManagement.Scene scene,
        WorldClickTarget[] targets,
        string sceneLabel)
    {
        worldClicksProp.arraySize = targets.Length;
        for (int i = 0; i < targets.Length; i++)
        {
            WorldClickTarget target = targets[i];
            SerializedProperty element = worldClicksProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("interactionId").stringValue = target.InteractionId;

            GameObject go = FindGameObjectInScene(scene, target.GameObjectName);
            Collider2D collider = go != null ? go.GetComponent<Collider2D>() : null;
            Clickable2D clickable = go != null ? go.GetComponent<Clickable2D>() : null;
            element.FindPropertyRelative("collider").objectReferenceValue = collider;
            element.FindPropertyRelative("clickable").objectReferenceValue = clickable;

            if (go == null || (collider == null && clickable == null))
            {
                Debug.LogWarning(
                    $"[RoomMigration] {sceneLabel}: missing collider/clickable for '{target.GameObjectName}' ({target.InteractionId}).");
            }
        }
    }

    public static void DisconnectObjectClickedHandlers(Flowchart flowchart, string[] blockNames)
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
                if (!IsAlive(handler))
                    continue;
                if (GetParentBlock(handler) != block)
                    continue;

                handler.enabled = false;
                EditorUtility.SetDirty(handler);
            }

            foreach (MonoBehaviour handler in flowchart.GetComponents<MonoBehaviour>())
            {
                if (!IsAlive(handler) || handler.GetType().Name != "GuardedObjectClicked")
                    continue;
                if (GetParentBlock(handler) != block)
                    continue;

                handler.enabled = false;
                EditorUtility.SetDirty(handler);
            }
        }
    }

    public static void DisconnectFungusButtonClickedHandlers(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
            return;

        var blockNameSet = new HashSet<string>(blockNames);
        foreach (MonoBehaviour handler in flowchart.GetComponents<MonoBehaviour>())
        {
            if (!IsAlive(handler) || handler.GetType().Name != "ButtonClicked")
                continue;

            Block parent = GetParentBlock(handler);
            if (parent == null || !blockNameSet.Contains(parent.BlockName))
                continue;

            parent._EventHandler = null;
            EditorUtility.SetDirty(parent);
            handler.enabled = false;
            EditorUtility.SetDirty(handler);
        }
    }

    public static void EnsureUiClickForwarder(
        GameObject uiTarget,
        RoomInteractionController controller,
        string interactionId)
    {
        if (uiTarget == null || controller == null)
            return;

        var forwarder = uiTarget.GetComponent<RoomUiClickForwarder>();
        if (forwarder == null)
            forwarder = uiTarget.AddComponent<RoomUiClickForwarder>();

        var so = new SerializedObject(forwarder);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.FindProperty("interactionId").stringValue = interactionId;
        so.FindProperty("clickable").objectReferenceValue = uiTarget.GetComponent<Clickable2D>();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(forwarder);

        Clickable2D clickable = uiTarget.GetComponent<Clickable2D>();
        if (clickable != null)
        {
            clickable.enabled = false;
            EditorUtility.SetDirty(clickable);
        }
    }

    public static Clickable2D TryResolveClickableForBlock(Flowchart flowchart, string blockName)
    {
        Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
        if (block == null)
            return null;

        foreach (ObjectClickedHandler handler in flowchart.GetComponents<ObjectClickedHandler>())
        {
            if (!IsAlive(handler) || GetParentBlock(handler) != block)
                continue;

            try
            {
                var handlerSo = new SerializedObject(handler);
                return handlerSo.FindProperty("clickableObject").objectReferenceValue as Clickable2D;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public static UnityEngine.UI.Button TryResolveUiButtonForBlock(Flowchart flowchart, string blockName)
    {
        Block block = flowchart.GetComponents<Block>().FirstOrDefault(b => b.BlockName == blockName);
        if (block == null)
            return null;

        foreach (MonoBehaviour handler in flowchart.GetComponents<MonoBehaviour>())
        {
            if (!IsAlive(handler) || handler.GetType().Name != "ButtonClicked")
                continue;
            if (GetParentBlock(handler) != block)
                continue;

            try
            {
                var handlerSo = new SerializedObject(handler);
                return handlerSo.FindProperty("targetButton").objectReferenceValue as UnityEngine.UI.Button;
            }
            catch (Exception)
            {
                return null;
            }
        }

        return null;
    }

    public static int CountDirectExecuteBlockCalls(
        UnityEngine.SceneManagement.Scene scene,
        HashSet<string> forbiddenBlockNames)
    {
        int count = 0;

        foreach (WorldItemDropZone dropZone in FindSceneComponents<WorldItemDropZone>(scene))
        {
            count += CountExecuteBlockCallsInUnityEvent(
                new SerializedObject(dropZone),
                "onUnlock",
                forbiddenBlockNames,
                scene.name,
                dropZone);
        }

        foreach (UnityEngine.UI.Button button in FindSceneComponents<UnityEngine.UI.Button>(scene))
        {
            count += CountExecuteBlockCallsInUnityEvent(
                new SerializedObject(button),
                "m_OnClick",
                forbiddenBlockNames,
                scene.name,
                button);
        }

        return count;
    }

    public static T[] FindSceneComponents<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(component => component != null && component.gameObject != null && component.gameObject.scene == scene)
            .ToArray();

    public static GameObject FindGameObjectInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
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

    public static Block GetParentBlock(MonoBehaviour handler)
    {
        if (!handler)
            return null;

        try
        {
            var so = new SerializedObject(handler);
            return so.FindProperty("parentBlock").objectReferenceValue as Block;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsAlive(MonoBehaviour component) =>
        component != null && component.gameObject != null;

    public static void SetPersistentStringUnityEvent(
        SerializedObject owner,
        string unityEventPropertyPath,
        UnityEngine.Object target,
        string controllerTypeName,
        string methodName,
        string stringArg)
    {
        SerializedProperty unityEventProperty = owner.FindProperty(unityEventPropertyPath);
        SerializedProperty calls = unityEventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");
        calls.ClearArray();
        calls.arraySize = 1;

        SerializedProperty call = calls.GetArrayElementAtIndex(0);
        call.FindPropertyRelative("m_Target").objectReferenceValue = target;
        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = controllerTypeName;
        call.FindPropertyRelative("m_MethodName").stringValue = methodName;
        call.FindPropertyRelative("m_Mode").enumValueIndex = 5;
        call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = stringArg;
        call.FindPropertyRelative("m_CallState").enumValueIndex = 2;
    }

    public static void DisableLoadSceneInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
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

    public static void DisableGoBackCommandsInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
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

    public static void DisableIsClickedResetInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
            return;

        var targets = new HashSet<string>(blockNames);
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (!IsLiveSceneObject(command, flowchart.gameObject.scene) || command.ParentBlock == null)
                continue;
            if (!targets.Contains(command.ParentBlock.BlockName))
                continue;
            if (!IsIsClickedResetCommand(flowchart, command))
                continue;

            SetComponentEnabled(command, false);
        }
    }

    public static void RewireRibbonBackButtons(
        UnityEngine.SceneManagement.Scene scene,
        RoomInteractionController controller,
        string controllerTypeName)
    {
        if (controller == null)
            return;

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
                    controllerTypeName,
                    nameof(RoomInteractionController.OnInteraction),
                    "back");
                buttonSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(button);
            }
        }
    }

    public static int CountEnabledLoadSceneCommandsInBlocks(Flowchart flowchart, string[] blockNames)
    {
        if (blockNames == null || blockNames.Length == 0)
            return 0;

        var targets = new HashSet<string>(blockNames);
        int count = 0;
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (!IsLiveSceneObject(command, flowchart.gameObject.scene) || command.ParentBlock == null)
                continue;
            if (!targets.Contains(command.ParentBlock.BlockName))
                continue;
            if (command.GetType().Name != "LoadScene")
                continue;
            if (!command.enabled)
                continue;

            count++;
        }

        return count;
    }

    public static int CountEnabledGoBackCommandsInBlocks(Flowchart flowchart, string[] blockNames) =>
        CountEnabledCommandsMatching(flowchart, blockNames, IsGoBackCommand);

    public static int VerifyRibbonBackButtonsWired(
        UnityEngine.SceneManagement.Scene scene,
        RoomInteractionController controller)
    {
        int violations = 0;
        foreach (BackNavigator navigator in FindSceneComponents<BackNavigator>(scene))
        {
            foreach (UnityEngine.UI.Button button in navigator.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                bool wiredToController = false;
                var buttonSo = new SerializedObject(button);
                SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls != null)
                {
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        SerializedProperty call = calls.GetArrayElementAtIndex(i);
                        if (call.FindPropertyRelative("m_Target").objectReferenceValue != controller)
                            continue;
                        if (call.FindPropertyRelative("m_MethodName").stringValue
                            != nameof(RoomInteractionController.OnInteraction))
                            continue;
                        if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue != "back")
                            continue;

                        wiredToController = true;
                        break;
                    }
                }

                if (!wiredToController)
                {
                    violations++;
                    Debug.LogError(
                        $"[RoomMigration] {scene.name}: ribbon button on '{button.gameObject.name}' is not wired to controller OnInteraction('back').",
                        button);
                }
            }
        }

        return violations;
    }

    public static bool UnityEventCallsExecuteBlock(
        SerializedObject owner,
        string unityEventPropertyPath,
        string blockName)
    {
        SerializedProperty calls = owner.FindProperty(unityEventPropertyPath)?.FindPropertyRelative("m_PersistentCalls.m_Calls");
        if (calls == null)
            return false;

        for (int i = 0; i < calls.arraySize; i++)
        {
            SerializedProperty call = calls.GetArrayElementAtIndex(i);
            if (call.FindPropertyRelative("m_MethodName").stringValue != "ExecuteBlock")
                continue;
            if (call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue != blockName)
                continue;

            return true;
        }

        return false;
    }

    static int CountExecuteBlockCallsInUnityEvent(
        SerializedObject owner,
        string unityEventPropertyPath,
        HashSet<string> forbiddenBlocks,
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
            if (!forbiddenBlocks.Contains(blockArg))
                continue;

            count++;
            Debug.LogError(
                $"[RoomMigration] {sceneName}: {context.GetType().Name} on '{((Component)context).gameObject.name}' still calls ExecuteBlock('{blockArg}').",
                context);
        }

        return count;
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

    static bool IsIsClickedResetCommand(Flowchart flowchart, Command command)
    {
        if (!command || command.GetType().Name != "SetVariable")
            return false;

        try
        {
            var so = new SerializedObject(command);
            SerializedProperty variableProp = so.FindProperty("anyVar.variable");
            if (variableProp == null)
                return false;

            var variable = variableProp.objectReferenceValue as Variable;
            if (variable == null || variable.Key != FungusVariableKeys.IsClicked)
                return false;

            SerializedProperty booleanVal = so.FindProperty("anyVar.data.booleanData.booleanVal");
            return booleanVal != null && !booleanVal.boolValue;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static int CountEnabledCommandsMatching(
        Flowchart flowchart,
        string[] blockNames,
        Func<Command, bool> predicate)
    {
        if (blockNames == null || blockNames.Length == 0)
            return 0;

        var targets = new HashSet<string>(blockNames);
        int count = 0;
        foreach (Command command in flowchart.GetComponents<Command>())
        {
            if (!IsLiveSceneObject(command, flowchart.gameObject.scene) || command.ParentBlock == null)
                continue;
            if (!targets.Contains(command.ParentBlock.BlockName))
                continue;
            if (!predicate(command))
                continue;
            if (!command.enabled)
                continue;

            count++;
        }

        return count;
    }

    static void SetComponentEnabled(Component component, bool enabled)
    {
        if (!component)
            return;

        try
        {
            var so = new SerializedObject(component);
            so.FindProperty("m_Enabled").boolValue = enabled;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoomMigration] Failed to set enabled={enabled} on {component.GetType().Name}: {ex.Message}");
        }
    }

    static bool IsLiveSceneObject(Component component, UnityEngine.SceneManagement.Scene scene) =>
        component != null && component.gameObject != null && component.gameObject.scene == scene;

    public sealed class WorldClickTarget
    {
        public string InteractionId;
        public string GameObjectName;
    }

    public sealed class InteractionRouteSpec
    {
        public string InteractionId;
        public string BlockName;
    }

    public sealed class BlockOutcomeSpec
    {
        public string BlockName;
        public bool ResetIsClicked;
        public bool LoadScene;
        public string SceneName;
        public bool GoBack;
    }

    public sealed class PanelCloseSpec
    {
        public string PanelCloseId;
        public GameObject Panel;
    }
}
