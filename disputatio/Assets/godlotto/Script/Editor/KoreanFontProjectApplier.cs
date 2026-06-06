#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Godlotto.Constants;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 프로젝트 씬·프리팹의 TMP 한글 폰트를 Nanum Gothic SDF로 통일하고,
/// 텍스트 주변 단색 Image 배경을 투명 처리합니다.
/// </summary>
public static class KoreanFontProjectApplier
{
    static readonly string[] SearchRoots =
    {
        "Assets/Scenes",
        "Assets/godlotto",
        "Assets/mokotan",
    };

    static readonly string[] ExcludePathPrefixes =
    {
        "Assets/FungusExamples/",
        "Assets/Fungus/Thirdparty/",
    };

    public struct ApplyResult
    {
        public int AssetsProcessed;
        public int AssetsChanged;
        public int TmpComponentsUpdated;
        public int BackgroundsCleared;
    }

    [MenuItem("Tools/Godlotto/Fonts/Apply NanumGothic To Project")]
    public static void ApplyNanumGothicToProjectMenuItem()
    {
        try
        {
            ApplyResult result = ApplyNanumGothicToProject();
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[KoreanFontProjectApplier] Done: {result.AssetsChanged}/{result.AssetsProcessed} assets changed, "
                + $"{result.TmpComponentsUpdated} TMP updated, {result.BackgroundsCleared} backgrounds cleared.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[KoreanFontProjectApplier] Failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static ApplyResult ApplyNanumGothicToProject()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFontPaths.KoreanRegularSdf);
        if (font == null)
            throw new InvalidOperationException($"Missing font asset: {GameFontPaths.KoreanRegularSdf}");

        UpdateTmpSettingsDefaultFont(font);

        var result = new ApplyResult();
        foreach (string path in CollectAssetPaths())
        {
            result.AssetsProcessed++;
            if (ProcessAsset(path, font, ref result))
                result.AssetsChanged++;
        }

        return result;
    }

    static void UpdateTmpSettingsDefaultFont(TMP_FontAsset font)
    {
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings == null)
        {
            Debug.LogWarning("[KoreanFontProjectApplier] TMP Settings not found under Resources.");
            return;
        }

        TMP_Settings.defaultFontAsset = font;
        EditorUtility.SetDirty(settings);
    }

    static IReadOnlyList<string> CollectAssetPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in SearchRoots)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { root }))
                TryAddAsset(paths, AssetDatabase.GUIDToAssetPath(guid));

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                TryAddAsset(paths, AssetDatabase.GUIDToAssetPath(guid));
        }

        return paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static void TryAddAsset(ISet<string> paths, string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        if (ExcludePathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(path);
    }

    static bool ProcessAsset(string path, TMP_FontAsset font, ref ApplyResult result)
    {
        if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            return ProcessSceneAsset(path, font, ref result);

        if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            return ProcessPrefabAsset(path, font, ref result);

        return false;
    }

    static bool ProcessSceneAsset(string path, TMP_FontAsset font, ref ApplyResult result)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        int tmpUpdated = 0;
        int backgrounds = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
            ApplyHierarchy(root, font, ref tmpUpdated, ref backgrounds);

        if (tmpUpdated == 0 && backgrounds == 0)
            return false;

        result.TmpComponentsUpdated += tmpUpdated;
        result.BackgroundsCleared += backgrounds;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static bool ProcessPrefabAsset(string path, TMP_FontAsset font, ref ApplyResult result)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int tmpUpdated = 0;
        int backgrounds = 0;
        ApplyHierarchy(root, font, ref tmpUpdated, ref backgrounds);

        if (tmpUpdated == 0 && backgrounds == 0)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return false;
        }

        result.TmpComponentsUpdated += tmpUpdated;
        result.BackgroundsCleared += backgrounds;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return true;
    }

    static void ApplyHierarchy(GameObject root, TMP_FontAsset font, ref int tmpUpdated, ref int backgrounds)
    {
        tmpUpdated += ReplaceAllTmpFontAssetReferences(root, font);

        foreach (TMP_Text tmp in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.font != font || tmp.fontSharedMaterial != font.material)
            {
                tmp.font = font;
                tmp.fontSharedMaterial = font.material;
                tmpUpdated++;
                EditorUtility.SetDirty(tmp);
            }

            backgrounds += ClearTextBackgrounds(tmp);
        }
    }

    static int ReplaceAllTmpFontAssetReferences(GameObject root, TMP_FontAsset font)
    {
        int replaced = 0;
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (property.objectReferenceValue is not TMP_FontAsset current)
                    continue;

                if (!ShouldReplaceWithNanumRegular(current))
                    continue;

                if (current == font)
                    continue;

                property.objectReferenceValue = font;
                changed = true;
                replaced++;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
            }
        }

        return replaced;
    }

    static bool ShouldReplaceWithNanumRegular(TMP_FontAsset current)
    {
        string path = AssetDatabase.GetAssetPath(current);
        if (string.IsNullOrEmpty(path))
            return false;

        return path.IndexOf("NanumGothic", StringComparison.OrdinalIgnoreCase) < 0;
    }

    static int ClearTextBackgrounds(TMP_Text tmp)
    {
        int cleared = 0;
        cleared += TryClearImage(tmp.GetComponent<Image>());

        Transform background = tmp.transform.Find("Background");
        if (background != null)
            cleared += TryClearImage(background.GetComponent<Image>());

        // GlassMenu 옵션: 루트 Image(버튼/패널 채우기)는 텍스트 없이 단독이므로 TMP 형제일 때만 처리
        Transform parent = tmp.transform.parent;
        if (parent != null)
        {
            Image parentImage = parent.GetComponent<Image>();
            if (parentImage != null && parent.GetComponentInChildren<TMP_Text>(true) == tmp)
                cleared += TryClearImage(parentImage);
        }

        return cleared;
    }

    static int TryClearImage(Image image)
    {
        if (image == null || image.color.a <= 0.001f)
            return 0;

        Color color = image.color;
        color.a = 0f;
        image.color = color;
        EditorUtility.SetDirty(image);
        return 1;
    }
}
#endif
