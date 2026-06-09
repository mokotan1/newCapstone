#if UNITY_EDITOR
using Godlotto.Constants;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// <c>disputatio/docs/dialogue-log-editor-setup.md</c> 사양대로
/// DialogueLogEntry 프리팹, IntroScene 패널, SayDialog 로그 버튼을 구성한다.
/// </summary>
public static class DialogueLogEditorSetup
{
    const string IntroScenePath = "Assets/Scenes/godlotto/IntroScene.unity";
    const string EntryPrefabPath = "Assets/godlotto/Prefab/DialogueLogEntry.prefab";
    const string SayDialogGothicPath = "Assets/godlotto/Prefab/SayDialogGothic.prefab";
    const string SayDialogNotebookPath = "Assets/godlotto/Prefab/SayDialogNotebook.prefab";
    public const string ManagerObjectName = "DialogueLogManager";
    const string LogButtonName = "LogButton";

    static readonly Color DimBackgroundColor = new Color(0f, 0f, 0f, 0.65f);
    static readonly Color PanelBackgroundColor = new Color(0.06f, 0.05f, 0.045f, 0.92f);
    static readonly Color TitleColor = new Color(0.96f, 0.88f, 0.74f, 1f);
    static readonly Color EntryTextColor = new Color(0.9f, 0.84f, 0.74f, 1f);
    static readonly Color ButtonColor = new Color(0.78f, 0.58f, 0.29f, 1f);

    [MenuItem("Tools/Godlotto/Setup Dialogue Log (Editor Guide)")]
    public static void ApplyAll()
    {
        GameObject entryPrefab = EnsureEntryPrefab();
        SetupIntroScene(entryPrefab);
        SetupSayDialogLogButton(SayDialogGothicPath);
        SetupSayDialogLogButton(SayDialogNotebookPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DialogueLogEditorSetup] 대사 로그 에디터 연동을 완료했습니다.");
    }

    [MenuItem("Tools/Godlotto/Setup Dialogue Log Button (SayDialogNotebook)")]
    public static void ApplySayDialogNotebookLogButton()
    {
        SetupSayDialogLogButton(SayDialogNotebookPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DialogueLogEditorSetup] SayDialogNotebook 로그 버튼을 적용했습니다.");
    }

    static GameObject EnsureEntryPrefab()
    {
        EnsureDirectory("Assets/godlotto/Prefab");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabPath);
        if (existing != null)
            return existing;

        var root = new GameObject("DialogueLogEntry", typeof(RectTransform), typeof(LayoutElement));
        var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(root.transform, false);

        var rect = root.GetComponent<RectTransform>();
        SetStretch(rect);

        var labelRect = labelGo.GetComponent<RectTransform>();
        SetStretch(labelRect);

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "<b>화자</b>\n대사 본문";
        label.fontSize = 22f;
        label.color = EntryTextColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.raycastTarget = false;
        ApplyKoreanFont(label);

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 48f;
        layout.flexibleWidth = 1f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void SetupIntroScene(GameObject entryPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(IntroScenePath, OpenSceneMode.Single);

        var existingManager = GameObject.Find(ManagerObjectName);
        if (existingManager != null)
            Undo.DestroyObjectImmediate(existingManager);

        var manager = new GameObject(ManagerObjectName);
        Undo.RegisterCreatedObjectUndo(manager, "Create DialogueLogManager");

        var canvasRoot = CreateCanvasRoot(manager.transform);
        var logPanel = CreateLogPanel(canvasRoot.transform, out ScrollRect scrollRect, out GameObject closeButton);
        var panelComponent = manager.AddComponent<DialogueLogPanel>();

        var serialized = new SerializedObject(panelComponent);
        serialized.FindProperty("logPanel").objectReferenceValue = logPanel;
        serialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        serialized.FindProperty("entryPrefab").objectReferenceValue = entryPrefab;
        serialized.FindProperty("logHotkey").enumValueIndex = (int)KeyCode.L;
        serialized.FindProperty("canvasSortingLayerName").stringValue = "Setting";
        serialized.FindProperty("canvasSortingOrder").intValue = 60;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        closeButton.AddComponent<DialogueLogButton>();
        logPanel.SetActive(false);

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject CreateCanvasRoot(Transform parent)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);

        var rect = canvasGo.GetComponent<RectTransform>();
        SetStretch(rect);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvasGo;
    }

    static GameObject CreateLogPanel(Transform canvasTransform, out ScrollRect scrollRect, out GameObject closeButton)
    {
        var logPanel = CreateImage(canvasTransform, "LogPanel", PanelBackgroundColor);
        SetStretch(logPanel.GetComponent<RectTransform>());

        CreateImage(logPanel.transform, "DimBackground", DimBackgroundColor);
        var dimRect = logPanel.transform.Find("DimBackground").GetComponent<RectTransform>();
        SetStretch(dimRect);
        dimRect.SetAsFirstSibling();

        var title = CreateLabel(logPanel.transform, "TitleText", "대사 기록", 28f, TitleColor);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-48f, 48f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        closeButton = CreateButton(logPanel.transform, "CloseButton", "닫기", new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(96f, 40f));

        var scrollRoot = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollRoot.name = "Scroll View";
        scrollRoot.transform.SetParent(logPanel.transform, false);
        var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(24f, 24f);
        scrollRectTransform.offsetMax = new Vector2(-24f, -88f);

        scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var content = scrollRect.content;
        var verticalLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.spacing = 12f;
        verticalLayout.padding = new RectOffset(8, 8, 8, 8);

        var contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return logPanel;
    }

    static void SetupSayDialogLogButton(string prefabPath)
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform panel = FindDeepChild(prefabRoot.transform, "Panel");
            if (panel == null)
            {
                Debug.LogError($"[DialogueLogEditorSetup] {prefabPath}에서 Panel을 찾을 수 없습니다.");
                return;
            }

            // 47e7f967 수동 YAML은 StoryText 하위에 LogButton을 둔 경우가 있어 직속 Find만으로는 누락된다.
            Transform existing = FindDeepChild(prefabRoot.transform, LogButtonName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var logButton = CreateButton(panel, LogButtonName, "로그", new Vector2(1f, 0f), new Vector2(-130f, 38f), new Vector2(77f, 77f));
            logButton.AddComponent<DialogueLogButton>();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    static GameObject CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return go;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        ApplyKoreanFont(label);
        return label;
    }

    static GameObject CreateButton(Transform parent, string name, string labelText, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var buttonGo = CreateImage(parent, name, ButtonColor);
        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var button = buttonGo.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.78f, 0.67f, 0.48f, 1f);
        button.colors = colors;

        var label = CreateLabel(buttonGo.transform, "Text", labelText, 18f, TitleColor);
        var labelRect = label.GetComponent<RectTransform>();
        SetStretch(labelRect);
        label.alignment = TextAlignmentOptions.Center;

        return buttonGo;
    }

    static void ApplyKoreanFont(TextMeshProUGUI label)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFontPaths.KoreanRegularSdf);
        if (font == null)
            return;
        label.font = font;
        label.fontSharedMaterial = font.material;
    }

    static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    static void EnsureDirectory(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(assetPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureDirectory(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
