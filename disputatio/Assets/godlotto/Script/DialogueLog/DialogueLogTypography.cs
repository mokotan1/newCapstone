using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대사 로그 본문·화자·패널 제목 타이포. JSON(<see cref="DialogueLogStyleSpec"/>) + TMP auto-size로
/// 40pt(≈53px) 가독성과 작은 화면 clamp(40~53px)를 맞춘다.
/// </summary>
public static class DialogueLogTypography
{
    public const float BodyFontMax = 53f;
    public const float BodyFontMin = 40f;
    public const float DefaultBodyLineHeightRatio = 1.38f;

    const float TmpBaselineLineHeightRatio = 1.08f;

    public static void ApplyEntryTypography(
        DialogueLogVisualStyle style,
        TMP_Text speakerLabel,
        TMP_Text bodyLabel)
    {
        var typography = ResolveTypography(style);
        if (speakerLabel != null)
            ApplySpeaker(speakerLabel, style, typography);
        if (bodyLabel != null)
            ApplyBody(bodyLabel, style, typography);
    }

    public static void ApplyBody(TMP_Text label, DialogueLogVisualStyle style)
    {
        if (label == null)
            return;

        ApplyBody(label, style, ResolveTypography(style));
    }

    public static void ApplyTitle(TMP_Text label, DialogueLogVisualStyle style)
    {
        if (label == null)
            return;

        var typography = ResolveTypography(style);
        float titleSize = typography?.titleFontSize > 0f
            ? typography.titleFontSize
            : style switch
            {
                DialogueLogVisualStyle.DarkConfession => 34f,
                DialogueLogVisualStyle.ParchmentCodex => 36f,
                _ => 32f,
            };

        label.enableAutoSizing = false;
        label.fontSize = titleSize;
        if (typography != null && typography.titleCharacterSpacing != 0f)
            label.characterSpacing = typography.titleCharacterSpacing;
    }

    public static void ApplyEntryLayout(
        RectTransform entryRoot,
        DialogueLogVisualStyle style,
        TMP_Text speakerLabel,
        TMP_Text bodyLabel,
        Image entrySeparator)
    {
        if (entryRoot == null)
            return;

        var entrySpec = DialogueLogStyleSpec.FindStyle(style.ToString())?.entry;
        float internalSpacing = ResolveEntryInternalSpacing(style, entrySpec);

        var vertical = entryRoot.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
        {
            vertical.spacing = internalSpacing;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            vertical.padding = entrySpec != null
                ? new RectOffset(
                    Mathf.RoundToInt(entrySpec.paddingLeft),
                    Mathf.RoundToInt(entrySpec.paddingRight),
                    Mathf.RoundToInt(entrySpec.paddingTop),
                    Mathf.RoundToInt(entrySpec.paddingBottom))
                : style switch
                {
                    DialogueLogVisualStyle.DarkConfession => new RectOffset(0, 0, 12, 18),
                    DialogueLogVisualStyle.ParchmentCodex => new RectOffset(8, 8, 18, 22),
                    _ => new RectOffset(4, 4, 12, 16),
                };
        }

        if (speakerLabel != null)
        {
            ConfigureTopAlignedLayoutChild(speakerLabel.rectTransform);
            var speakerRow = speakerLabel.transform.parent as RectTransform;
            if (speakerRow != null)
            {
                ConfigureTopAlignedLayoutChild(speakerRow);
                var rowLayout = speakerRow.GetComponent<LayoutElement>();
                if (rowLayout != null)
                {
                    rowLayout.minHeight = 56f;
                    rowLayout.preferredHeight = 56f;
                    rowLayout.flexibleHeight = 0f;
                }
            }
        }

        if (bodyLabel != null)
        {
            ConfigureTopAlignedLayoutChild(bodyLabel.rectTransform);
            EnsureVerticalPreferredSize(bodyLabel.gameObject);

            var bodyLayout = bodyLabel.GetComponent<LayoutElement>();
            if (bodyLayout == null)
                bodyLayout = bodyLabel.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 64f;
            bodyLayout.flexibleWidth = 1f;
            bodyLayout.flexibleHeight = 0f;

            bodyLabel.margin = new Vector4(0f, 6f, 0f, 4f);
            bodyLabel.paragraphSpacing = 6f;
        }

        if (entrySeparator != null)
        {
            var separatorLayout = entrySeparator.GetComponent<LayoutElement>();
            if (separatorLayout == null)
                separatorLayout = entrySeparator.gameObject.AddComponent<LayoutElement>();
            separatorLayout.minHeight = 2f;
            separatorLayout.preferredHeight = 2f;
            separatorLayout.flexibleWidth = 1f;
            separatorLayout.flexibleHeight = 0f;
        }

        var rootLayout = entryRoot.GetComponent<LayoutElement>();
        if (rootLayout != null)
        {
            rootLayout.minHeight = -1f;
            rootLayout.preferredHeight = -1f;
            rootLayout.flexibleHeight = 0f;
        }
    }

    static DialogueLogStyleSpec.TypographyEntry ResolveTypography(DialogueLogVisualStyle style) =>
        DialogueLogStyleSpec.FindStyle(style.ToString())?.typography;

    static void ApplyBody(TMP_Text label, DialogueLogVisualStyle style, DialogueLogStyleSpec.TypographyEntry typography)
    {
        float max = typography?.bodyFontSize > 0f ? typography.bodyFontSize : BodyFontMax;
        float min = typography?.bodyFontSizeMin > 0f ? typography.bodyFontSizeMin : BodyFontMin;
        float ratio = typography?.bodyLineHeightRatio > 0f
            ? typography.bodyLineHeightRatio
            : DefaultBodyLineHeightRatio;
        float lineSpacing = ResolveLineSpacing(max, typography?.bodyLineSpacing ?? 0f, ratio);

        ApplyResponsive(label, max, min, lineSpacing);
    }

    static void ApplySpeaker(TMP_Text label, DialogueLogVisualStyle style, DialogueLogStyleSpec.TypographyEntry typography)
    {
        float defaultMax = style switch
        {
            DialogueLogVisualStyle.DarkConfession => 42f,
            DialogueLogVisualStyle.ParchmentCodex => 44f,
            _ => 42f,
        };

        float max = typography?.speakerFontSize > 0f ? typography.speakerFontSize : defaultMax;
        float minBase = typography?.bodyFontSizeMin > 0f ? typography.bodyFontSizeMin : BodyFontMin;
        float min = Mathf.Min(minBase - 4f, max - 6f);

        ApplyResponsive(label, max, min, lineSpacing: 2f);
        label.fontStyle = style == DialogueLogVisualStyle.ParchmentCodex
            ? FontStyles.Bold
            : label.fontStyle;

        if (typography != null && typography.speakerCharacterSpacing != 0f)
            label.characterSpacing = typography.speakerCharacterSpacing;
    }

    static float ResolveLineSpacing(float fontSize, float explicitSpacing, float ratio)
    {
        float ratioSpacing = fontSize * ratio - fontSize * TmpBaselineLineHeightRatio;
        ratioSpacing = Mathf.Max(8f, ratioSpacing);

        if (explicitSpacing > 0f)
            return Mathf.Max(explicitSpacing, ratioSpacing);

        return ratioSpacing;
    }

    static float ResolveEntryInternalSpacing(DialogueLogVisualStyle style, DialogueLogStyleSpec.LayoutEntry entrySpec)
    {
        if (entrySpec != null && entrySpec.spacing > 0f)
            return entrySpec.spacing;

        return style switch
        {
            DialogueLogVisualStyle.DarkConfession => 16f,
            DialogueLogVisualStyle.ParchmentCodex => 14f,
            _ => 12f,
        };
    }

    static void ApplyResponsive(TMP_Text label, float max, float min, float lineSpacing)
    {
        min = Mathf.Clamp(min, 8f, max);
        label.enableAutoSizing = true;
        label.fontSize = max;
        label.fontSizeMax = max;
        label.fontSizeMin = min;
        if (lineSpacing > 0f)
            label.lineSpacing = lineSpacing;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.wordWrappingRatios = 0.35f;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    static void ConfigureTopAlignedLayoutChild(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 0f);
    }

    static void EnsureVerticalPreferredSize(GameObject target)
    {
        var fitter = target.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = target.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
