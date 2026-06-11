#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Builds the serif horror TMP font and material used by the main-menu title.
/// Menu: Disputatio/Title/Build Horror Title Font Assets
/// </summary>
public static class HorrorTitleFontAssetBuilder
{
    const string SourceFontPath =
        "Assets/Modern GDR - Free icons pack/01_Demo/Font/IbarraRealNova-SemiBold.ttf";

    const string OutputDir = "Assets/godlotto/Resources/TitleFonts";
    const string FontAssetPath = OutputDir + "/IbarraRealNova-SemiBold SDF.asset";
    const string MaterialPath = OutputDir + "/HorrorTitle_IbarraRealNova.mat";
    const string RegistryPath = "Assets/godlotto/Resources/TitleFontRegistry.asset";

    [MenuItem("Disputatio/Title/Build Horror Title Font Assets")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(OutputDir);
        TMP_FontAsset fontAsset = BuildFontAsset();
        if (fontAsset == null)
            return;

        Material material = BuildHorrorMaterial(fontAsset);
        UpdateTitleFontRegistry(fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HorrorTitleFontAssetBuilder] Ready: {FontAssetPath} | {MaterialPath}");
    }

    static TMP_FontAsset BuildFontAsset()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
        {
            Debug.LogError("[HorrorTitleFontAssetBuilder] Missing source font: " + SourceFontPath);
            return null;
        }

        string preservedGuid = ReadAssetGuid(FontAssetPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(FontAssetPath) != null)
            AssetDatabase.DeleteAsset(FontAssetPath);

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            samplingPointSize: 90,
            atlasPadding: 6,
            renderMode: GlyphRenderMode.SDFAA,
            atlasWidth: 4096,
            atlasHeight: 4096,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[HorrorTitleFontAssetBuilder] TMP font creation failed.");
            return null;
        }

        fontAsset.name = "IbarraRealNova-SemiBold SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
        RegisterFontAssetSubAssets(fontAsset);

        if (!string.IsNullOrEmpty(preservedGuid))
            RestoreAssetGuid(FontAssetPath, preservedGuid);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    static Material BuildHorrorMaterial(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null || fontAsset.material == null)
            return null;

        Material material = new Material(fontAsset.material);
        material.name = "HorrorTitle_IbarraRealNova";
        HorrorTitleTypography.ConfigureMaterialProperties(material);

        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            AssetDatabase.DeleteAsset(MaterialPath);

        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    static void UpdateTitleFontRegistry(TMP_FontAsset fontAsset)
    {
        TitleFontRegistry registry = AssetDatabase.LoadAssetAtPath<TitleFontRegistry>(RegistryPath);
        if (registry == null || fontAsset == null)
            return;

        SerializedObject serialized = new SerializedObject(registry);
        SerializedProperty fontEntries = serialized.FindProperty("fontEntries");
        bool updated = false;

        for (int i = 0; i < fontEntries.arraySize; i++)
        {
            SerializedProperty entry = fontEntries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("fontKey").stringValue != HorrorTitleTypography.HorrorFontKey)
                continue;

            entry.FindPropertyRelative("fontAsset").objectReferenceValue = fontAsset;
            updated = true;
            break;
        }

        if (!updated)
        {
            fontEntries.arraySize++;
            SerializedProperty entry = fontEntries.GetArrayElementAtIndex(fontEntries.arraySize - 1);
            entry.FindPropertyRelative("fontKey").stringValue = HorrorTitleTypography.HorrorFontKey;
            entry.FindPropertyRelative("fontAsset").objectReferenceValue = fontAsset;
        }

        SerializedProperty languageFallbacks = serialized.FindProperty("languageFallbacks");
        for (int i = 0; i < languageFallbacks.arraySize; i++)
        {
            SerializedProperty entry = languageFallbacks.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("languageCode").stringValue != TitleFontRegistry.LanguageEnglish)
                continue;

            entry.FindPropertyRelative("fontAsset").objectReferenceValue = fontAsset;
            break;
        }

        serialized.FindProperty("globalFallback").objectReferenceValue = fontAsset;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(registry);
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
    }

    static string ReadAssetGuid(string assetPath)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
            return null;

        Match match = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-f]+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value : null;
    }

    static void RestoreAssetGuid(string assetPath, string guid)
    {
        string metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
            return;

        string meta = File.ReadAllText(metaPath);
        meta = Regex.Replace(meta, @"^guid:\s*[0-9a-f]+", $"guid: {guid}", RegexOptions.Multiline);
        File.WriteAllText(metaPath, meta);
    }
}
#endif
