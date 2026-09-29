using System;
using Godlotto.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hall_playerble: CorridorEntrance Fungus Flowchart + Variablemanager → HallPlayableController + HallGlobalStateHost.
/// </summary>
public static class HallPlayableFlowchartRemovalPilot
{
    const string ScenePath = "Assets/Scenes/Mokotan/First Floor/Hall_playerble.unity";
    const string OpeningOfficePath = "Assets/Scenes/Mokotan/Opening_Office.unity";

    [MenuItem("Tools/Godlotto/Fungus Deletion/Hall Playable Remove Flowcharts")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before editing Hall_playerble.");

        ApplyHallPlayable();
        RemoveVariablemanagerFromOpeningOffice();
        Debug.Log("[HallPlayableFlowchartRemovalPilot] Hall_playerble + Opening_Office Variablemanager cleared.");
    }

    [MenuItem("Tools/Godlotto/Fungus Deletion/Hall Playable Clear Orphan ExecuteBlock Calls")]
    public static void ClearOrphansOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        HallPlayableController controller = UnityEngine.Object.FindFirstObjectByType<HallPlayableController>();
        if (controller == null)
            throw new InvalidOperationException("HallPlayableController missing in Hall_playerble.");

        RewireButtonsToHallPlayable(scene, controller);
        RewirePersistentOnInteractionCalls(scene, controller);
        int cleared = ClearOrphanFlowchartCalls(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[HallPlayableFlowchartRemovalPilot] unlock rewire + orphan cleared=" + cleared);
    }

    [MenuItem("Tools/Godlotto/Fungus Deletion/Hall Playable PlayMode Route Smoke")]
    public static string PlayModeRouteSmoke()
    {
        HallPlayableController.ResetStateForTests();
        HallGlobalStateHost.EnsureInstance();
        HallPlayableController controller = UnityEngine.Object.FindFirstObjectByType<HallPlayableController>();
        if (controller == null)
            return "NO_CONTROLLER";

        var loads = new System.Collections.Generic.List<string>();
        HallPlayableController.SceneLoadHandlerForTests = scene =>
        {
            loads.Add(scene);
            return true;
        };
        HallPlayableController.FadeThenHandlerForTests = (_, __, done) => done?.Invoke();
        HallPlayableController.SayHandlerForTests = (_, done) => done?.Invoke();
        HallPlayableController.MenuHandlerForTests = (_, choose) => choose(0);
        HallPlayableController.SequenceRunnerForTests = seq =>
        {
            var stack = new System.Collections.Generic.Stack<System.Collections.IEnumerator>();
            stack.Push(seq);
            while (stack.Count > 0)
            {
                System.Collections.IEnumerator cur = stack.Peek();
                if (!cur.MoveNext())
                {
                    stack.Pop();
                    continue;
                }

                if (cur.Current is System.Collections.IEnumerator nested)
                    stack.Push(nested);
            }
        };

        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;

        controller.OnInteraction(HallPlayableController.InteractionRight);
        SceneInteractionController.ClearDuplicateClickHistory();
        controller.OnInteraction(HallPlayableController.InteractionLeft);
        SceneInteractionController.ClearDuplicateClickHistory();
        controller.OnInteraction(HallPlayableController.InteractionStair);
        SceneInteractionController.ClearDuplicateClickHistory();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedBasementKey, false);
        controller.OnInteraction(HallPlayableController.InteractionBasement);
        SceneInteractionController.ClearDuplicateClickHistory();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedBasementKey, true);
        controller.OnInteraction(HallPlayableController.InteractionBasement);
        SceneInteractionController.ClearDuplicateClickHistory();
        controller.OnInteraction(HallPlayableController.InteractionUnlock);
        SceneInteractionController.ClearDuplicateClickHistory();
        controller.OnInteraction(HallPlayableController.InteractionMap);

        bool hub = controller.ShouldLoadHallAnimateFromHub();
        bool busy = controller.IsBusyForTests;
        HallPlayableController.ResetStateForTests();
        return "loads=" + string.Join(",", loads) + ";hubAnimate=" + hub + ";busy=" + busy;
    }

    static void ApplyHallPlayable()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CorridorEntranceController legacy = UnityEngine.Object.FindFirstObjectByType<CorridorEntranceController>();
        GameObject flowchart = Find(scene, "Flowchart_hall") ?? Find(scene, "Flowchart");
        GameObject variablemanager = Find(scene, "Variablemanager");
        if (flowchart == null)
            throw new InvalidOperationException("Hall_playerble preflight: Flowchart_hall missing.");

        GameObject hostGo = Find(scene, "HallPlayableController") ?? new GameObject("HallPlayableController");
        HallPlayableController controller = hostGo.GetComponent<HallPlayableController>()
            ?? hostGo.AddComponent<HallPlayableController>();

        SerializedObject so = new SerializedObject(controller);
        SerializedProperty worldClicks = so.FindProperty("worldClicks");

        if (legacy != null)
        {
            SerializedObject legacySo = new SerializedObject(legacy);
            SerializedProperty legacyClicks = legacySo.FindProperty("worldClicks");
            worldClicks.arraySize = legacyClicks.arraySize;
            for (int i = 0; i < legacyClicks.arraySize; i++)
            {
                SerializedProperty src = legacyClicks.GetArrayElementAtIndex(i);
                SerializedProperty dst = worldClicks.GetArrayElementAtIndex(i);
                dst.FindPropertyRelative("interactionId").stringValue =
                    src.FindPropertyRelative("interactionId").stringValue;
                dst.FindPropertyRelative("collider").objectReferenceValue =
                    src.FindPropertyRelative("collider").objectReferenceValue;
                dst.FindPropertyRelative("clickable").objectReferenceValue =
                    src.FindPropertyRelative("clickable").objectReferenceValue;
            }

            UnityEngine.Object.DestroyImmediate(legacy);
        }

        GameObject fieldMap = Find(scene, "FeildMap");
        so.FindProperty("fieldMapObject").objectReferenceValue = fieldMap;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject stateGo = Find(scene, HallGlobalStateHost.HostObjectName)
            ?? new GameObject(HallGlobalStateHost.HostObjectName);
        if (stateGo.GetComponent<HallGlobalStateHost>() == null)
            stateGo.AddComponent<HallGlobalStateHost>();

        RewireButtonsToHallPlayable(scene, controller);
        RewirePersistentOnInteractionCalls(scene, controller);
        ClearOrphanFlowchartCalls(scene);

        UnityEngine.Object.DestroyImmediate(flowchart);
        if (variablemanager != null)
            UnityEngine.Object.DestroyImmediate(variablemanager);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void RemoveVariablemanagerFromOpeningOffice()
    {
        Scene scene = EditorSceneManager.OpenScene(OpeningOfficePath, OpenSceneMode.Single);
        GameObject variablemanager = Find(scene, "Variablemanager");
        GameObject stateGo = Find(scene, HallGlobalStateHost.HostObjectName)
            ?? new GameObject(HallGlobalStateHost.HostObjectName);
        if (stateGo.GetComponent<HallGlobalStateHost>() == null)
            stateGo.AddComponent<HallGlobalStateHost>();

        if (variablemanager != null)
            UnityEngine.Object.DestroyImmediate(variablemanager);

        GameObject flowchart = Find(scene, "Flowchart");
        if (flowchart != null)
            UnityEngine.Object.DestroyImmediate(flowchart);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void RewireButtonsToHallPlayable(Scene scene, HallPlayableController controller)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                SerializedObject buttonSo = new SerializedObject(button);
                SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                bool dirty = false;
                for (int i = 0; i < calls.arraySize; i++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    string typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? string.Empty;
                    string method = call.FindPropertyRelative("m_MethodName").stringValue ?? string.Empty;
                    if (!typeName.Contains("CorridorEntranceController") && !typeName.Contains("RoomInteractionController"))
                        continue;
                    if (method != "OnInteraction")
                        continue;

                    call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
                    call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                        "HallPlayableController, Assembly-CSharp";
                    dirty = true;
                }

                if (dirty)
                    buttonSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static void RewirePersistentOnInteractionCalls(Scene scene, HallPlayableController controller)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                SerializedObject so = new SerializedObject(behaviour);
                bool dirty = false;
                foreach (string path in new[]
                         {
                             "onUnlock.m_PersistentCalls.m_Calls",
                             "m_OnClick.m_PersistentCalls.m_Calls"
                         })
                {
                    SerializedProperty calls = so.FindProperty(path);
                    if (calls == null || !calls.isArray)
                        continue;
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        SerializedProperty call = calls.GetArrayElementAtIndex(i);
                        string typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? string.Empty;
                        string method = call.FindPropertyRelative("m_MethodName").stringValue ?? string.Empty;
                        if (!typeName.Contains("CorridorEntranceController") && !typeName.Contains("RoomInteractionController"))
                            continue;
                        if (method != "OnInteraction")
                            continue;

                        call.FindPropertyRelative("m_Target").objectReferenceValue = controller;
                        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                            "HallPlayableController, Assembly-CSharp";
                        dirty = true;
                    }
                }

                if (dirty)
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static int ClearOrphanFlowchartCalls(Scene scene)
    {
        int cleared = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                SerializedObject buttonSo = new SerializedObject(button);
                SerializedProperty calls = buttonSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                bool dirty = false;
                for (int i = calls.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    string typeName = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue ?? string.Empty;
                    string method = call.FindPropertyRelative("m_MethodName").stringValue ?? string.Empty;
                    UnityEngine.Object target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                    if (!typeName.Contains("Fungus.Flowchart") && !(method == "ExecuteBlock" && target == null))
                        continue;
                    calls.DeleteArrayElementAtIndex(i);
                    cleared++;
                    dirty = true;
                }

                if (dirty)
                    buttonSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return cleared;
    }

    static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child.gameObject;
            }
        }

        return null;
    }
}
