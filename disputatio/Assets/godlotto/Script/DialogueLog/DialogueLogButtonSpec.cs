using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <c>docs/dialogue-log-button-03-ghost.spec.json</c> (id: log-button-03-ghost) 스펙 모델·로더.
/// SayDialog LogButton 에디터 생성 시 참조한다.
/// </summary>
public static class DialogueLogButtonSpec
{
    public const string DefaultRelativePath = "docs/dialogue-log-button-03-ghost.spec.json";
    public const string SpecId = "log-button-03-ghost";

    [Serializable]
    public class GhostButtonSpec
    {
        public string id = SpecId;
        public string buttonName = "LogButton";
        public Vector2 anchorMin = new Vector2(1f, 0f);
        public Vector2 anchoredPosition = new Vector2(-130f, 38f);
        public Vector2 recommendedHitArea = new Vector2(56f, 56f);
        public float layoutSpacing = 4f;
        public string captionText = "로그";
        public float captionFontSize = 12f;
        public float captionLetterSpacing = 2f;
        public Vector2 iconRenderSize = new Vector2(24f, 24f);
        public float underlineHeight = 1f;
        public float underlineMarginTop = 2f;
        public Color foregroundIdle = new Color(0.839f, 0.745f, 0.588f, 0.62f);
        public Color foregroundOpaque = new Color(0.839f, 0.745f, 0.588f, 1f);
        public Color accent = new Color(0.906f, 0.788f, 0.471f, 1f);
        public ColorBlockData colorBlock = ColorBlockData.GhostDefaults();
    }

    [Serializable]
    public struct ColorBlockData
    {
        public Color normalColor;
        public Color highlightedColor;
        public Color pressedColor;
        public Color selectedColor;
        public Color disabledColor;
        public float colorMultiplier;
        public float fadeDuration;

        public static ColorBlockData GhostDefaults() => new ColorBlockData
        {
            normalColor = new Color(0.839f, 0.745f, 0.588f, 0.62f),
            highlightedColor = new Color(0.906f, 0.788f, 0.471f, 1f),
            pressedColor = new Color(0.906f, 0.788f, 0.471f, 1f),
            selectedColor = new Color(0.839f, 0.745f, 0.588f, 0.62f),
            disabledColor = new Color(0.839f, 0.745f, 0.588f, 0.25f),
            colorMultiplier = 1f,
            fadeDuration = 0.15f,
        };

        public ColorBlock ToUnityColorBlock()
        {
            return new ColorBlock
            {
                normalColor = normalColor,
                highlightedColor = highlightedColor,
                pressedColor = pressedColor,
                selectedColor = selectedColor,
                disabledColor = disabledColor,
                colorMultiplier = colorMultiplier,
                fadeDuration = fadeDuration,
            };
        }
    }

    static GhostButtonSpec cachedSpec;

    public static GhostButtonSpec Load(string relativePath = DefaultRelativePath)
    {
        if (cachedSpec != null)
            return cachedSpec;

        cachedSpec = CreateDefaults();
        string fullPath = ResolveSpecPath(relativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[DialogueLogButtonSpec] JSON not found, using embedded defaults: {fullPath}");
            return cachedSpec;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            ApplyJson(cachedSpec, JObject.Parse(json));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogueLogButtonSpec] Failed to parse JSON, using embedded defaults: {ex.Message}");
        }

        return cachedSpec;
    }

    public static void ClearCacheForTests() => cachedSpec = null;

    static GhostButtonSpec CreateDefaults() => new GhostButtonSpec();

    static string ResolveSpecPath(string relativePath)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        string repoPath = Path.Combine(repoRoot, relativePath);
        if (File.Exists(repoPath))
            return repoPath;
        return Path.Combine(projectRoot, relativePath);
    }

    static void ApplyJson(GhostButtonSpec spec, JObject root)
    {
        if (root == null)
            return;

        spec.id = root.Value<string>("id") ?? spec.id;

        JToken target = root["target"];
        if (target != null)
            spec.buttonName = target.Value<string>("buttonName") ?? spec.buttonName;

        JToken container = root["container"];
        if (container != null)
            spec.layoutSpacing = container.Value<float?>("spacing") ?? spec.layoutSpacing;

        JToken caption = root["caption"];
        if (caption != null)
        {
            spec.captionText = caption.Value<string>("text") ?? spec.captionText;
            spec.captionFontSize = caption.Value<float?>("fontSize") ?? spec.captionFontSize;
            spec.captionLetterSpacing = caption.Value<float?>("letterSpacing") ?? spec.captionLetterSpacing;
        }

        JToken icon = root["icon"];
        if (icon?["renderSize"] is JArray renderSize && renderSize.Count >= 2)
        {
            spec.iconRenderSize = new Vector2(renderSize[0].Value<float>(), renderSize[1].Value<float>());
        }

        JToken underline = root["underline"];
        if (underline != null)
        {
            spec.underlineHeight = underline.Value<float?>("height") ?? spec.underlineHeight;
            spec.underlineMarginTop = underline.Value<float?>("marginTop") ?? spec.underlineMarginTop;
        }

        JToken layoutHints = root["layoutHints"];
        if (layoutHints?["recommendedHitArea"] is JArray hitArea && hitArea.Count >= 2)
        {
            spec.recommendedHitArea = new Vector2(hitArea[0].Value<float>(), hitArea[1].Value<float>());
        }

        JToken colors = root["colors"];
        if (colors != null)
        {
            spec.foregroundIdle = ParseUnityColor(colors["color.fg"], spec.foregroundIdle);
            spec.foregroundOpaque = ParseUnityColor(colors["color.fgOpaque"], spec.foregroundOpaque);
            spec.accent = ParseUnityColor(colors["color.accent"], spec.accent);
        }

        JToken interaction = root["interaction"];
        if (interaction?["unityColorBlock"] is JObject colorBlock)
        {
            spec.colorBlock = new ColorBlockData
            {
                normalColor = ParseUnityColor(colorBlock["normalColor"], spec.colorBlock.normalColor),
                highlightedColor = ParseUnityColor(colorBlock["highlightedColor"], spec.colorBlock.highlightedColor),
                pressedColor = ParseUnityColor(colorBlock["pressedColor"], spec.colorBlock.pressedColor),
                selectedColor = ParseUnityColor(colorBlock["selectedColor"], spec.colorBlock.selectedColor),
                disabledColor = ParseUnityColor(colorBlock["disabledColor"], spec.colorBlock.disabledColor),
                colorMultiplier = colorBlock.Value<float?>("colorMultiplier") ?? spec.colorBlock.colorMultiplier,
                fadeDuration = colorBlock.Value<float?>("fadeDuration") ?? spec.colorBlock.fadeDuration,
            };
        }
    }

    static Color ParseUnityColor(JToken token, Color fallback)
    {
        if (token == null)
            return fallback;

        if (token is JArray rgba && rgba.Count >= 3)
        {
            float a = rgba.Count >= 4 ? rgba[3].Value<float>() : 1f;
            return new Color(rgba[0].Value<float>(), rgba[1].Value<float>(), rgba[2].Value<float>(), a);
        }

        if (token is JObject obj && obj["unityColor"] is JArray unityColor && unityColor.Count >= 3)
        {
            float a = unityColor.Count >= 4 ? unityColor[3].Value<float>() : 1f;
            return new Color(unityColor[0].Value<float>(), unityColor[1].Value<float>(), unityColor[2].Value<float>(), a);
        }

        string hex = token.Value<string>("hex");
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            return parsed;

        return fallback;
    }
}
