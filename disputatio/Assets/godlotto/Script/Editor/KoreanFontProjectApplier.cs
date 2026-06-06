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

/// <summary>
/// 프로젝트 씬·프리팹의 TMP 한글 폰트를 Nanum Gothic SDF로 통일합니다.
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
                + $"{result.TmpComponentsUpdated} TMP updated.");
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

        foreach (GameObject root in scene.GetRootGameObjects())
            ApplyHierarchy(root, font, ref tmpUpdated);

        if (tmpUpdated == 0)
            return false;

        result.TmpComponentsUpdated += tmpUpdated;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return true;
    }

    static bool ProcessPrefabAsset(string path, TMP_FontAsset font, ref ApplyResult result)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int tmpUpdated = 0;
        ApplyHierarchy(root, font, ref tmpUpdated);

        if (tmpUpdated == 0)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return false;
        }

        result.TmpComponentsUpdated += tmpUpdated;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return true;
    }

    static void ApplyHierarchy(GameObject root, TMP_FontAsset font, ref int tmpUpdated)
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
}
#endif
