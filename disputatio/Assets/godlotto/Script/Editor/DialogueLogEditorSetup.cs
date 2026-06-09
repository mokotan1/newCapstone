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
/// 스타일별 DialogueLogEntry 프리팹·IntroScene 패널·SayDialog 로그 버튼을 구성한다.
/// </summary>
public static class DialogueLogEditorSetup
{
    const string IntroScenePath = "Assets/Scenes/godlotto/IntroScene.unity";
    const string EntryPrefabLegacyPath = "Assets/godlotto/Prefab/DialogueLogEntry.prefab";
    const string EntryPrefabParchmentPath = "Assets/godlotto/Prefab/DialogueLogEntry_Parchment.prefab";
    const string EntryPrefabDarkConfessionPath = "Assets/godlotto/Prefab/DialogueLogEntry_DarkConfession.prefab";
    const string SayDialogGothicPath = "Assets/godlotto/Prefab/SayDialogGothic.prefab";
    const string SayDialogNotebookPath = "Assets/godlotto/Prefab/SayDialogNotebook.prefab";
    const string HistoryIconPath = "Assets/Fungus/Textures/HistoryIcon.png";
    public const string ManagerObjectName = "DialogueLogManager";
    const string LogButtonName = "LogButton";

    [MenuItem("Tools/Godlotto/Setup Dialogue Log (Editor Guide)")]
    public static void ApplyAllParchmentCodex() => ApplyStyle(DialogueLogVisualStyle.ParchmentCodex);

    [MenuItem("Tools/Godlotto/Setup Dialogue Log/Parchment Codex (①)")]
    public static void ApplyParchmentCodex() => ApplyStyle(DialogueLogVisualStyle.ParchmentCodex);

    [MenuItem("Tools/Godlotto/Setup Dialogue Log/Legacy Notebook")]
    public static void ApplyLegacyNotebook() => ApplyStyle(DialogueLogVisualStyle.LegacyNotebook);

    [MenuItem("Tools/Godlotto/Setup Dialogue Log/Dark Confession (⑤)")]
    public static void ApplyDarkConfession() => ApplyStyle(DialogueLogVisualStyle.DarkConfession);

    [MenuItem("Tools/Godlotto/Setup Dialogue Log Button (SayDialogNotebook)")]
    public static void ApplySayDialogNotebookLogButton()
    {
        SetupSayDialogLogButton(SayDialogNotebookPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DialogueLogEditorSetup] SayDialogNotebook 로그 버튼을 적용했습니다.");
    }

    [MenuItem("Tools/Godlotto/Setup Dialogue Log Button (Ghost, Both SayDialogs)")]
    public static void ApplyGhostLogButtonsToSayDialogs()
    {
        SetupSayDialogLogButton(SayDialogGothicPath);
        SetupSayDialogLogButton(SayDialogNotebookPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[DialogueLogEditorSetup] SayDialog Gothic·Notebook에 ghost LogButton을 적용했습니다.");
    }

    public static void ApplyStyle(DialogueLogVisualStyle style)
    {
        GameObject entryPrefab = EnsureEntryPrefab(style);
        SetupIntroScene(entryPrefab, style);
        SetupSayDialogLogButton(SayDialogGothicPath);
        SetupSayDialogLogButton(SayDialogNotebookPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DialogueLogEditorSetup] 대사 로그 에디터 연동을 완료했습니다. style={style}");
    }

    static GameObject EnsureEntryPrefab(DialogueLogVisualStyle style)
    {
        switch (style)
        {
            case DialogueLogVisualStyle.ParchmentCodex:
                return EnsureParchmentEntryPrefab(forceRebuild: true);
            case DialogueLogVisualStyle.DarkConfession:
                return EnsureDarkConfessionEntryPrefab(forceRebuild: true);
            default:
                return EnsureLegacyEntryPrefab();
        }
    }

    static GameObject EnsureLegacyEntryPrefab()
    {
        EnsureDirectory("Assets/godlotto/Prefab");

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabLegacyPath);
        if (existing != null)
            return existing;

        var palette = DialogueLogStylePalette.LegacyNotebook;
        var root = new GameObject("DialogueLogEntry", typeof(RectTransform), typeof(LayoutElement));
        var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(root.transform, false);

        SetStretch(root.GetComponent<RectTransform>());
        SetStretch(labelGo.GetComponent<RectTransform>());

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "<b>화자</b>\n대사 본문";
        label.fontSize = DialogueLogTypography.BodyFontMax;
        label.enableAutoSizing = true;
        label.fontSizeMin = DialogueLogTypography.BodyFontMin;
        label.fontSizeMax = DialogueLogTypography.BodyFontMax;
        label.lineSpacing = 10f;
        label.color = palette.BodyColor;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.raycastTarget = false;
        ApplyKoreanFont(label);

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 96f;
        layout.flexibleWidth = 1f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabLegacyPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject EnsureParchmentEntryPrefab(bool forceRebuild)
    {
        EnsureDirectory("Assets/godlotto/Prefab");

        if (!forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabParchmentPath);
            if (existing != null)
                return existing;
        }

        var palette = DialogueLogStylePalette.ParchmentCodex;

        var root = new GameObject(
            "DialogueLogEntry_Parchment",
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(DialogueLogEntryView));
        var rootRect = root.GetComponent<RectTransform>();
        SetStretch(rootRect);

        var vertical = root.AddComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.spacing = 6f;
        vertical.padding = new RectOffset(0, 0, 12, 12);

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 128f;
        layout.flexibleWidth = 1f;

        var speakerRoot = new GameObject("SpeakerRow", typeof(RectTransform), typeof(LayoutElement));
        speakerRoot.transform.SetParent(root.transform, false);
        var speakerLayout = speakerRoot.AddComponent<LayoutElement>();
        speakerLayout.minHeight = 52f;
        speakerLayout.flexibleWidth = 1f;

        var speakerLabel = CreateLabel(
            speakerRoot.transform,
            "SpeakerText",
            DialogueLogLogic.FormatSpeakerLine("Chester", DialogueLogVisualStyle.ParchmentCodex),
            48f,
            palette.SpeakerColor);
        speakerLabel.fontStyle = FontStyles.Bold;
        speakerLabel.characterSpacing = 2f;
        speakerLabel.richText = true;
        speakerLabel.enableAutoSizing = true;
        speakerLabel.fontSizeMin = DialogueLogTypography.BodyFontMin;
        speakerLabel.fontSizeMax = 48f;
        SetStretch(speakerLabel.rectTransform);

        var bodyLabel = CreateLabel(
            root.transform,
            "BodyText",
            "대사 본문이 여기에 표시됩니다.",
            DialogueLogTypography.BodyFontMax,
            palette.BodyColor);
        bodyLabel.lineSpacing = 10f;
        bodyLabel.enableAutoSizing = true;
        bodyLabel.fontSizeMin = DialogueLogTypography.BodyFontMin;
        bodyLabel.fontSizeMax = DialogueLogTypography.BodyFontMax;
        bodyLabel.margin = new Vector4(0f, 0f, 0f, 0f);
        var bodyLayout = bodyLabel.gameObject.AddComponent<LayoutElement>();
        bodyLayout.flexibleWidth = 1f;
        SetStretch(bodyLabel.rectTransform);

        var separatorGo = CreateImage(root.transform, "Separator", palette.EntrySeparator);
        var separatorRect = separatorGo.GetComponent<RectTransform>();
        separatorRect.sizeDelta = new Vector2(0f, 1f);
        var separatorLayout = separatorGo.AddComponent<LayoutElement>();
        separatorLayout.minHeight = 1f;
        separatorLayout.preferredHeight = 1f;
        separatorLayout.flexibleWidth = 1f;

        var entryView = root.GetComponent<DialogueLogEntryView>();
        WireEntryView(entryView, DialogueLogVisualStyle.ParchmentCodex, speakerRoot, speakerLabel, bodyLabel, separatorGo.GetComponent<Image>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabParchmentPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static GameObject EnsureDarkConfessionEntryPrefab(bool forceRebuild)
    {
        EnsureDirectory("Assets/godlotto/Prefab");

        if (!forceRebuild)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EntryPrefabDarkConfessionPath);
            if (existing != null)
                return existing;
        }

        var palette = DialogueLogStylePalette.DarkConfession;

        var root = new GameObject(
            "DialogueLogEntry_DarkConfession",
            typeof(RectTransform),
            typeof(LayoutElement),
            typeof(DialogueLogEntryView));
        SetStretch(root.GetComponent<RectTransform>());

        var vertical = root.AddComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        vertical.spacing = 5f;
        vertical.padding = new RectOffset(0, 0, 0, 0);

        var layout = root.GetComponent<LayoutElement>();
        layout.minHeight = 140f;
        layout.flexibleWidth = 1f;

        var speakerRoot = new GameObject("SpeakerRow", typeof(RectTransform), typeof(LayoutElement));
        speakerRoot.transform.SetParent(root.transform, false);

        var speakerRowLayout = speakerRoot.AddComponent<HorizontalLayoutGroup>();
        speakerRowLayout.childAlignment = TextAnchor.MiddleLeft;
        speakerRowLayout.childControlWidth = true;
        speakerRowLayout.childControlHeight = true;
        speakerRowLayout.childForceExpandWidth = false;
        speakerRowLayout.childForceExpandHeight = false;
        speakerRowLayout.spacing = 10f;
        speakerRowLayout.padding = new RectOffset(0, 0, 0, 0);

        var speakerRowElement = speakerRoot.GetComponent<LayoutElement>();
        speakerRowElement.minHeight = 50f;
        speakerRowElement.preferredHeight = 50f;
        speakerRowElement.flexibleWidth = 1f;

        var speakerLabel = CreateLabel(
            speakerRoot.transform,
            "SpeakerText",
            DialogueLogLogic.FormatSpeakerLine("Chester", DialogueLogVisualStyle.DarkConfession),
            46f,
            palette.SpeakerColor);
        speakerLabel.characterSpacing = 3f;
        speakerLabel.enableAutoSizing = true;
        speakerLabel.fontSizeMin = DialogueLogTypography.BodyFontMin;
        speakerLabel.fontSizeMax = 46f;
        speakerLabel.overflowMode = TextOverflowModes.Overflow;
        var speakerTextLayout = speakerLabel.gameObject.AddComponent<LayoutElement>();
        speakerTextLayout.minWidth = 48f;
        speakerTextLayout.preferredHeight = 50f;
        speakerTextLayout.flexibleWidth = 0f;

        var speakerLineGo = CreateImage(speakerRoot.transform, "SpeakerLine", palette.TitleUnderline);
        var speakerLineRect = speakerLineGo.GetComponent<RectTransform>();
        speakerLineRect.sizeDelta = new Vector2(0f, 1f);
        var speakerLineLayout = speakerLineGo.AddComponent<LayoutElement>();
        speakerLineLayout.minHeight = 1f;
        speakerLineLayout.preferredHeight = 1f;
        speakerLineLayout.flexibleWidth = 1f;
        speakerLineLayout.flexibleHeight = 0f;

        var bodyLabel = CreateLabel(
            root.transform,
            "BodyText",
            "대사 본문이 여기에 표시됩니다.",
            DialogueLogTypography.BodyFontMax,
            palette.BodyColor);
        bodyLabel.lineSpacing = 12f;
        bodyLabel.enableAutoSizing = true;
        bodyLabel.fontSizeMin = DialogueLogTypography.BodyFontMin;
        bodyLabel.fontSizeMax = DialogueLogTypography.BodyFontMax;
        var bodyLayout = bodyLabel.gameObject.AddComponent<LayoutElement>();
        bodyLayout.minHeight = 64f;
        bodyLayout.flexibleWidth = 1f;

        var entryView = root.GetComponent<DialogueLogEntryView>();
        WireEntryView(
            entryView,
            DialogueLogVisualStyle.DarkConfession,
            speakerRoot,
            speakerLabel,
            bodyLabel,
            separator: null,
            speakerLine: speakerLineGo.GetComponent<Image>());

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EntryPrefabDarkConfessionPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void WireEntryView(
        DialogueLogEntryView entryView,
        DialogueLogVisualStyle style,
        GameObject speakerRoot,
        TMP_Text speakerLabel,
        TMP_Text bodyLabel,
        Image separator,
        Image speakerLine = null)
    {
        var serialized = new SerializedObject(entryView);
        serialized.FindProperty("style").enumValueIndex = DialogueLogVisualStyleIndex.ToEnumValueIndex(style);
        serialized.FindProperty("speakerRoot").objectReferenceValue = speakerRoot;
        serialized.FindProperty("speakerLabel").objectReferenceValue = speakerLabel;
        serialized.FindProperty("speakerLine").objectReferenceValue = speakerLine;
        serialized.FindProperty("bodyLabel").objectReferenceValue = bodyLabel;
        serialized.FindProperty("entrySeparator").objectReferenceValue = separator;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetupIntroScene(GameObject entryPrefab, DialogueLogVisualStyle style)
    {
        Scene scene = EditorSceneManager.OpenScene(IntroScenePath, OpenSceneMode.Single);

        var existingManager = GameObject.Find(ManagerObjectName);
        if (existingManager != null)
            Undo.DestroyObjectImmediate(existingManager);

        var manager = new GameObject(ManagerObjectName);
        Undo.RegisterCreatedObjectUndo(manager, "Create DialogueLogManager");

        var canvasRoot = CreateCanvasRoot(manager.transform);

        var parchmentPrefab = EnsureParchmentEntryPrefab(forceRebuild: false);
        var darkPrefab = EnsureDarkConfessionEntryPrefab(forceRebuild: false);
        var legacyPrefab = EnsureLegacyEntryPrefab();

        ScrollRect parchmentScroll;
        ScrollRect darkScroll;
        ScrollRect legacyScroll;
        GameObject parchmentClose;
        GameObject darkClose;
        GameObject legacyClose;

        GameObject parchmentPanel = CreateParchmentLogPanel(canvasRoot.transform, out parchmentScroll, out parchmentClose);
        GameObject darkPanel = CreateDarkConfessionLogPanel(canvasRoot.transform, out darkScroll, out darkClose);
        GameObject legacyPanel = CreateLegacyLogPanel(canvasRoot.transform, out legacyScroll, out legacyClose);

        parchmentPanel.name = "LogPanel_Parchment";
        darkPanel.name = "LogPanel_DarkConfession";
        legacyPanel.name = "LogPanel_Legacy";

        parchmentClose.AddComponent<DialogueLogButton>();
        darkClose.AddComponent<DialogueLogButton>();
        legacyClose.AddComponent<DialogueLogButton>();

        parchmentPanel.SetActive(style == DialogueLogVisualStyle.ParchmentCodex);
        darkPanel.SetActive(style == DialogueLogVisualStyle.DarkConfession);
        legacyPanel.SetActive(style == DialogueLogVisualStyle.LegacyNotebook);

        var panelComponent = manager.AddComponent<DialogueLogPanel>();

        var serialized = new SerializedObject(panelComponent);
        serialized.FindProperty("visualStyle").enumValueIndex = DialogueLogVisualStyleIndex.ToEnumValueIndex(style);
        serialized.FindProperty("logPanel").objectReferenceValue = parchmentPanel;
        serialized.FindProperty("scrollRect").objectReferenceValue = parchmentScroll;
        serialized.FindProperty("entryPrefab").objectReferenceValue = entryPrefab;

        WireStyleLayer(serialized.FindProperty("parchmentLayer"), parchmentPanel, parchmentScroll, parchmentPrefab);
        WireStyleLayer(serialized.FindProperty("darkConfessionLayer"), darkPanel, darkScroll, darkPrefab);
        WireStyleLayer(serialized.FindProperty("legacyLayer"), legacyPanel, legacyScroll, legacyPrefab);

        serialized.FindProperty("logHotkey").enumValueIndex = (int)KeyCode.L;
        serialized.FindProperty("canvasSortingLayerName").stringValue = "Setting";
        serialized.FindProperty("canvasSortingOrder").intValue = 60;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        panelComponent.ApplyVisualStyle();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void WireStyleLayer(SerializedProperty layerProperty, GameObject panel, ScrollRect scroll, GameObject entry)
    {
        layerProperty.FindPropertyRelative("panelRoot").objectReferenceValue = panel;
        layerProperty.FindPropertyRelative("scrollRect").objectReferenceValue = scroll;
        layerProperty.FindPropertyRelative("entryPrefab").objectReferenceValue = entry;
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

    static GameObject CreateLegacyLogPanel(Transform canvasTransform, out ScrollRect scrollRect, out GameObject closeButton)
    {
        var palette = DialogueLogStylePalette.LegacyNotebook;

        var logPanel = CreateImage(canvasTransform, "LogPanel", palette.PanelBackground);
        SetStretch(logPanel.GetComponent<RectTransform>());

        CreateImage(logPanel.transform, "DimBackground", palette.DimBackground);
        var dimRect = logPanel.transform.Find("DimBackground").GetComponent<RectTransform>();
        SetStretch(dimRect);
        dimRect.SetAsFirstSibling();

        var title = CreateLabel(logPanel.transform, "TitleText", "대사 기록", 28f, palette.TitleColor);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(-48f, 48f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;

        closeButton = CreateButton(
            logPanel.transform,
            "CloseButton",
            "닫기",
            palette.CloseButtonColor,
            new Vector2(1f, 1f),
            new Vector2(-24f, -24f),
            new Vector2(96f, 40f));

        var scrollRoot = CreateScrollArea(logPanel.transform, new Vector2(24f, 24f), new Vector2(-24f, -88f), spacing: 12f);
        scrollRect = scrollRoot.GetComponent<ScrollRect>();
        return logPanel;
    }

    static GameObject CreateParchmentLogPanel(Transform canvasTransform, out ScrollRect scrollRect, out GameObject closeButton)
    {
        var palette = DialogueLogStylePalette.ParchmentCodex;

        var logPanel = new GameObject("LogPanel", typeof(RectTransform));
        logPanel.transform.SetParent(canvasTransform, false);
        SetStretch(logPanel.GetComponent<RectTransform>());

        var dim = CreateImage(logPanel.transform, "DimBackground", palette.DimBackground);
        SetStretch(dim.GetComponent<RectTransform>());

        var codexFrame = CreateImage(logPanel.transform, "CodexFrame", palette.OuterBorder);
        var codexRect = codexFrame.GetComponent<RectTransform>();
        codexRect.anchorMin = new Vector2(0.09f, 0.10f);
        codexRect.anchorMax = new Vector2(0.91f, 0.90f);
        codexRect.offsetMin = Vector2.zero;
        codexRect.offsetMax = Vector2.zero;

        var innerRing = CreateImage(codexFrame.transform, "InnerRing", palette.InnerBorder);
        var innerRingRect = innerRing.GetComponent<RectTransform>();
        innerRingRect.anchorMin = Vector2.zero;
        innerRingRect.anchorMax = Vector2.one;
        innerRingRect.offsetMin = new Vector2(3f, 3f);
        innerRingRect.offsetMax = new Vector2(-3f, -3f);

        var parchment = CreateImage(codexFrame.transform, "ParchmentBackground", palette.PanelBackground);
        var parchmentRect = parchment.GetComponent<RectTransform>();
        parchmentRect.anchorMin = Vector2.zero;
        parchmentRect.anchorMax = Vector2.one;
        parchmentRect.offsetMin = new Vector2(5f, 5f);
        parchmentRect.offsetMax = new Vector2(-5f, -5f);
        parchment.GetComponent<Image>().raycastTarget = true;

        closeButton = CreateCloseXButton(
            codexFrame.transform,
            palette.CloseButtonColor,
            new Vector2(1f, 1f),
            new Vector2(-18f, -14f),
            new Vector2(36f, 36f));

        var title = CreateLabel(
            parchment.transform,
            "TitleText",
            DialogueLogLogic.ParchmentTitleText,
            26f,
            palette.TitleColor);
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;
        title.characterSpacing = 8f;
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(-56f, 40f);

        var titleRule = CreateImage(parchment.transform, "TitleRule", palette.TitleUnderline);
        var ruleRect = titleRule.GetComponent<RectTransform>();
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(1f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.anchoredPosition = new Vector2(0f, -62f);
        ruleRect.sizeDelta = new Vector2(-44f, 2f);

        var scrollRoot = CreateScrollArea(
            parchment.transform,
            new Vector2(22f, 22f),
            new Vector2(-22f, -78f),
            spacing: 0f,
            contentPadding: new RectOffset(4, 4, 4, 4));
        scrollRect = scrollRoot.GetComponent<ScrollRect>();

        StyleScrollViewport(scrollRoot, palette.PanelBackground);

        return logPanel;
    }

    static GameObject CreateDarkConfessionLogPanel(Transform canvasTransform, out ScrollRect scrollRect, out GameObject closeButton)
    {
        var palette = DialogueLogStylePalette.DarkConfession;

        var logPanel = new GameObject("LogPanel", typeof(RectTransform));
        logPanel.transform.SetParent(canvasTransform, false);
        SetStretch(logPanel.GetComponent<RectTransform>());

        ApplyVerticalGradientBackground(logPanel.transform, palette.OuterBorder, palette.PanelBackground);

        closeButton = CreateCloseXButton(
            logPanel.transform,
            palette.CloseButtonColor,
            new Vector2(1f, 1f),
            new Vector2(-32f, -26f),
            new Vector2(40f, 40f));

        var title = CreateLabel(logPanel.transform, "TitleText", "L O G", 24f, palette.TitleColor);
        title.alignment = TextAlignmentOptions.TopLeft;
        title.characterSpacing = 14f;
        title.enableAutoSizing = false;
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(34f, -30f);
        titleRect.sizeDelta = new Vector2(-68f, 32f);

        var scrollRoot = CreateScrollArea(
            logPanel.transform,
            new Vector2(34f, 30f),
            new Vector2(-34f, -82f),
            spacing: 18f,
            contentPadding: new RectOffset(0, 0, 0, 8));
        scrollRect = scrollRoot.GetComponent<ScrollRect>();

        StyleScrollViewport(scrollRoot, new Color(0f, 0f, 0f, 0f));

        return logPanel;
    }

    static void ApplyVerticalGradientBackground(Transform parent, Color topColor, Color bottomColor)
    {
        var bottomGo = CreateImage(parent, "BackgroundBottom", bottomColor);
        SetStretch(bottomGo.GetComponent<RectTransform>());
        bottomGo.transform.SetAsFirstSibling();

        var topGo = CreateImage(parent, "BackgroundTop", topColor);
        var topRect = topGo.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 0.5f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.offsetMin = Vector2.zero;
        topRect.offsetMax = Vector2.zero;
        topGo.transform.SetAsFirstSibling();
    }

    static GameObject CreateScrollArea(
        Transform parent,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float spacing,
        RectOffset contentPadding = null)
    {
        var scrollRoot = DefaultControls.CreateScrollView(new DefaultControls.Resources());
        scrollRoot.name = "Scroll View";
        scrollRoot.transform.SetParent(parent, false);

        var scrollRectTransform = scrollRoot.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = offsetMin;
        scrollRectTransform.offsetMax = offsetMax;

        var scrollRect = scrollRoot.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        var content = scrollRect.content;
        var verticalLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.spacing = spacing;
        verticalLayout.padding = contentPadding ?? new RectOffset(8, 8, 8, 8);

        var contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return scrollRoot;
    }

    static void StyleScrollViewport(GameObject scrollRoot, Color backgroundColor)
    {
        Transform viewport = scrollRoot.transform.Find("Viewport");
        if (viewport == null)
            return;

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = backgroundColor;
            viewportImage.raycastTarget = true;
        }
    }

    static void SetupSayDialogLogButton(string prefabPath)
    {
        // 스펙 원본: docs/dialogue-log-button-03-ghost.spec.json (id: log-button-03-ghost)
        DialogueLogButtonSpec.GhostButtonSpec spec = DialogueLogButtonSpec.Load();

        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform panel = FindDeepChild(prefabRoot.transform, "Panel");
            if (panel == null)
            {
                Debug.LogError($"[DialogueLogEditorSetup] {prefabPath}에서 Panel을 찾을 수 없습니다.");
                return;
            }

            Transform existing = FindDeepChild(prefabRoot.transform, LogButtonName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            CreateGhostLogButton(panel, spec);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// log-button-03-ghost 스펙 기반 미니멀 고스트 LogButton.
    /// LogButton → Icon / Caption / Underline (VerticalLayoutGroup)
    /// </summary>
    static GameObject CreateGhostLogButton(Transform parent, DialogueLogButtonSpec.GhostButtonSpec spec)
    {
        string buttonName = string.IsNullOrEmpty(spec.buttonName) ? LogButtonName : spec.buttonName;
        var buttonGo = new GameObject(
            buttonName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(VerticalLayoutGroup));

        buttonGo.transform.SetParent(parent, false);

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = spec.anchorMin;
        rect.anchorMax = spec.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.recommendedHitArea;

        var background = buttonGo.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0f);
        background.raycastTarget = true;
        background.raycastPadding = new Vector4(12f, 12f, 12f, 12f);

        var button = buttonGo.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = spec.colorBlock.ToUnityColorBlock();

        var vertical = buttonGo.GetComponent<VerticalLayoutGroup>();
        vertical.childAlignment = TextAnchor.MiddleCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = false;
        vertical.childForceExpandHeight = false;
        vertical.spacing = spec.layoutSpacing;
        vertical.padding = new RectOffset(0, 0, 0, 0);

        Graphic iconGraphic = CreateGhostLogIcon(buttonGo.transform, spec);
        TextMeshProUGUI caption = CreateGhostLogCaption(buttonGo.transform, spec);
        GameObject underline = CreateGhostLogUnderline(buttonGo.transform, spec);

        var hover = buttonGo.AddComponent<DialogueLogGhostButtonHover>();
        hover.Initialize(underline, iconGraphic, caption, spec.foregroundIdle, spec.accent);

        buttonGo.AddComponent<DialogueLogButton>();
        return buttonGo;
    }

    static Graphic CreateGhostLogIcon(Transform parent, DialogueLogButtonSpec.GhostButtonSpec spec)
    {
        Sprite historySprite = AssetDatabase.LoadAssetAtPath<Sprite>(HistoryIconPath);
        if (historySprite != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(parent, false);

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.sizeDelta = spec.iconRenderSize;

            var iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = historySprite;
            iconImage.color = spec.foregroundIdle;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            var layout = iconGo.AddComponent<LayoutElement>();
            layout.preferredWidth = spec.iconRenderSize.x;
            layout.preferredHeight = spec.iconRenderSize.y;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
            return iconImage;
        }

        var iconLabel = CreateLabel(parent, "Icon", "\u2261", 20f, spec.foregroundIdle);
        iconLabel.alignment = TextAlignmentOptions.Center;
        iconLabel.characterSpacing = 0f;
        iconLabel.raycastTarget = false;
        var iconLabelRect = iconLabel.rectTransform;
        iconLabelRect.sizeDelta = spec.iconRenderSize;

        var textLayout = iconLabel.gameObject.AddComponent<LayoutElement>();
        textLayout.preferredWidth = spec.iconRenderSize.x;
        textLayout.preferredHeight = spec.iconRenderSize.y;
        textLayout.flexibleWidth = 0f;
        textLayout.flexibleHeight = 0f;
        return iconLabel;
    }

    static TextMeshProUGUI CreateGhostLogCaption(Transform parent, DialogueLogButtonSpec.GhostButtonSpec spec)
    {
        var caption = CreateLabel(parent, "Caption", spec.captionText, spec.captionFontSize, spec.foregroundIdle);
        caption.alignment = TextAlignmentOptions.Center;
        caption.characterSpacing = spec.captionLetterSpacing;
        caption.enableAutoSizing = false;
        caption.raycastTarget = false;

        var layout = caption.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = spec.captionFontSize + 4f;
        layout.flexibleWidth = 1f;
        return caption;
    }

    static GameObject CreateGhostLogUnderline(Transform parent, DialogueLogButtonSpec.GhostButtonSpec spec)
    {
        var underlineGo = CreateImage(parent, "Underline", spec.accent);
        var underlineRect = underlineGo.GetComponent<RectTransform>();
        underlineRect.sizeDelta = new Vector2(spec.recommendedHitArea.x * 0.7f, spec.underlineHeight);

        var underlineImage = underlineGo.GetComponent<Image>();
        underlineImage.raycastTarget = false;

        var underlineGroup = underlineGo.AddComponent<CanvasGroup>();
        underlineGroup.alpha = 0f;

        var layout = underlineGo.AddComponent<LayoutElement>();
        layout.minHeight = spec.underlineHeight;
        layout.preferredHeight = spec.underlineHeight;
        layout.preferredWidth = spec.recommendedHitArea.x * 0.7f;
        layout.flexibleWidth = 0f;

        return underlineGo;
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

    static GameObject CreateButton(
        Transform parent,
        string name,
        string labelText,
        Color buttonColor,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        var buttonGo = CreateImage(parent, name, buttonColor);
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

        var label = CreateLabel(buttonGo.transform, "Text", labelText, 18f, Color.white);
        var labelRect = label.GetComponent<RectTransform>();
        SetStretch(labelRect);
        label.alignment = TextAlignmentOptions.Center;

        return buttonGo;
    }

    static GameObject CreateCloseXButton(
        Transform parent,
        Color labelColor,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        var buttonGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.78f, 0.67f, 0.48f, 1f);
        button.colors = colors;

        var label = CreateLabel(buttonGo.transform, "Text", "X", 20f, labelColor);
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
