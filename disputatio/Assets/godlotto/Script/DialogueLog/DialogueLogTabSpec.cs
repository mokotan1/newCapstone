using UnityEngine;

/// <summary>
/// Dialogue Log Tab HTML 스펙(690×380 양피지 패널) 치수·색상.
/// </summary>
public static class DialogueLogTabSpec
{
    public const float TabBarWidth = 372f;
    public const float TabBarHeight = 62f;
    public const float TabWidth = 176f;
    public const float TabFontSize = 34f;
    public const float TabUnderlineHeight = 4f;
    public const float TabUnderlineInset = 28f;

    public const float TitleAnchoredTop = 8f;
    public const float TitleAreaHeight = 24f;
    public const float TitleFontSize = 18f;
    public const float TitleLetterSpacing = 4f;
    public const float TitleMarginBottom = 10f;
    public const float TabBarMarginBottom = 12f;
    public const float PanelBottomPadding = 12f;
    public const float ContentHeight = 252f;
    public const float ContentVerticalPadding = 22f;
    public const float ContentHorizontalPadding = 24f;
    public const float EmptyFontSize = 22f;

    public const string EmptyDialogueText = "아직 기록된 대사가 없습니다.";
    public const string EmptyCheshireText = "아직 기록된 체셔 대화가 없습니다.";

    public static readonly Color TabInactiveColor = Rgb(63, 45, 24, 0.55f);
    public static readonly Color TabActiveColor = Rgb(63, 45, 24, 1f);
    public static readonly Color TabBarBorderColor = Rgb(123, 93, 44, 0.35f);
    public static readonly Color TabUnderlineColor = Rgb(139, 107, 49, 1f);
    public static readonly Color ContentBackground = Rgb(255, 252, 241, 0.45f);
    public static readonly Color ContentBorderColor = Rgb(154, 121, 63, 0.12f);
    public static readonly Color EmptyTextColor = Rgb(63, 45, 24, 0.42f);
    public static readonly Color TitleInkColor = Rgb(63, 45, 24, 1f);

    public static float ScrollTopInset =>
        TitleAnchoredTop + TitleAreaHeight + TitleMarginBottom + TabBarHeight + TabBarMarginBottom;

    public static float TabBarAnchoredTop =>
        TitleAnchoredTop + TitleAreaHeight + TitleMarginBottom;

    static Color Rgb(byte r, byte g, byte b, float a) =>
        new Color(r / 255f, g / 255f, b / 255f, a);
}

