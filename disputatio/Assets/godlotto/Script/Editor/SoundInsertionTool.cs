#if UNITY_EDITOR
using System.Collections.Generic;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SoundInsertionTool : EditorWindow
{
    private const string AudioBridgePrefabPath = "Assets/godlotto/Prefab/Audio Bridge.prefab";
    private const string SfxPlayerPrefabPath = "Assets/godlotto/Prefab/SFX Player.prefab";
    private const string RuntimeSfxPlayerPrefabPath = "Assets/godlotto/Resources/Audio/SFX Player.prefab";

    private AudioClip selectedClip;
    private int sfxIndex;
    private Flowchart targetFlowchart;
    private int selectedBlockIndex;
    private int insertAfterCommandIndex = -1;
    private string randomSfxIndicesText = "0, 1";
    private bool stopAllSfx;
    private Vector2 scrollPosition;

    [MenuItem("Tools/Godlotto/Audio/Sound Insertion Tool")]
    private static void OpenWindow()
    {
        var window = GetWindow<SoundInsertionTool>("Sound Insertion");
        window.minSize = new Vector2(480f, 360f);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Scene Audio Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "현재 씬에서 Fungus Invoke Method 대상이 될 Audio Bridge를 보장합니다. BGM Player는 Play 시작 시 자동 생성됩니다.",
            MessageType.Info);

        if (GUILayout.Button("Ensure Audio Bridge In Current Scene"))
            EnsureAudioBridgeInCurrentScene();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("SFX Registration", EditorStyles.boldLabel);
        selectedClip = (AudioClip)EditorGUILayout.ObjectField("SFX Clip", selectedClip, typeof(AudioClip), false);

        using (new EditorGUI.DisabledScope(selectedClip == null))
        {
            if (GUILayout.Button("Add Clip To Global SFX List"))
                AddClipToGlobalSfxList(selectedClip);

            if (GUILayout.Button("Ping Selected Clip"))
                EditorGUIUtility.PingObject(selectedClip);
        }

        EditorGUILayout.Space(12f);
        DrawCurrentSfxList();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("Fungus Flowchart Insert", EditorStyles.boldLabel);
        DrawFungusInsertTool();

        EditorGUILayout.EndScrollView();
    }

    private static void EnsureAudioBridgeInCurrentScene()
    {
        if (Object.FindFirstObjectByType<AudioBridge>() != null)
        {
            Debug.Log("[SoundInsertionTool] 현재 씬에 Audio Bridge가 이미 있습니다.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioBridgePrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SoundInsertionTool] Audio Bridge prefab을 찾을 수 없습니다: {AudioBridgePrefabPath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        Undo.RegisterCreatedObjectUndo(instance, "Create Audio Bridge");
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeObject = instance;
        Debug.Log("[SoundInsertionTool] 현재 씬에 Audio Bridge를 추가했습니다.");
    }

    private static void AddClipToGlobalSfxList(AudioClip clip)
    {
        int authoringIndex = AddClipToPrefabSfxList(SfxPlayerPrefabPath, clip);
        int runtimeIndex = AddClipToPrefabSfxList(RuntimeSfxPlayerPrefabPath, clip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int index = runtimeIndex >= 0 ? runtimeIndex : authoringIndex;
        Debug.Log($"[SoundInsertionTool] SFX 등록 완료: {clip.name}, index {index}. Fungus에서는 Audio Bridge -> CallPlaySFX({index})를 호출하십시오.");
    }

    private static void ClearGlobalSfxIndex(int index)
    {
        SetPrefabSfxIndex(SfxPlayerPrefabPath, index, null, removeSlot: false);
        SetPrefabSfxIndex(RuntimeSfxPlayerPrefabPath, index, null, removeSlot: false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[SoundInsertionTool] SFX index {index} 슬롯을 비웠습니다. 기존 Fungus 인덱스는 유지됩니다.");
    }

    private static void RemoveGlobalSfxIndex(int index)
    {
        SetPrefabSfxIndex(SfxPlayerPrefabPath, index, null, removeSlot: true);
        SetPrefabSfxIndex(RuntimeSfxPlayerPrefabPath, index, null, removeSlot: true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.LogWarning($"[SoundInsertionTool] SFX index {index}를 삭제했습니다. 뒤쪽 index가 당겨졌으므로 기존 Fungus 참조를 확인하십시오.");
    }

    private static int AddClipToPrefabSfxList(string prefabPath, AudioClip clip)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SfxController controller = prefabRoot.GetComponent<SfxController>();
            if (controller == null)
            {
                Debug.LogError($"[SoundInsertionTool] SfxController가 없습니다: {prefabPath}");
                return -1;
            }

            var clips = new List<AudioClip>(controller.sfxList ?? new AudioClip[0]);
            int existingIndex = clips.IndexOf(clip);
            if (existingIndex >= 0)
                return existingIndex;

            clips.Add(clip);
            controller.sfxList = clips.ToArray();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            return clips.Count - 1;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void SetPrefabSfxIndex(string prefabPath, int index, AudioClip clip, bool removeSlot)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SfxController controller = prefabRoot.GetComponent<SfxController>();
            if (controller == null || controller.sfxList == null || index < 0 || index >= controller.sfxList.Length)
                return;

            var clips = new List<AudioClip>(controller.sfxList);
            if (removeSlot)
                clips.RemoveAt(index);
            else
                clips[index] = clip;

            controller.sfxList = clips.ToArray();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private void DrawCurrentSfxList()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeSfxPlayerPrefabPath);
        SfxController controller = prefab != null ? prefab.GetComponent<SfxController>() : null;

        EditorGUILayout.LabelField("Current Runtime SFX Index", EditorStyles.boldLabel);
        if (controller == null || controller.sfxList == null || controller.sfxList.Length == 0)
        {
            EditorGUILayout.HelpBox("등록된 SFX가 없습니다.", MessageType.Warning);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            for (int i = 0; i < controller.sfxList.Length; i++)
                EditorGUILayout.ObjectField($"Index {i}", controller.sfxList[i], typeof(AudioClip), false);
        }

        EditorGUILayout.Space(4f);
        sfxIndex = EditorGUILayout.IntField("Target SFX Index", Mathf.Max(0, sfxIndex));

        using (new EditorGUI.DisabledScope(sfxIndex < 0 || sfxIndex >= controller.sfxList.Length))
        {
            if (GUILayout.Button("Clear SFX Slot, Keep Index"))
                ClearGlobalSfxIndex(sfxIndex);

            if (GUILayout.Button("Remove SFX Slot, Shift Later Indices"))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "Remove SFX Slot",
                    "이 작업은 뒤쪽 SFX index를 모두 당깁니다. 이미 Fungus에서 index를 참조하고 있다면 깨질 수 있습니다. 계속하시겠습니까?",
                    "Remove",
                    "Cancel");
                if (ok)
                    RemoveGlobalSfxIndex(sfxIndex);
            }
        }
    }

    private void DrawFungusInsertTool()
    {
        targetFlowchart = (Flowchart)EditorGUILayout.ObjectField("Flowchart", targetFlowchart, typeof(Flowchart), true);
        sfxIndex = EditorGUILayout.IntField("SFX Index", Mathf.Max(0, sfxIndex));

        if (targetFlowchart == null)
        {
            if (Selection.activeGameObject != null)
            {
                Flowchart selectedFlowchart = Selection.activeGameObject.GetComponent<Flowchart>();
                if (selectedFlowchart != null && GUILayout.Button("Use Selected Flowchart"))
                    targetFlowchart = selectedFlowchart;
            }
            return;
        }

        Block[] blocks = targetFlowchart.GetComponents<Block>();
        if (blocks.Length == 0)
        {
            EditorGUILayout.HelpBox("선택한 Flowchart에 Block이 없습니다.", MessageType.Warning);
            return;
        }

        selectedBlockIndex = Mathf.Clamp(selectedBlockIndex, 0, blocks.Length - 1);
        string[] blockNames = new string[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
            blockNames[i] = blocks[i].BlockName;

        selectedBlockIndex = EditorGUILayout.Popup("Block", selectedBlockIndex, blockNames);
        Block block = blocks[selectedBlockIndex];

        string[] positions = BuildCommandPositionOptions(block);
        int selectedPosition = Mathf.Clamp(insertAfterCommandIndex + 1, 0, positions.Length - 1);
        selectedPosition = EditorGUILayout.Popup("Insert Position", selectedPosition, positions);
        insertAfterCommandIndex = selectedPosition - 1;

        if (GUILayout.Button("Insert Play Registered SFX Command"))
            QueueInsertPlayRegisteredSfxCommand(block, insertAfterCommandIndex, sfxIndex);

        EditorGUILayout.Space(6f);
        randomSfxIndicesText = EditorGUILayout.TextField("Random SFX Indices", randomSfxIndicesText);
        if (GUILayout.Button("Insert Random Registered SFX Command"))
        {
            int[] indices = ParseSfxIndices(randomSfxIndicesText);
            if (indices.Length == 0)
                Debug.LogWarning("[SoundInsertionTool] Random SFX indices are empty.");
            else
                QueueInsertRandomRegisteredSfxCommand(block, insertAfterCommandIndex, indices);
        }

        EditorGUILayout.Space(6f);
        stopAllSfx = EditorGUILayout.Toggle("Stop All SFX", stopAllSfx);
        if (GUILayout.Button("Insert Stop Registered SFX Command"))
            QueueInsertStopRegisteredSfxCommand(block, insertAfterCommandIndex, sfxIndex, stopAllSfx);
    }

    private static string[] BuildCommandPositionOptions(Block block)
    {
        var options = new List<string> { "At Start" };
        for (int i = 0; i < block.CommandList.Count; i++)
        {
            Command command = block.CommandList[i];
            string summary = command != null ? command.GetSummary() : "Missing Command";
            if (string.IsNullOrWhiteSpace(summary))
                summary = command != null ? command.GetType().Name : "Missing Command";

            options.Add($"After {i}: {summary}");
        }

        return options.ToArray();
    }

    private static void QueueInsertPlayRegisteredSfxCommand(Block block, int afterCommandIndex, int index)
    {
        if (block == null)
            return;

        EditorApplication.delayCall += () => InsertPlayRegisteredSfxCommand(block, afterCommandIndex, index);
    }

    private static void QueueInsertRandomRegisteredSfxCommand(Block block, int afterCommandIndex, int[] indices)
    {
        if (block == null)
            return;

        EditorApplication.delayCall += () => InsertRandomRegisteredSfxCommand(block, afterCommandIndex, indices);
    }

    private static void QueueInsertStopRegisteredSfxCommand(Block block, int afterCommandIndex, int index, bool stopAll)
    {
        if (block == null)
            return;

        EditorApplication.delayCall += () => InsertStopRegisteredSfxCommand(block, afterCommandIndex, index, stopAll);
    }

    private static void InsertPlayRegisteredSfxCommand(Block block, int afterCommandIndex, int index)
    {
        if (block == null)
            return;

        Flowchart flowchart = block.GetFlowchart();
        if (flowchart == null)
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Insert Play Registered SFX");
        Undo.RecordObject(flowchart, "Insert Play Registered SFX");
        Undo.RecordObject(block, "Insert Play Registered SFX");

        PlayRegisteredSfx command = Undo.AddComponent<PlayRegisteredSfx>(flowchart.gameObject);
        flowchart.AddSelectedCommand(command);
        command.ParentBlock = block;
        command.ItemId = flowchart.NextItemId();
        command.SetSfxIndex(index);
        command.OnCommandAdded(block);

        int insertIndex = Mathf.Clamp(afterCommandIndex + 1, 0, block.CommandList.Count);
        block.CommandList.Insert(insertIndex, command);
        block.UpdateIndentLevels();

        flowchart.ClearSelectedCommands();
        flowchart.SelectedBlock = block;
        flowchart.AddSelectedCommand(command);

        EditorUtility.SetDirty(command);
        EditorUtility.SetDirty(block);
        EditorUtility.SetDirty(flowchart);
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        EditorSceneManager.MarkSceneDirty(block.gameObject.scene);

        Selection.activeObject = flowchart.gameObject;
        Undo.CollapseUndoOperations(undoGroup);
        EditorApplication.delayCall += () => VerifyInsertedCommand(block, command, insertIndex, index);
        Debug.Log($"[SoundInsertionTool] {flowchart.name}/{block.BlockName}의 command index {insertIndex}에 Play Registered SFX({index})를 추가했습니다.");
    }

    private static void InsertRandomRegisteredSfxCommand(Block block, int afterCommandIndex, int[] indices)
    {
        if (block == null)
            return;

        Flowchart flowchart = block.GetFlowchart();
        if (flowchart == null)
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Insert Random Registered SFX");
        Undo.RecordObject(flowchart, "Insert Random Registered SFX");
        Undo.RecordObject(block, "Insert Random Registered SFX");

        PlayRandomRegisteredSfx command = Undo.AddComponent<PlayRandomRegisteredSfx>(flowchart.gameObject);
        flowchart.AddSelectedCommand(command);
        command.ParentBlock = block;
        command.ItemId = flowchart.NextItemId();
        command.SetSfxIndices(indices);
        command.OnCommandAdded(block);

        int insertIndex = Mathf.Clamp(afterCommandIndex + 1, 0, block.CommandList.Count);
        block.CommandList.Insert(insertIndex, command);
        FinishInsertCommand(flowchart, block, command, undoGroup);

        string label = command.GetSummary();
        EditorApplication.delayCall += () => VerifyInsertedCommand(block, command, insertIndex, label);
        Debug.Log($"[SoundInsertionTool] {flowchart.name}/{block.BlockName} command index {insertIndex} added {label}.");
    }

    private static void InsertStopRegisteredSfxCommand(Block block, int afterCommandIndex, int index, bool stopAll)
    {
        if (block == null)
            return;

        Flowchart flowchart = block.GetFlowchart();
        if (flowchart == null)
            return;

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Insert Stop Registered SFX");
        Undo.RecordObject(flowchart, "Insert Stop Registered SFX");
        Undo.RecordObject(block, "Insert Stop Registered SFX");

        StopRegisteredSfx command = Undo.AddComponent<StopRegisteredSfx>(flowchart.gameObject);
        flowchart.AddSelectedCommand(command);
        command.ParentBlock = block;
        command.ItemId = flowchart.NextItemId();
        command.SetStopTarget(index, stopAll);
        command.OnCommandAdded(block);

        int insertIndex = Mathf.Clamp(afterCommandIndex + 1, 0, block.CommandList.Count);
        block.CommandList.Insert(insertIndex, command);
        FinishInsertCommand(flowchart, block, command, undoGroup);

        string label = command.GetSummary();
        EditorApplication.delayCall += () => VerifyInsertedCommand(block, command, insertIndex, label);
        Debug.Log($"[SoundInsertionTool] {flowchart.name}/{block.BlockName} command index {insertIndex} added {label}.");
    }

    private static void FinishInsertCommand(Flowchart flowchart, Block block, Command command, int undoGroup)
    {
        block.UpdateIndentLevels();

        flowchart.ClearSelectedCommands();
        flowchart.SelectedBlock = block;
        flowchart.AddSelectedCommand(command);

        EditorUtility.SetDirty(command);
        EditorUtility.SetDirty(block);
        EditorUtility.SetDirty(flowchart);
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        EditorSceneManager.MarkSceneDirty(block.gameObject.scene);

        Selection.activeObject = flowchart.gameObject;
        Undo.CollapseUndoOperations(undoGroup);
    }

    private static int[] ParseSfxIndices(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new int[0];

        string[] parts = text.Split(',');
        var indices = new List<int>();
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int index) && index >= 0)
                indices.Add(index);
        }

        return indices.ToArray();
    }

    private static void VerifyInsertedCommand(Block block, Command command, int insertIndex, object index)
    {
        if (block == null)
            return;

        bool survived = command != null && block.CommandList.Contains(command);
        if (!survived)
        {
            Debug.LogError($"[SoundInsertionTool] Play Registered SFX({index}) command가 삽입 직후 제거되었습니다. Fungus Flowchart 정리 루틴과 충돌했을 가능성이 큽니다.");
            return;
        }

        if (insertIndex >= 0 && insertIndex < block.CommandList.Count && block.CommandList[insertIndex] == command)
            return;

        Debug.LogWarning($"[SoundInsertionTool] Play Registered SFX({index}) command는 남아 있지만 예상 위치와 다릅니다. 현재 Block의 커맨드 순서를 확인하십시오.");
    }
}
#endif
