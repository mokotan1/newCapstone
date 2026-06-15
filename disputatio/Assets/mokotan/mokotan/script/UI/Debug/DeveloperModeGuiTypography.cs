using UnityEngine;

/// <summary>
/// Dev Mode IMGUI 오버레이 전용 글자 크기. 일반 게임 UI(TMP/UGUI)와 분리됩니다.
/// </summary>
public static class DeveloperModeGuiTypography
{
    public const string PlayerPrefsKey = "DevMode.OverlayFontSize";

    public const float MinFontSize = 10f;
    public const float MaxFontSize = 25f;
    public const float DefaultFontSize = 13f;

    public const float ReferenceFontSize = DefaultFontSize;

    static float fontSize = DefaultFontSize;

    public static float FontSize => fontSize;

    public static void Load()
    {
        fontSize = Clamp(PlayerPrefs.GetFloat(PlayerPrefsKey, DefaultFontSize));
    }

    public static void SetFontSize(float size)
    {
        fontSize = Clamp(size);
        PlayerPrefs.SetFloat(PlayerPrefsKey, fontSize);
        PlayerPrefs.Save();
    }

    public static float Clamp(float size)
    {
        return Mathf.Clamp(size, MinFontSize, MaxFontSize);
    }

    public static float ScaleFactor => FontSize / ReferenceFontSize;

    public static float ScaledLength(float referenceLengthAtDefaultFont)
    {
        return referenceLengthAtDefaultFont * ScaleFactor;
    }
}
