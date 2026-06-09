using System;
using System.IO;
using UnityEngine;

/// <summary>
/// <c>docs/dialogue-log-styles.json</c> 스펙 로더.
/// HTML mockup은 사람용 시안, Unity는 이 JSON(또는 <see cref="DialogueLogStylePalette"/>)을 참조한다.
/// </summary>
public static class DialogueLogStyleSpec
{
    public const string DefaultRelativePath = "docs/dialogue-log-styles.json";

    [Serializable]
    public class StylesDocument
    {
        public int version = 1;
        public StyleEntry[] styles = Array.Empty<StyleEntry>();
    }

    [Serializable]
    public class StyleEntry
    {
        public string id;
        public string displayName;
        public string titleText;
        public string speakerPrefix;
        public LayoutEntry panel;
        public LayoutEntry scroll;
        public LayoutEntry entry;
        public ColorEntry colors;
        public TypographyEntry typography;
    }

    [Serializable]
    public class LayoutEntry
    {
        public float insetLeft;
        public float insetRight;
        public float insetTop;
        public float insetBottom;
        public float spacing;
        public float paddingLeft;
        public float paddingRight;
        public float paddingTop;
        public float paddingBottom;
    }

    [Serializable]
    public class ColorEntry
    {
        public string panelBackground;
        public string outerBorder;
        public string innerBorder;
        public string title;
        public string titleAccent;
        public string titleUnderline;
        public string body;
        public string speaker;
        public string speakerLine;
        public string narration;
        public string entrySeparator;
        public string closeButton;
        public string dimBackground;
    }

    [Serializable]
    public class TypographyEntry
    {
        public float titleFontSize;
        public float titleCharacterSpacing;
        public float speakerFontSize;
        public float speakerCharacterSpacing;
        public float bodyFontSize;
        public float bodyFontSizeMin;
        public float bodyLineSpacing;
        public float bodyLineHeightRatio;
    }

    static StylesDocument cachedDocument;

    public static StylesDocument LoadFromProjectRoot(string relativePath = DefaultRelativePath)
    {
        if (cachedDocument != null)
            return cachedDocument;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string fullPath = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(fullPath))
            fullPath = Path.Combine(projectRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[DialogueLogStyleSpec] JSON not found: {fullPath}");
            cachedDocument = new StylesDocument();
            return cachedDocument;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            cachedDocument = JsonUtility.FromJson<StylesDocument>(json) ?? new StylesDocument();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogueLogStyleSpec] Failed to parse JSON: {ex.Message}");
            cachedDocument = new StylesDocument();
        }

        return cachedDocument;
    }

    public static StyleEntry FindStyle(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        var document = LoadFromProjectRoot();
        for (int i = 0; i < document.styles.Length; i++)
        {
            if (string.Equals(document.styles[i].id, id, StringComparison.OrdinalIgnoreCase))
                return document.styles[i];
        }

        return null;
    }

    public static void ClearCacheForTests() => cachedDocument = null;

    public static Color ParseColor(string rgba, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(rgba))
            return fallback;

        if (ColorUtility.TryParseHtmlString(rgba, out Color parsed))
            return parsed;

        return fallback;
    }
}
