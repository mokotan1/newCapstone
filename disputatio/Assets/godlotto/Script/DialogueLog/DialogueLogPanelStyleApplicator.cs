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
        ApplyLabel(panelRoot, "TitleText", ResolveTitleText(style, spec), palette.TitleColor, spec);
        ApplyImage(panelRoot, "TitleRule", palette.TitleUnderline);
        ApplyLabel(panelRoot, "CloseButton/Text", "X", palette.CloseButtonColor, spec, isClose: true);
        ApplyScrollSpacing(panelRoot, style, spec);
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

        if (spec?.typography == null)
            return;

        if (!isClose)
        {
            if (spec.typography.titleFontSize > 0f)
                label.fontSize = spec.typography.titleFontSize;
            if (spec.typography.titleCharacterSpacing != 0f)
                label.characterSpacing = spec.typography.titleCharacterSpacing;
        }
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

    static void ApplyScrollSpacing(GameObject panelRoot, DialogueLogVisualStyle style, DialogueLogStyleSpec.StyleEntry spec)
    {
        Transform scroll = panelRoot.transform.Find("Scroll View/Viewport/Content");
        if (scroll == null)
            return;

        var layout = scroll.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            return;

        float spacing = style switch
        {
            DialogueLogVisualStyle.ParchmentCodex => 0f,
            DialogueLogVisualStyle.DarkConfession => 18f,
            _ => 12f,
        };

        if (spec?.entry != null && spec.entry.spacing > 0f)
            spacing = spec.entry.spacing;

        layout.spacing = spacing;

        if (spec?.scroll != null)
        {
            layout.padding = new RectOffset(
                Mathf.RoundToInt(spec.scroll.paddingLeft),
                Mathf.RoundToInt(spec.scroll.paddingRight),
                Mathf.RoundToInt(spec.scroll.paddingTop),
                Mathf.RoundToInt(spec.scroll.paddingBottom));
        }
    }
}
