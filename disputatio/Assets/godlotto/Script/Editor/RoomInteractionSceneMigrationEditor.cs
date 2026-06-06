using System;
using System.Collections.Generic;
using System.Linq;
using Fungus;
using Godlotto.Interaction;
using UnityEditor;
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
}
