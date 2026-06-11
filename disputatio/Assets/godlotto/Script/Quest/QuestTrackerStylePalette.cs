using UnityEngine;

/// <summary>
/// <c>docs/quest-tracker-spec.html</c> 컬러·레이아웃 토큰.
/// </summary>
public static class QuestTrackerStylePalette
{
    public const float PanelWidth = 264f;
    public const float MarginTop = 18f;
    public const float MarginRight = 16f;
    public const float PanelPaddingLeft = 15f;
    public const float PanelPaddingRight = 15f;
    public const float PanelPaddingTop = 13f;
    public const float PanelPaddingBottom = 14f;
    public const float LeftAccentWidth = 3f;
    public const float StepRowGap = 9f;
    public const float IntroSlideOffset = 48f;
    public const float IntroDurationSeconds = 0.35f;
    public const float CrossfadeDelayAfterClearSeconds = 1.5f;
    public const float CrossfadeDurationSeconds = 0.35f;

    public static readonly Color Blood = Rgb(0x8a, 0x03, 0x03);
    public static readonly Color BloodBright = Rgb(0xc1, 0x14, 0x14);
    public static readonly Color PanelTop = Rgb(0x16, 0x10, 0x10, 0.92f);
    public static readonly Color PanelBottom = Rgb(0x0c, 0x08, 0x08, 0.92f);
    public static readonly Color PanelEdge = Rgb(0x2a, 0x1d, 0x1c);
    public static readonly Color Ink = Rgb(0xf0, 0xe6, 0xd6);
    public static readonly Color InkDim = Rgb(0x9a, 0x8d, 0x79);
    public static readonly Color InkDone = Rgb(0x6f, 0x63, 0x54);
    public static readonly Color Done = Rgb(0x5e, 0x7d, 0x52);
    public static readonly Color MarkBorderPending = Rgb(0x4a, 0x3a, 0x3a);
    public static readonly Color Hint = Rgb(0x8a, 0x7a, 0x6a);
    public static readonly Color ClearedBannerDivider = Rgb(0x3a, 0x4a, 0x30);
    public static readonly Color HintDivider = Rgb(0x3a, 0x2a, 0x2a);

    public const int HeaderFontSize = 11;
    public const int QuestNameFontSize = 15;
    public const int StepFontSize = 13;
    public const int HintFontSize = 12;
    public const int ClearedBannerFontSize = 12;
    public const int MarkFontSize = 11;
    public const float MarkSize = 16f;
    public const float PulseDotSize = 6f;

    static Color Rgb(byte r, byte g, byte b, float a = 1f) =>
        new Color(r / 255f, g / 255f, b / 255f, a);
}
