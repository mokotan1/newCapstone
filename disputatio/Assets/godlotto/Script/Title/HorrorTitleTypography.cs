using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// Applies classic horror title typography to an existing TMP label without changing its wording.
/// </summary>
public static class HorrorTitleTypography
{
    public const string HorrorFontKey = "cinzel";
    public const string DefaultLanguage = "en";

    public const float DefaultBaseFontSize = 92f;
    public const float FirstLineSizeScale = 1f;
    public const float SecondLineSizeScale = 0.78f;
    public const float CharacterSpacing = 7f;
    public const float LineSpacing = -6f;

    public const float OutlineWidth = 0.14f;
    public const float UnderlayOffsetX = 0.35f;
    public const float UnderlayOffsetY = -0.45f;
    public const float UnderlayDilate = 0.08f;
    public const float GlowOuter = 0.22f;
    public const float GlowPower = 0.65f;

    public const float PositionJitter = 1.1f;
    public const float RotationJitterDegrees = 1.4f;
    public const int DefaultJitterSeed = 1138;

    public const string HorrorMaterialResourcePath = "TitleFonts/HorrorTitle_IbarraRealNova";

    static readonly Color FaceColor = TitleStylePayload.ParseHexColor("#8f0b0b", new Color(0.56f, 0.04f, 0.04f));
    static readonly Color OutlineColor = TitleStylePayload.ParseHexColor("#1a0000", new Color(0.1f, 0f, 0f));
    static readonly Color UnderlayColor = new Color(0f, 0f, 0f, 0.72f);
    static readonly Color GlowColor = new Color(0.75f, 0.02f, 0.02f, 0.42f);

    public static void ApplyToMainMenu(TMP_Text text, TitleStylePayload dripPayload = null)
    {
        if (text == null)
            return;

        string plainText = ExtractPlainText(text.text);
        ApplyCoreTypography(text, plainText, dripPayload);
        EnsureCharacterJitter(text);
    }

    public static string ExtractPlainText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string stripped = value.IndexOf('<') >= 0 ? StripRichTextTags(value) : value;
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    public static string BuildLineSizedRichText(string plainText, float baseFontSize)
    {
        string trimmed = plainText.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return string.Empty;

        int lastSpace = trimmed.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace >= trimmed.Length - 1)
        {
            float singleSize = baseFontSize * FirstLineSizeScale;
            return $"<size={singleSize:F0}>{trimmed}</size>";
        }

        string firstLine = trimmed.Substring(0, lastSpace);
        string secondLine = trimmed.Substring(lastSpace + 1);
        float firstSize = baseFontSize * FirstLineSizeScale;
        float secondSize = baseFontSize * SecondLineSizeScale;

        var builder = new StringBuilder(trimmed.Length + 48);
        builder.Append("<size=").Append(firstSize.ToString("F0")).Append('>').Append(firstLine).Append("</size>");
        builder.Append('\n');
        builder.Append("<size=").Append(secondSize.ToString("F0")).Append('>').Append(secondLine).Append("</size>");
        return builder.ToString();
    }

    static void ApplyCoreTypography(TMP_Text text, string plainText, TitleStylePayload dripPayload)
    {
        TitleFontRegistry registry = TitleFontRegistry.GetOrCreate();
        TMP_FontAsset horrorFont = registry != null
            ? registry.Resolve(HorrorFontKey, dripPayload?.Language ?? DefaultLanguage)
            : text.font;

        if (horrorFont != null)
            text.font = horrorFont;

        float baseSize = text.fontSize > 0f ? text.fontSize : DefaultBaseFontSize;
        text.fontSize = baseSize;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = CharacterSpacing;
        text.lineSpacing = LineSpacing;
        text.alignment = TextAlignmentOptions.Center;
        text.text = BuildLineSizedRichText(plainText, baseSize);
        text.color = dripPayload != null ? dripPayload.Color : FaceColor;

        ApplyHorrorMaterial(text);
        text.ForceMeshUpdate();
    }

    static void ApplyHorrorMaterial(TMP_Text text)
    {
        Material preset = Resources.Load<Material>(HorrorMaterialResourcePath);
        if (preset != null && text.font != null)
        {
            Material instance = new Material(preset);
            if (text.font.atlasTexture != null)
                instance.SetTexture(ShaderUtilities.ID_MainTex, text.font.atlasTexture);

            text.fontSharedMaterial = instance;
            return;
        }

        text.outlineWidth = OutlineWidth;
        text.outlineColor = OutlineColor;
    }

    public static void ConfigureMaterialProperties(Material material)
    {
        if (material == null)
            return;

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.EnableKeyword(ShaderUtilities.Keyword_Glow);

        material.SetColor(ShaderUtilities.ID_FaceColor, FaceColor);
        material.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, UnderlayColor);
        material.SetColor(ShaderUtilities.ID_GlowColor, GlowColor);

        material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, UnderlayOffsetX);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, UnderlayOffsetY);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, UnderlayDilate);
        material.SetFloat(ShaderUtilities.ID_GlowOuter, GlowOuter);
        material.SetFloat(ShaderUtilities.ID_GlowPower, GlowPower);
    }

    static void EnsureCharacterJitter(TMP_Text text)
    {
        if (!text.TryGetComponent(out HorrorTitleCharacterJitter jitter))
            jitter = text.gameObject.AddComponent<HorrorTitleCharacterJitter>();

        jitter.Configure(DefaultJitterSeed, PositionJitter, RotationJitterDegrees);
    }

    static string StripRichTextTags(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        bool insideTag = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '<')
            {
                insideTag = true;
                continue;
            }

            if (c == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag)
                builder.Append(c);
        }

        return builder.ToString();
    }
}
