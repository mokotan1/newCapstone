using UnityEngine;

/// <summary>
/// 대사 로그 스타일별 rgba 팔레트. <c>docs/dialogue-log-mockups.html</c> 색상표 기준.
/// ①·⑤ 구현자가 공통으로 참조한다.
/// </summary>
[System.Serializable]
public struct DialogueLogStylePalette
{
    public Color DimBackground;
    public Color PanelBackground;
    public Color OuterBorder;
    public Color InnerBorder;
    public Color TitleColor;
    public Color TitleAccentColor;
    public Color TitleUnderline;
    public Color BodyColor;
    public Color SpeakerColor;
    public Color SpeakerOrnamentColor;
    public Color NarrationColor;
    public Color EntrySeparator;
    public Color CloseButtonColor;

    public static DialogueLogStylePalette ForStyle(DialogueLogVisualStyle style)
    {
        switch (style)
        {
            case DialogueLogVisualStyle.ParchmentCodex:
                return ParchmentCodex;
            case DialogueLogVisualStyle.DarkConfession:
                return DarkConfession;
            default:
                return LegacyNotebook;
        }
    }

    /// <summary>① 양피지 고문서 — mockups.html rgba 값.</summary>
    public static DialogueLogStylePalette ParchmentCodex => new DialogueLogStylePalette
    {
        DimBackground = Rgb(0, 0, 0, 0.55f),
        PanelBackground = Rgb(232, 220, 192),
        OuterBorder = Rgb(107, 79, 42),
        InnerBorder = Rgb(216, 199, 159),
        TitleColor = Rgb(90, 61, 28),
        TitleAccentColor = Rgb(160, 122, 58),
        TitleUnderline = Rgb(183, 154, 99),
        BodyColor = Rgb(58, 44, 26),
        SpeakerColor = Rgb(122, 74, 30),
        SpeakerOrnamentColor = Rgb(176, 124, 52),
        NarrationColor = Rgb(107, 90, 64),
        EntrySeparator = Rgb(191, 164, 115),
        CloseButtonColor = Rgb(138, 106, 56),
    };

    /// <summary>⑤ 어둠 속 고백록 — mockups.html rgba 값 (패널·항목 구현용).</summary>
    public static DialogueLogStylePalette DarkConfession => new DialogueLogStylePalette
    {
        DimBackground = Rgb(0, 0, 0, 0f),
        PanelBackground = Rgb(13, 11, 9),
        OuterBorder = Rgb(10, 9, 8),
        InnerBorder = Rgb(13, 11, 9),
        TitleColor = Rgb(110, 99, 86),
        TitleAccentColor = Rgb(110, 99, 86),
        TitleUnderline = Rgb(184, 71, 58, 0.5f),
        BodyColor = Rgb(200, 188, 168),
        SpeakerColor = Rgb(184, 71, 58),
        SpeakerOrnamentColor = Rgb(184, 71, 58),
        NarrationColor = Rgb(125, 113, 96),
        EntrySeparator = Rgb(184, 71, 58, 0.25f),
        CloseButtonColor = Rgb(90, 80, 68),
    };

    /// <summary>기존 DialogueLogEditorSetup 다크 노트북 팔레트.</summary>
    public static DialogueLogStylePalette LegacyNotebook => new DialogueLogStylePalette
    {
        DimBackground = Rgb(0, 0, 0, 0.65f),
        PanelBackground = Rgb(15, 13, 12, 0.92f),
        OuterBorder = Rgb(15, 13, 12, 0.92f),
        InnerBorder = Rgb(15, 13, 12, 0.92f),
        TitleColor = Rgb(245, 224, 189),
        TitleAccentColor = Rgb(245, 224, 189),
        TitleUnderline = Rgb(46, 38, 25),
        BodyColor = Rgb(230, 215, 189),
        SpeakerColor = Rgb(230, 215, 189),
        SpeakerOrnamentColor = Rgb(230, 215, 189),
        NarrationColor = Rgb(230, 215, 189),
        EntrySeparator = Rgb(46, 38, 25),
        CloseButtonColor = Rgb(199, 149, 59),
    };

    static Color Rgb(byte r, byte g, byte b, float a = 1f) =>
        new Color(r / 255f, g / 255f, b / 255f, a);
}
