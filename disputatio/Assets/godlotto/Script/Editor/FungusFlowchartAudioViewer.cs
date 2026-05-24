#if UNITY_EDITOR
using System.Collections.Generic;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FungusFlowchartAudioViewer : EditorWindow
{
    private const string RuntimeBgmPlayerPrefabPath = "Assets/godlotto/Resources/Audio/BGM Player.prefab";

    private Flowchart flowchart;
    private int selectedBlockIndex;
    private int targetSfxIndex;
    private Vector2 scroll;

    [MenuItem("Tools/Godlotto/Audio/Fungus Flowchart Audio Viewer")]
    private static void OpenWindow()
    {
        var window = GetWindow<FungusFlowchartAudioViewer>("Flowchart Audio");
        window.minSize = new Vector2(760f, 420f);
    }

    private void OnGUI()
    {
        DrawFlowchartPicker();

        if (flowchart == null)
        {
            EditorGUILayout.HelpBox("Select a Flowchart to inspect its command order and audio commands.", MessageType.Info);
            return;
        }

        Block[] blocks = flowchart.GetComponents<Block>();
        if (blocks.Length == 0)
        {
            EditorGUILayout.HelpBox("This Flowchart has no Blocks.", MessageType.Warning);
            return;
        }

        selectedBlockIndex = Mathf.Clamp(selectedBlockIndex, 0, blocks.Length - 1);
        selectedBlockIndex = EditorGUILayout.Popup("Block", selectedBlockIndex, BuildBlockNames(blocks));

        Block block = blocks[selectedBlockIndex];
        DrawBulkActions(block);
        DrawCommandTable(block);
    }

    private void DrawFlowchartPicker()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        flowchart = (Flowchart)EditorGUILayout.ObjectField(flowchart, typeof(Flowchart), true);

        if (GUILayout.Button("Use Selection", EditorStyles.toolbarButton, GUILayout.Width(100f)))
        {
            if (Selection.activeGameObject != null)
                flowchart = Selection.activeGameObject.GetComponent<Flowchart>();
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            Repaint();

        EditorGUILayout.EndHorizontal();
    }

    private static string[] BuildBlockNames(Block[] blocks)
    {
        string[] names = new string[blocks.Length];
        for (int i = 0; i < blocks.Length; i++)
            names[i] = $"{i}: {blocks[i].BlockName}";
        return names;
    }

    private void DrawBulkActions(Block block)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        targetSfxIndex = EditorGUILayout.IntField("Target SFX Index", Mathf.Max(0, targetSfxIndex), GUILayout.Width(220f));

        if (GUILayout.Button("Remove Matching SFX In Block", GUILayout.Width(210f)))
            RemoveMatchingSfxCommands(block, targetSfxIndex);

        if (GUILayout.Button("Remove Consecutive Duplicate SFX", GUILayout.Width(230f)))
            RemoveConsecutiveDuplicateSfxCommands(block);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4f);
    }

    private void DrawCommandTable(Block block)
    {
        AudioClip[] clips = LoadRuntimeSfxList();

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(36f));
        GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(180f));
        GUILayout.Label("Summary", EditorStyles.boldLabel);
        GUILayout.Label("Audio", EditorStyles.boldLabel, GUILayout.Width(210f));
        GUILayout.Label("Actions", EditorStyles.boldLabel, GUILayout.Width(160f));
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < block.CommandList.Count; i++)
        {
            Command command = block.CommandList[i];
            if (command == null)
            {
                DrawMissingCommandRow(i);
                continue;
            }

            DrawCommandRow(block, command, i, clips);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawMissingCommandRow(int index)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(index.ToString(), GUILayout.Width(36f));
        GUILayout.Label("Missing", GUILayout.Width(180f));
        GUILayout.Label("Null command reference");
        GUILayout.Space(370f);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCommandRow(Block block, Command command, int index, AudioClip[] clips)
    {
        PlayRegisteredSfx registeredSfx = command as PlayRegisteredSfx;
        string typeName = command.GetType().Name;
        string summary = command.GetSummary();
        if (string.IsNullOrWhiteSpace(summary))
            summary = typeName;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(index.ToString(), GUILayout.Width(36f));
        GUILayout.Label(typeName, GUILayout.Width(180f));
        GUILayout.Label(new GUIContent(summary), GUILayout.MinWidth(180f));
        GUILayout.Label(BuildAudioLabel(registeredSfx, clips), GUILayout.Width(210f));

        if (GUILayout.Button("Select", GUILayout.Width(70f)))
            SelectCommand(block, command);

        using (new EditorGUI.DisabledScope(registeredSfx == null))
        {
            if (GUILayout.Button("Remove", GUILayout.Width(80f)))
                RemoveCommand(block, command);
        }

        EditorGUILayout.EndHorizontal();
    }

    private static string BuildAudioLabel(PlayRegisteredSfx command, AudioClip[] clips)
    {
        if (command == null)
            return "";

        int index = command.SfxIndex;
        if (clips == null || index < 0 || index >= clips.Length)
            return $"SFX {index}: out of range";

        AudioClip clip = clips[index];
        return clip == null ? $"SFX {index}: empty" : $"SFX {index}: {clip.name}";
    }

    private static AudioClip[] LoadRuntimeSfxList()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeBgmPlayerPrefabPath);
        AudioController controller = prefab != null ? prefab.GetComponent<AudioController>() : null;
        return controller != null ? controller.sfxList : null;
    }

    private static void SelectCommand(Block block, Command command)
    {
        Flowchart owner = block.GetFlowchart();
        owner.SelectedBlock = block;
        owner.ClearSelectedCommands();
        owner.AddSelectedCommand(command);
        Selection.activeObject = owner.gameObject;
    }

    private static void RemoveCommand(Block block, Command command)
    {
        if (block == null || command == null)
            return;

        Flowchart owner = block.GetFlowchart();
        Undo.RecordObject(block, "Remove Fungus SFX Command");
        Undo.RecordObject(owner, "Remove Fungus SFX Command");
        block.CommandList.Remove(command);
        block.UpdateIndentLevels();
        owner.ClearSelectedCommands();
        Undo.DestroyObjectImmediate(command);
        EditorUtility.SetDirty(block);
        EditorUtility.SetDirty(owner);
        EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
    }

    private static void RemoveMatchingSfxCommands(Block block, int index)
    {
        var targets = new List<Command>();
        foreach (Command command in block.CommandList)
        {
            PlayRegisteredSfx sfx = command as PlayRegisteredSfx;
            if (sfx != null && sfx.SfxIndex == index)
                targets.Add(command);
        }

        RemoveCommands(block, targets, $"Remove SFX Index {index} Commands");
    }

    private static void RemoveConsecutiveDuplicateSfxCommands(Block block)
    {
        var targets = new List<Command>();
        PlayRegisteredSfx previous = null;

        foreach (Command command in block.CommandList)
        {
            PlayRegisteredSfx current = command as PlayRegisteredSfx;
            if (current != null && previous != null && current.SfxIndex == previous.SfxIndex)
                targets.Add(current);

            previous = current;
        }

        RemoveCommands(block, targets, "Remove Consecutive Duplicate SFX Commands");
    }

    private static void RemoveCommands(Block block, List<Command> commands, string undoName)
    {
        if (block == null || commands == null || commands.Count == 0)
            return;

        Flowchart owner = block.GetFlowchart();
        Undo.RecordObject(block, undoName);
        Undo.RecordObject(owner, undoName);

        foreach (Command command in commands)
        {
            block.CommandList.Remove(command);
            Undo.DestroyObjectImmediate(command);
        }

        block.UpdateIndentLevels();
        owner.ClearSelectedCommands();
        EditorUtility.SetDirty(block);
        EditorUtility.SetDirty(owner);
        EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
    }
}
#endif
