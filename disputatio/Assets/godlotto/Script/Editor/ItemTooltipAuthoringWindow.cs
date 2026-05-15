#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ItemTooltipAuthoringWindow : EditorWindow
{
    private const string TooltipTableResourcePath = "Assets/Resources/Scenario/item_tooltip_table.csv";

    private readonly List<Item> items = new List<Item>();
    private int selectedIndex;
    private Vector2 scrollPosition;
    private ItemTooltipTable tooltipTable;

    [MenuItem("Tools/Godlotto/Item Tooltip Authoring")]
    private static void OpenWindow()
    {
        var window = GetWindow<ItemTooltipAuthoringWindow>("Item Tooltip Authoring");
        window.minSize = new Vector2(460f, 320f);
        window.RefreshItems();
    }

    private void OnEnable()
    {
        RefreshItems();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (items.Count == 0)
        {
            EditorGUILayout.HelpBox("Item 에셋이 없습니다. Create > Inventory > Item 으로 생성해주세요.", MessageType.Info);
            return;
        }

        selectedIndex = Mathf.Clamp(selectedIndex, 0, items.Count - 1);
        Item current = items[selectedIndex];
        if (current == null)
        {
            EditorGUILayout.HelpBox("선택한 Item 에셋을 찾을 수 없습니다. 새로고침 해주세요.", MessageType.Warning);
            return;
        }

        DrawItemEditor(current);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            RefreshItems();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawItemEditor(Item item)
    {
        string[] options = BuildItemNameOptions();
        selectedIndex = EditorGUILayout.Popup("Item", selectedIndex, options);
        ItemTooltipContent tableContent = GetTableContent(item);

        var serializedObject = new SerializedObject(item);
        serializedObject.Update();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemName"), new GUIContent("Item Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("itemDescription"), new GUIContent("Tooltip Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tooltipRows"), new GUIContent("Tooltip Table"), true);
        DrawParsedTooltipTable(tableContent);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"), new GUIContent("Icon"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Tooltip Preview", EditorStyles.boldLabel);
        string preview = ItemTooltipTextFormatter.Build(
            tableContent.itemName,
            tableContent.itemDescription,
            tableContent.rows.Count > 0 ? tableContent.rows : item.tooltipRows);
        EditorGUILayout.HelpBox(preview, MessageType.None);

        EditorGUILayout.EndScrollView();

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssetIfDirty(item);
        }
    }

    private string[] BuildItemNameOptions()
    {
        var options = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            options[i] = item == null ? "Missing Item" : item.name;
        }

        return options;
    }

    private void RefreshItems()
    {
        RefreshTooltipTable();
        items.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Item");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null)
                items.Add(item);
        }

        selectedIndex = 0;
        Repaint();
    }

    private void RefreshTooltipTable()
    {
        TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(TooltipTableResourcePath);
        tooltipTable = ItemTooltipTable.FromCsv(csv != null ? csv.text : "");
    }

    private ItemTooltipContent GetTableContent(Item item)
    {
        if (tooltipTable == null)
            RefreshTooltipTable();

        return tooltipTable.GetContent(item.itemId, item.itemName, item.itemDescription);
    }

    private static void DrawParsedTooltipTable(ItemTooltipContent tableContent)
    {
        if (tableContent.rows.Count == 0)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Parsed Tooltip Table", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            foreach (ItemTooltipRow row in tableContent.rows)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.TextField(row.key);
                EditorGUILayout.TextField(row.value);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
