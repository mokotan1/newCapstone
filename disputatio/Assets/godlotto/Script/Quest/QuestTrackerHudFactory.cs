using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 트래커 HUD uGUI 계층을 런타임에 생성한다.
/// </summary>
public static class QuestTrackerHudFactory
{
    public const string RootObjectName = "QuestTrackerHud";

    public sealed class BuiltHud
    {
        public RectTransform Root;
        public CanvasGroup CanvasGroup;
        public Image LeftAccent;
        public TextMeshProUGUI HeaderText;
        public TextMeshProUGUI QuestNameText;
        public RectTransform StepsContainer;
        public TextMeshProUGUI HintText;
        public TextMeshProUGUI ClearedBannerText;
        public QuestTrackerHudView View;
    }

    public static BuiltHud Create(Transform parent, int layer)
    {
        var rootObject = new GameObject(RootObjectName, typeof(RectTransform), typeof(CanvasGroup), typeof(QuestTrackerHudView));
        rootObject.layer = layer;
        rootObject.transform.SetParent(parent, false);

        var root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-QuestTrackerStylePalette.MarginRight, -QuestTrackerStylePalette.MarginTop);
        root.sizeDelta = new Vector2(QuestTrackerStylePalette.PanelWidth, 0f);

        var canvasGroup = rootObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image background = CreateStretchImage(root, "Background", QuestTrackerStylePalette.PanelTop);
        background.raycastTarget = false;
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = QuestTrackerStylePalette.PanelEdge;
        outline.effectDistance = new Vector2(1f, -1f);
        var shadow = background.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -8f);

        Image leftAccent = CreateLeftAccent(root);
        var content = CreateStretchRect(root, "Content");
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(
            (int)QuestTrackerStylePalette.PanelPaddingLeft,
            (int)QuestTrackerStylePalette.PanelPaddingRight,
            (int)QuestTrackerStylePalette.PanelPaddingTop,
            (int)QuestTrackerStylePalette.PanelPaddingBottom);
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI header = CreateText(content, "Header", QuestTrackerStylePalette.HeaderFontSize, QuestTrackerStylePalette.BloodBright, FontStyles.UpperCase);
        header.text = "※ 현재 임무";
        header.characterSpacing = 12f;

        TextMeshProUGUI questName = CreateText(content, "QuestName", QuestTrackerStylePalette.QuestNameFontSize, QuestTrackerStylePalette.Ink, FontStyles.Bold);
        questName.margin = new Vector4(0f, 0f, 0f, 3f);

        RectTransform stepsContainer = CreateStretchRect(content, "Steps");
        var stepsLayout = stepsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
        stepsLayout.spacing = QuestTrackerStylePalette.StepRowGap;
        stepsLayout.childAlignment = TextAnchor.UpperLeft;
        stepsLayout.childControlWidth = true;
        stepsLayout.childControlHeight = true;
        stepsLayout.childForceExpandWidth = true;
        stepsLayout.childForceExpandHeight = false;
        stepsContainer.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI hint = CreateText(content, "Hint", QuestTrackerStylePalette.HintFontSize, QuestTrackerStylePalette.Hint, FontStyles.Italic);
        hint.margin = new Vector4(0f, 8f, 0f, 0f);

        TextMeshProUGUI clearedBanner = CreateText(content, "ClearedBanner", QuestTrackerStylePalette.ClearedBannerFontSize, QuestTrackerStylePalette.Done, FontStyles.UpperCase);
        clearedBanner.alignment = TextAlignmentOptions.Center;
        clearedBanner.characterSpacing = 10f;
        clearedBanner.text = "임무 완료";
        clearedBanner.gameObject.SetActive(false);

        var view = rootObject.GetComponent<QuestTrackerHudView>();
        view.Bind(leftAccent, header, questName, stepsContainer, hint, clearedBanner, canvasGroup, root);

        return new BuiltHud
        {
            Root = root,
            CanvasGroup = canvasGroup,
            LeftAccent = leftAccent,
            HeaderText = header,
            QuestNameText = questName,
            StepsContainer = stepsContainer,
            HintText = hint,
            ClearedBannerText = clearedBanner,
            View = view
        };
    }

    public static QuestTrackerStepRowView CreateStepRow(Transform parent, int layer)
    {
        var rowObject = new GameObject("StepRow", typeof(RectTransform), typeof(QuestTrackerStepRowView), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObject.layer = layer;
        rowObject.transform.SetParent(parent, false);

        var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        var layoutElement = rowObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 20f;

        Image markBackground = CreateImage(rowObject.transform, "Mark", QuestTrackerStylePalette.MarkSize, QuestTrackerStylePalette.MarkSize);
        markBackground.color = new Color(0f, 0f, 0f, 0.12f);
        var markOutline = markBackground.gameObject.AddComponent<Outline>();
        markOutline.effectColor = QuestTrackerStylePalette.MarkBorderPending;
        markOutline.effectDistance = new Vector2(1.5f, -1.5f);

        TextMeshProUGUI markLabel = CreateText(markBackground.rectTransform, "MarkLabel", QuestTrackerStylePalette.MarkFontSize, QuestTrackerStylePalette.Done, FontStyles.Bold);
        markLabel.alignment = TextAlignmentOptions.Center;
        markLabel.rectTransform.anchorMin = Vector2.zero;
        markLabel.rectTransform.anchorMax = Vector2.one;
        markLabel.rectTransform.offsetMin = Vector2.zero;
        markLabel.rectTransform.offsetMax = Vector2.zero;

        var textColumn = CreateStretchRect(rowObject.transform, "TextColumn");
        var textColumnLayout = textColumn.gameObject.AddComponent<LayoutElement>();
        textColumnLayout.flexibleWidth = 1f;
        textColumnLayout.minWidth = 180f;

        TextMeshProUGUI stepText = CreateText(textColumn, "StepText", QuestTrackerStylePalette.StepFontSize, QuestTrackerStylePalette.InkDim, FontStyles.Normal);
        stepText.alignment = TextAlignmentOptions.TopLeft;
        stepText.textWrappingMode = TextWrappingModes.Normal;

        Image strikethrough = CreateImage(textColumn, "Strikethrough", 0f, 1f);
        strikethrough.color = new Color(QuestTrackerStylePalette.Done.r, QuestTrackerStylePalette.Done.g, QuestTrackerStylePalette.Done.b, 0.55f);
        strikethrough.enabled = false;
        var strikeRect = strikethrough.rectTransform;
        strikeRect.anchorMin = new Vector2(0f, 0.55f);
        strikeRect.anchorMax = new Vector2(1f, 0.55f);
        strikeRect.offsetMin = new Vector2(0f, -0.5f);
        strikeRect.offsetMax = new Vector2(0f, 0.5f);

        Image pulse = CreateImage(textColumn, "PulseDot", QuestTrackerStylePalette.PulseDotSize, QuestTrackerStylePalette.PulseDotSize);
        pulse.color = QuestTrackerStylePalette.BloodBright;
        pulse.enabled = false;
        var pulseRect = pulse.rectTransform;
        pulseRect.anchorMin = new Vector2(1f, 0.5f);
        pulseRect.anchorMax = new Vector2(1f, 0.5f);
        pulseRect.anchoredPosition = new Vector2(-3f, 0f);

        var rowView = rowObject.GetComponent<QuestTrackerStepRowView>();
        rowView.Bind(markBackground, markLabel, stepText, strikethrough, pulse);
        return rowView;
    }

    static Image CreateLeftAccent(RectTransform parent)
    {
        Image accent = CreateImage(parent, "LeftAccent", QuestTrackerStylePalette.LeftAccentWidth, 0f);
        accent.color = QuestTrackerStylePalette.Blood;
        var rect = accent.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(QuestTrackerStylePalette.LeftAccentWidth, 0f);
        return accent;
    }

    static RectTransform CreateStretchRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static Image CreateStretchImage(RectTransform parent, string name, Color color)
    {
        Image image = CreateImage(parent, name, 0f, 0f);
        image.color = color;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    static Image CreateImage(Transform parent, string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        if (width > 0f)
            rect.sizeDelta = new Vector2(width, height > 0f ? height : width);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, int fontSize, Color color, FontStyles fontStyle)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = ResolveDefaultFont();
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = fontStyle;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static TMP_FontAsset ResolveDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
