using System;
using UnityEngine;

/// <summary>
/// Blood gradient colors for a single drip instance.
/// </summary>
[Serializable]
public struct BloodDripPalette
{
    public Color Main;
    public Color Dark;
    public Color Bright;

    public static BloodDripPalette FromTitleStyle(TitleStylePayload payload)
    {
        if (payload == null)
            return FromDefaults();

        return new BloodDripPalette
        {
            Main = payload.Color,
            Dark = payload.DarkColor,
            Bright = payload.BrightColor,
        };
    }

    public static BloodDripPalette FromDefaults()
    {
        return new BloodDripPalette
        {
            Main = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultColorHex, Color.red),
            Dark = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultDarkColorHex, Color.black),
            Bright = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultBrightColorHex, Color.red),
        };
    }

    public Color StreakTop => new Color(Bright.r, Bright.g, Bright.b, 0f);

    public Color StreakUpper => Bright;

    public Color StreakMid => Main;

    public Color StreakLower => Dark;

    public Color TipFill => Color.Lerp(Bright, Main, 0.35f);
}
