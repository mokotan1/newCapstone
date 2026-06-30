using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 활성 로그 패널 루트에 <see cref="DialogueLogStylePalette"/>·JSON 타이포를 적용한다.
/// </summary>
public static class DialogueLogPanelStyleApplicator
{
    public static void Apply(GameObject panelRoot, DialogueLogVisualStyle style)
    {
        if (panelRoot == null)
            return;

        var palette = DialogueLogStylePalette.ForStyle(style);
        var spec = DialogueLogStyleSpec.FindStyle(style.ToString());

        ApplyBackground(panelRoot, palette, style);
        ApplyLabel(panelRoot, "TitleText", ResolveTitleText(style, spec), palette.TitleColor, style, spec);
        ApplyImage(panelRoot, "TitleRule", palette.TitleUnderline);
        ApplyLabel(panelRoot, "CloseButton/Text", "X", palette.CloseButtonColor, style, spec, isClose: true);
        ApplyScrollArea(panelRoot, style, spec);
    }

    static string ResolveTitleText(DialogueLogVisualStyle style, DialogueLogStyleSpec.StyleEntry spec)
    {
        if (!string.IsNullOrEmpty(spec?.titleText))
            return spec.titleText;

        return DialogueLogLogic.FormatPanelTitle(style);
    }

    static void ApplyBackground(GameObject panelRoot, DialogueLogStylePalette palette, DialogueLogVisualStyle style)
    {
        ApplyImage(panelRoot, "DimBackground", palette.DimBackground);

        switch (style)
        {
            case DialogueLogVisualStyle.ParchmentCodex:
                ApplyImage(panelRoot, "CodexFrame", palette.OuterBorder);
                ApplyImage(panelRoot, "CodexFrame/InnerRing", palette.InnerBorder);
                ApplyImage(panelRoot, "CodexFrame/ParchmentBackground", palette.PanelBackground);
                break;
            case DialogueLogVisualStyle.DarkConfession:
                ApplyImage(panelRoot, "BackgroundBottom", palette.PanelBackground);
                ApplyImage(panelRoot, "BackgroundTop", palette.OuterBorder);
                break;
            default:
                var panelImage = panelRoot.GetComponent<Image>();
                if (panelImage != null)
                    panelImage.color = palette.PanelBackground;
                break;
        }
    }

    static void ApplyLabel(
        GameObject root,
        string path,
        string text,
        Color color,
        DialogueLogVisualStyle style,
        DialogueLogStyleSpec.StyleEntry spec,
        bool isClose = false)
    {
        Transform target = string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path);
        if (target == null)
            return;

        var label = target.GetComponent<TMP_Text>();
        if (label == null)
            return;

        label.text = text;
        label.color = color;

        if (isClose)
        {
            label.enableAutoSizing = false;
            label.fontSize = 20f;
            return;
        }

        DialogueLogTypography.ApplyTitle(label, style);
    }

    static void ApplyImage(GameObject root, string path, Color color)
    {
        Transform target = root.transform.Find(path);
        if (target == null)
            return;

        var image = target.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }

    static void ApplyScrollArea(GameObject panelRoot, DialogueLogVisualStyle style, DialogueLogStyleSpec.StyleEntry spec)
    {
        Transform scrollRoot = panelRoot.transform.Find("Scroll View");
        if (scrollRoot == null)
            return;

        ApplyScrollViewportInsets(scrollRoot as RectTransform, style, spec);

        Transform content = scrollRoot.Find("Viewport/Content");
        if (content == null)
            return;

        var layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        layout.spacing = ResolveScrollEntrySpacing(style, spec);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (spec?.scroll != null)
        {
            layout.padding = new RectOffset(
                Mathf.RoundToInt(spec.scroll.paddingLeft),
                Mathf.RoundToInt(spec.scroll.paddingRight),
                Mathf.RoundToInt(spec.scroll.paddingTop),
                Mathf.RoundToInt(spec.scroll.paddingBottom));
        }
        else
        {
            layout.padding = style switch
            {
                DialogueLogVisualStyle.ParchmentCodex => new RectOffset(12, 12, 20, 16),
                DialogueLogVisualStyle.DarkConfession => new RectOffset(8, 8, 24, 16),
                _ => new RectOffset(8, 8, 16, 12),
            };
        }
    }

    static float ResolveScrollEntrySpacing(DialogueLogVisualStyle style, DialogueLogStyleSpec.StyleEntry spec)
    {
        if (spec?.scroll != null && spec.scroll.spacing > 0f)
            return spec.scroll.spacing;

        return style switch
        {
            DialogueLogVisualStyle.ParchmentCodex => 28f,
            DialogueLogVisualStyle.DarkConfession => 36f,
            _ => 20f,
        };
    }

    static void ApplyScrollViewportInsets(RectTransform scrollRect, DialogueLogVisualStyle style, DialogueLogStyleSpec.StyleEntry spec)
    {
        if (scrollRect == null)
            return;

        if (spec?.scroll != null)
        {
            scrollRect.offsetMin = new Vector2(spec.scroll.insetLeft, spec.scroll.insetBottom);
            scrollRect.offsetMax = new Vector2(-spec.scroll.insetRight, -spec.scroll.insetTop);
            return;
        }

        switch (style)
        {
            case DialogueLogVisualStyle.ParchmentCodex:
                scrollRect.offsetMin = new Vector2(
                    DialogueLogTabSpec.ContentHorizontalPadding,
                    DialogueLogTabSpec.PanelBottomPadding);
                scrollRect.offsetMax = new Vector2(
                    -DialogueLogTabSpec.ContentHorizontalPadding,
                    -DialogueLogTabSpec.ScrollTopInset);
                break;
            case DialogueLogVisualStyle.DarkConfession:
                scrollRect.offsetMin = new Vector2(34f, 30f);
                scrollRect.offsetMax = new Vector2(-34f, -108f);
                break;
        }
    }
}
