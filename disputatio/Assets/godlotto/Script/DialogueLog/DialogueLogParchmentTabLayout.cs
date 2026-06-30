using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 양피지(ParchmentCodex) 로그 패널에 탭 레이아웃·콘텐츠 영역 스타일을 적용한다.
/// 글자 크기는 <see cref="DialogueLogTypography"/>·<see cref="DialogueLogStyleSpec"/>을 따른다.
/// </summary>
public static class DialogueLogParchmentTabLayout
{
    const string ParchmentPath = "CodexFrame/ParchmentBackground";

    public static void Apply(Transform panelRoot, ScrollRect scrollRect)
    {
        if (panelRoot == null || scrollRect == null)
            return;

        Transform parchment = panelRoot.Find(ParchmentPath);
        if (parchment == null)
            parchment = panelRoot;

        ApplyTitle(parchment);
        ApplyScrollRect(parchment, scrollRect);
        ApplyContentPadding(scrollRect);
    }

    static void ApplyTitle(Transform parchment)
    {
        Transform title = parchment.Find("TitleText");
        if (title == null)
            return;

        var label = title.GetComponent<TMP_Text>();
        if (label == null)
            return;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -DialogueLogTabSpec.TitleAnchoredTop);
        rect.sizeDelta = new Vector2(-56f, DialogueLogTabSpec.TitleAreaHeight);

        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = false;
        label.fontSize = DialogueLogTabSpec.TitleFontSize;
        label.lineSpacing = 0f;
        label.characterSpacing = DialogueLogTabSpec.TitleLetterSpacing;
        label.color = DialogueLogTabSpec.TitleInkColor;
    }

    static void ApplyScrollRect(Transform parchment, ScrollRect scrollRect)
    {
        var scrollTransform = scrollRect.transform as RectTransform;
        if (scrollTransform == null)
            return;

        scrollTransform.SetParent(parchment, false);
        scrollTransform.anchorMin = Vector2.zero;
        scrollTransform.anchorMax = Vector2.one;
        scrollTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollTransform.offsetMin = new Vector2(
            DialogueLogTabSpec.ContentHorizontalPadding,
            DialogueLogTabSpec.PanelBottomPadding);
        scrollTransform.offsetMax = new Vector2(
            -DialogueLogTabSpec.ContentHorizontalPadding,
            -DialogueLogTabSpec.ScrollTopInset);

        Transform viewport = scrollTransform.Find("Viewport");
        if (viewport == null)
            return;

        var viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
        {
            viewportImage.color = DialogueLogTabSpec.ContentBackground;
            viewportImage.raycastTarget = true;
        }

        var outline = viewport.GetComponent<Outline>();
        if (outline == null)
            outline = viewport.gameObject.AddComponent<Outline>();

        outline.effectColor = DialogueLogTabSpec.ContentBorderColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    static void ApplyContentPadding(ScrollRect scrollRect)
    {
        if (scrollRect.content == null)
            return;

        var layout = scrollRect.content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = scrollRect.content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 28f;
        layout.padding = new RectOffset(
            Mathf.RoundToInt(DialogueLogTabSpec.ContentHorizontalPadding),
            Mathf.RoundToInt(DialogueLogTabSpec.ContentHorizontalPadding),
            Mathf.RoundToInt(DialogueLogTabSpec.ContentVerticalPadding),
            Mathf.RoundToInt(DialogueLogTabSpec.ContentVerticalPadding));
    }
}
