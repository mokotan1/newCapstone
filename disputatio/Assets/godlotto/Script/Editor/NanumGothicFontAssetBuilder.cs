#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using Godlotto.Constants;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Nanum Gothic OTF를 TMP SDF Font Asset으로 변환한다.
/// 메뉴: Tools ▸ Godlotto ▸ Fonts ▸ Build NanumGothic SDF Assets
/// </summary>
public static class NanumGothicFontAssetBuilder
{
    private const string FontRoot = "Assets/Font";

    private static readonly (string source, string output)[] Targets =
    {
        ("NanumGothic.otf", Path.GetFileName(GameFontPaths.KoreanRegularSdf)),
        ("NanumGothicBold.otf", Path.GetFileName(GameFontPaths.KoreanBoldSdf)),
        ("NanumGothicLight.otf", Path.GetFileName(GameFontPaths.KoreanLightSdf)),
        ("NanumGothicExtraBold.otf", Path.GetFileName(GameFontPaths.KoreanExtraBoldSdf)),
    };

    [MenuItem("Tools/Godlotto/Fonts/Build NanumGothic SDF Assets")]
    public static void BuildAll()
    {
        var created = 0;
        foreach (var (source, output) in Targets)
        {
            if (BuildOne(source, output))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[NanumGothicFontAssetBuilder] {created}/{Targets.Length} TMP SDF assets ready under {FontRoot}.");
    }

    private static bool BuildOne(string sourceFileName, string outputFileName)
    {
        var sourcePath = $"{FontRoot}/{sourceFileName}";
        var outputPath = $"{FontRoot}/{outputFileName}";

        var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (font == null)
        {
            Debug.LogWarning($"[NanumGothicFontAssetBuilder] Missing source font: {sourcePath}");
            return false;
        }

        var preservedGuid = ReadAssetGuid(outputPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(outputPath) != null)
            AssetDatabase.DeleteAsset(outputPath);

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            samplingPointSize: 72,
            atlasPadding: 5,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 4096,
            atlasHeight: 4096,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError($"[NanumGothicFontAssetBuilder] Failed to create TMP asset for {sourcePath}");
            return false;
        }

        fontAsset.name = Path.GetFileNameWithoutExtension(outputFileName);
        AssetDatabase.CreateAsset(fontAsset, outputPath);
        RegisterFontAssetSubAssets(fontAsset);

        if (!string.IsNullOrEmpty(preservedGuid))
            RestoreAssetGuid(outputPath, preservedGuid);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

        var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
        if (saved == null || saved.atlasTextures == null || saved.atlasTextures.Length == 0 || saved.atlasTextures[0] == null)
        {
            Debug.LogError($"[NanumGothicFontAssetBuilder] Atlas texture missing after save: {outputPath}");
            return false;
        }

        Debug.Log($"[NanumGothicFontAssetBuilder] Created {outputPath}");
        return true;
    }

    static void RegisterFontAssetSubAssets(TMP_FontAsset fontAsset)
    {
        if (fontAsset.material != null)
        {
            fontAsset.material.name = $"{fontAsset.name} Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        Texture2D[] atlases = fontAsset.atlasTextures;
        if (atlases == null)
            return;

        for (int i = 0; i < atlases.Length; i++)
        {
            Texture2D atlas = atlases[i];
            if (atlas == null)
                continue;

            atlas.name = atlases.Length == 1
                ? $"{fontAsset.name} Atlas"
                : $"{fontAsset.name} Atlas {i}";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }

        if (fontAsset.material != null && fontAsset.atlasTexture != null)
            fontAsset.material.mainTexture = fontAsset.atlasTexture;

        EditorUtility.SetDirty(fontAsset);
        if (fontAsset.material != null)
            EditorUtility.SetDirty(fontAsset.material);
    }

    static string ReadAssetGuid(string assetPath)
    {
        var metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
            return null;

        var match = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-f]+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    static void RestoreAssetGuid(string assetPath, string guid)
    {
        var metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
            return;

        var meta = File.ReadAllText(metaPath);
        meta = Regex.Replace(meta, @"^guid:\s*[0-9a-f]+", $"guid: {guid}", RegexOptions.Multiline);
        File.WriteAllText(metaPath, meta);
    }
}
#endif
