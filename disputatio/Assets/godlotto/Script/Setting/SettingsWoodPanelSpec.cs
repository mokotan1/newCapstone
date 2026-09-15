using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Immutable layout and palette for SettingScene v2.
/// Logical units are 1920×1080 Unity UI units. Visual colors follow 01-ui-spec.html.
/// </summary>
public static class SettingsWoodPanelSpec
{
    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;
    public const float FrameWidth = 1680f;
    public const float FrameHeight = 936f;
    public const float FramePadding = 32f;
    public const float SidebarWidth = 256f;
    public const float ColumnGap = 32f;
    public const float ContentWidth = 1328f;
    public const float ContentHeight = 872f;
    public const float HeaderHeight = 104f;
    public const float BodyHeight = 632f;
    public const float FooterHeight = 88f;
    public const float SectionGap = 24f;
    public const float TabWidth = 256f;
    public const float TabHeight = 80f;
    public const float TabGap = 16f;
    public const float TabStartY = 136f;
    public const float TabInnerPadding = 16f;
    public const float TabMarkerSize = 8f;
    public const float TabMarkerSlot = 16f;
    public const float TabIndexSlot = 32f;
    public const float TabIndexGap = 8f;
    public const float RowHeight = 112f;
    public const float LabelColumnWidth = 976f;
    public const float LabelColumnHeight = 80f;
    public const float ControlWidth = 320f;
    public const float ControlHeight = 72f;
    public const float ReturnButtonWidth = 256f;
    public const float ReturnButtonHeight = 72f;
    public const float TitleFontSize = 40f;
    public const float UiFontSize = 24f;
    public const float CaptionFontSize = 21f;
    public const float BrandPixelFontSize = 36f;
    public const float AnswerFontSize = 27f;
    public const float DropdownCaptionPaddingLeft = 20f;
    public const float DropdownCaptionPaddingRight = 52f;
    public const float DropdownItemHeight = 64f;
    public const float DropdownListMaxItems = 5f;
    public const float DropdownListPaddingY = 8f;
    public const float DropdownListMaxHeight = 336f;
    public const float CheckboxSize = 32f;
    public const float CheckboxMarkSize = 20f;
    public const float CheckboxStatusGap = 16f;
    public const float SliderTrackWidth = 288f;
    public const float SliderTrackHeight = 8f;
    public const float SliderHandleWidth = 24f;
    public const float SliderHandleHeight = 32f;
    public const float RuleThickness = 2f;

    public const float WoodPadding = FramePadding;
    public const float WoodInnerInset = FramePadding;
    public const float DividerHeight = RuleThickness;
    public const float ContentPaddingX = 0f;
    public const float MetricsColumnWidth = 174f;
    public const float MetricsColumnGap = ColumnGap;
    public const float TabButtonMinHeight = TabHeight;
    public const float MetricPixelFontSize = 44f;
    public const float TabArrowReserve = TabMarkerSlot;
    public const float SliderWidth = SliderTrackWidth;
    public const float WoodSliceLeft = 110f;
    public const float WoodSliceBottom = 80f;
    public const float WoodSliceRight = 110f;
    public const float WoodSliceTop = 80f;
    public const float SettingsRowPaddingY = 0f;
    public const float SettingsRowGap = ColumnGap;
    public const float ToggleSize = CheckboxSize;

    public static readonly Color PanelBackground = Hex(0x201B17);
    public static readonly Color InputBackground = Hex(0x151310);
    public static readonly Color PrimaryText = Hex(0xE8D6AC);
    public static readonly Color SecondaryText = Hex(0xB3A384);
    public static readonly Color Border = Hex(0x65503A);
    public static readonly Color Accent = Hex(0xD0AB5D);
    public static readonly Color SelectedBackground = Hex(0x49351E);
    public static readonly Color SelectedBorder = Hex(0xAE8950);
    public static readonly Color ReadyStatus = Hex(0xA3B875);
    public static readonly Color ButtonFace = Hex(0x342A1E);
    public static readonly Color PrimaryButtonFace = Hex(0x78562B);
    public static readonly Color SidebarBackground = new Color(21f / 255f, 17f / 255f, 14f / 255f, 1f);
    public static readonly Color ContentBackground = new Color(27f / 255f, 23f / 255f, 19f / 255f, 1f);
    public static readonly Color OverlayDim = new Color(16f / 255f, 13f / 255f, 10f / 255f, 0.72f);

    public static Color Hex(int rgb, float alpha = 1f)
    {
        float red = ((rgb >> 16) & 0xFF) / 255f;
        float green = ((rgb >> 8) & 0xFF) / 255f;
        float blue = (rgb & 0xFF) / 255f;
        return new Color(red, green, blue, alpha);
    }

    public static float CanvasScale(float viewportWidth, float viewportHeight)
    {
        if (viewportWidth <= 0f || viewportHeight <= 0f)
            return 0f;
        return Mathf.Min(viewportWidth / ReferenceWidth, viewportHeight / ReferenceHeight);
    }

    public static Vector2 FramePixelSize(float viewportWidth, float viewportHeight)
    {
        float scale = CanvasScale(viewportWidth, viewportHeight);
        return new Vector2(FrameWidth * scale, FrameHeight * scale);
    }

    public static Vector2 FrameTopLeftPx(float viewportWidth, float viewportHeight)
    {
        Vector2 size = FramePixelSize(viewportWidth, viewportHeight);
        return new Vector2((viewportWidth - size.x) * 0.5f, (viewportHeight - size.y) * 0.5f);
    }

    public static int NormalizeScalePercent(int percent)
    {
        if (percent >= 130)
            return 140;
        if (percent >= 110)
            return 120;
        return 100;
    }

    public static float AnswerFontSizeForScale(int percent)
    {
        return AnswerFontSize * (NormalizeScalePercent(percent) / 100f);
    }

    public static float UiFontSizeForAnswerScale(int percent)
    {
        _ = percent;
        return UiFontSize;
    }

    public static void ApplyReferenceScaler(CanvasScaler scaler)
    {
        if (scaler == null)
            return;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
    }
}

public enum SettingsTabId
{
    General = 0,
    CheshireAi = 1,
    Language = 2
}

public readonly struct SettingsChromeSnapshot
{
    public readonly Vector2 SidebarPosition;
    public readonly Vector2 HeaderPosition;
    public readonly Vector2 FooterPosition;
    public readonly Vector2 SidebarSize;
    public readonly Vector2 HeaderSize;
    public readonly Vector2 FooterSize;

    public SettingsChromeSnapshot(
        Vector2 sidebarPosition,
        Vector2 headerPosition,
        Vector2 footerPosition,
        Vector2 sidebarSize,
        Vector2 headerSize,
        Vector2 footerSize)
    {
        SidebarPosition = sidebarPosition;
        HeaderPosition = headerPosition;
        FooterPosition = footerPosition;
        SidebarSize = sidebarSize;
        HeaderSize = headerSize;
        FooterSize = footerSize;
    }

    public bool Equals(SettingsChromeSnapshot other)
    {
        return SidebarPosition == other.SidebarPosition
            && HeaderPosition == other.HeaderPosition
            && FooterPosition == other.FooterPosition
            && SidebarSize == other.SidebarSize
            && HeaderSize == other.HeaderSize
            && FooterSize == other.FooterSize;
    }

    public override bool Equals(object obj)
    {
        return obj is SettingsChromeSnapshot other && Equals(other);
    }

    public override int GetHashCode()
    {
        return SidebarPosition.GetHashCode()
            ^ HeaderPosition.GetHashCode()
            ^ FooterPosition.GetHashCode();
    }

    public static bool operator ==(SettingsChromeSnapshot left, SettingsChromeSnapshot right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SettingsChromeSnapshot left, SettingsChromeSnapshot right)
    {
        return !left.Equals(right);
    }
}
