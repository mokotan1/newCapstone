#if UNITY_EDITOR
using System.IO;
using Godlotto.Constants;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Creates the reusable blood-drip title prefab and sample demo scene (BTD-06).
/// Menu: Disputatio/Title/Create Blood Drip Title Demo.
/// </summary>
public static class BloodDripTitleSceneBuilder
{
    const string PrefabDir = "Assets/godlotto/Prefab/Title";
    const string PrefabPath = PrefabDir + "/BloodDripTitleRoot.prefab";
    const string SceneDir = "Assets/Scenes/godlotto";
    const string ScenePath = SceneDir + "/BloodDripTitleDemo.unity";
    const string MainMenuScenePath = SceneDir + "/MainMenuScene.unity";
    const string RegistryPath = "Assets/godlotto/Resources/TitleFontRegistry.asset";
    const string FloorLineMaterialPath = "Assets/godlotto/Material/Title/FloorLine_BloodPulse.mat";
    const string FloorLineMaterialShaderName = "Sprite Shaders Ultimate/GUI SSU";

    const string LiberationSansPath =
        "Assets/Fungus/Thirdparty/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Disputatio/Title/Ensure Title Font Registry")]
    public static void EnsureTitleFontRegistry()
    {
        EnsureRegistryAsset();
        AssetDatabase.SaveAssets();
        Debug.Log("[BloodDripTitleSceneBuilder] TitleFontRegistry ready at " + RegistryPath);
    }

    [MenuItem("Disputatio/Title/Ensure Floor Line Blood Pulse Material")]
    public static void EnsureFloorLineBloodPulseMaterialMenu()
    {
        EnsureFloorLineBloodPulseMaterial();
        AssetDatabase.SaveAssets();
        Debug.Log("[BloodDripTitleSceneBuilder] Floor line material ready at " + FloorLineMaterialPath);
    }

    [MenuItem("Disputatio/Title/Apply Floor Line Material To Prefab")]
    public static void ApplyFloorLineMaterialToPrefab()
    {
        EnsureFloorLineBloodPulseMaterial();

        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var stage = prefabRoot.transform.Find("Stage") as RectTransform;
            var floorLine = stage != null ? stage.Find("FloorLine") as RectTransform : null;
            if (floorLine == null)
            {
                Debug.LogError("[BloodDripTitleSceneBuilder] FloorLine not found in prefab " + PrefabPath);
                return;
            }

            ApplyFloorLineVisuals(floorLine);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[BloodDripTitleSceneBuilder] Applied floor line material to " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("Disputatio/Title/Create Blood Drip Title Demo")]
    public static void CreateDemoSceneAndPrefab()
    {
        EnsureRegistryAsset();
        EnsureFloorLineBloodPulseMaterial();
        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(SceneDir);
        AssetDatabase.Refresh();

        GameObject titleRoot = BuildTitleRootHierarchy();
        var prefab = PrefabUtility.SaveAsPrefabAsset(titleRoot, PrefabPath);
        Object.DestroyImmediate(titleRoot);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        BuildDemoScene(prefab);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BloodDripTitleSceneBuilder] Prefab: " + PrefabPath + " | Scene: " + ScenePath);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ScenePath);
    }

    [MenuItem("Disputatio/Title/Wire Main Menu Blood Drip Effect")]
    public static void WireMainMenuBloodDripEffect()
    {
        EnsureRegistryAsset();

        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        var titleText = FindMainMenuTitleText();
        if (titleText == null)
        {
            Debug.LogError("[BloodDripTitleSceneBuilder] Main menu Title TMP not found in " + MainMenuScenePath);
            return;
        }

        var stage = titleText.transform.parent as RectTransform;
        if (stage == null)
        {
            Debug.LogError("[BloodDripTitleSceneBuilder] Title TMP has no RectTransform parent.");
            return;
        }

        var dripContainer = FindOrCreateStretchChild(stage, "BloodDripContainer");
        var floorLine = FindOrCreateFloorLine(stage);
        var bloodPool = FindOrCreateBloodPool(stage);
        var bloodFloodOverlay = FindOrCreateBloodFloodOverlay(stage);
        var renderer = FindOrCreateComponent<BloodDripTitleRenderer>(stage.gameObject);
        var adapter = FindOrCreateComponent<MainMenuBloodDripTitleAdapter>(stage.gameObject);

        WireRenderer(renderer, titleText, dripContainer, bloodPool, floorLine, bloodFloodOverlay);
        WireMainMenuAdapter(adapter, titleText, renderer);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BloodDripTitleSceneBuilder] Wired blood-drip effect on existing main menu title in " + MainMenuScenePath);
        Selection.activeGameObject = stage.gameObject;
    }

    static TextMeshProUGUI FindMainMenuTitleText()
    {
        foreach (var text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.gameObject.name == "Title")
                return text;
        }

        return null;
    }

    static RectTransform FindOrCreateStretchChild(RectTransform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null)
            return existing;

        return CreateStretchChild(parent, name);
    }

    static RectTransform FindOrCreateFloorLine(RectTransform stage)
    {
        var existing = stage.Find("FloorLine") as RectTransform;
        if (existing != null)
        {
            ApplyFloorLineVisuals(existing);
            return existing;
        }

        return CreateFloorLine(stage);
    }

    static BloodPool FindOrCreateBloodPool(RectTransform stage)
    {
        var existingTransform = stage.Find("BloodPool");
        if (existingTransform != null && existingTransform.TryGetComponent(out BloodPool existingPool))
            return existingPool;

        return CreateBloodPool(stage);
    }

    static BloodFloodOverlay FindOrCreateBloodFloodOverlay(RectTransform stage)
    {
        var existingTransform = stage.Find("BloodFloodOverlay");
        if (existingTransform != null && existingTransform.TryGetComponent(out BloodFloodOverlay existingOverlay))
        {
            existingOverlay.ApplyLayerOrder();
            return existingOverlay;
        }

        return CreateBloodFloodOverlay(stage);
    }

    static T FindOrCreateComponent<T>(GameObject host) where T : Component
    {
        if (host.TryGetComponent(out T existing))
            return existing;

        return host.AddComponent<T>();
    }

    static void WireMainMenuAdapter(
        MainMenuBloodDripTitleAdapter adapter,
        TextMeshProUGUI titleText,
        BloodDripTitleRenderer renderer)
    {
        var serialized = new SerializedObject(adapter);
        serialized.FindProperty("titleText").objectReferenceValue = titleText;
        serialized.FindProperty("renderer").objectReferenceValue = renderer;
        serialized.FindProperty("loadVisualParamsFromMock").boolValue = true;
        serialized.FindProperty("applyOnStart").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildDemoScene(GameObject prefab)
    {
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject titleInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
        titleInstance.name = "BloodDripTitleRoot";

        var stageRect = titleInstance.transform.Find("Stage") as RectTransform;
        if (stageRect != null)
        {
            stageRect.anchorMin = Vector2.zero;
            stageRect.anchorMax = Vector2.one;
            stageRect.offsetMin = new Vector2(40f, 120f);
            stageRect.offsetMax = new Vector2(-40f, -120f);
        }

        CreateDemoControls(canvasGo.transform, titleInstance.GetComponent<BloodDripTitleDemo>());
    }

    static void CreateDemoControls(Transform canvas, BloodDripTitleDemo demo)
    {
        var panel = new GameObject("DemoControls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        panel.transform.SetParent(canvas, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);
        panelRect.sizeDelta = new Vector2(640f, 56f);

        var layout = panel.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        CreateButton(panel.transform, "English Mock", demo.ApplyEnglishMock);
        CreateButton(panel.transform, "Korean Mock", demo.ApplyKoreanMock);
    }

    static void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.54f, 0.01f, 0.01f, 1f);

        var button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(buttonGo.transform, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(180f, 44f);
    }

    static GameObject BuildTitleRootHierarchy()
    {
        var root = new GameObject("TitleRoot", typeof(RectTransform), typeof(BloodDripTitleRenderer), typeof(BloodDripTitleDemo));
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var stage = CreateStage(root.transform);
        var titleText = CreateTitleText(stage);
        var dripContainer = CreateStretchChild(stage, "BloodDripContainer");
        var floorLine = CreateFloorLine(stage);
        var bloodPool = CreateBloodPool(stage);
        var bloodFloodOverlay = CreateBloodFloodOverlay(stage);

        var renderer = root.GetComponent<BloodDripTitleRenderer>();
        var demo = root.GetComponent<BloodDripTitleDemo>();

        WireRenderer(renderer, titleText, dripContainer, bloodPool, floorLine, bloodFloodOverlay);
        WireDemo(demo, renderer);

        return root;
    }

    static RectTransform CreateStage(Transform parent)
    {
        var stage = new GameObject("Stage", typeof(RectTransform), typeof(Image));
        stage.transform.SetParent(parent, false);
        var rect = stage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = stage.GetComponent<Image>();
        image.color = new Color(0.07f, 0.05f, 0.05f, 1f);
        image.raycastTarget = false;
        return rect;
    }

    static TextMeshProUGUI CreateTitleText(RectTransform stage)
    {
        var go = new GameObject("TMP_TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(stage, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(900f, 160f);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = TitleStylePayload.DefaultText;
        text.fontSize = 64f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.characterSpacing = 8f;
        text.color = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultBrightColorHex, Color.red);
        text.raycastTarget = false;

        var registry = AssetDatabase.LoadAssetAtPath<TitleFontRegistry>(RegistryPath);
        if (registry != null)
            text.font = registry.Resolve(TitleStylePayload.DefaultFontKey, TitleStylePayload.DefaultLanguage);

        return text;
    }

    static RectTransform CreateStretchChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    static RectTransform CreateFloorLine(RectTransform stage)
    {
        var go = new GameObject("FloorLine", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(stage, false);
        var rect = go.GetComponent<RectTransform>();
        ApplyFloorLineVisuals(rect);
        return rect;
    }

    static void ApplyFloorLineVisuals(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 2f);

        var image = rect.GetComponent<Image>();
        var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorLineMaterialPath);
        if (floorMaterial != null)
        {
            image.material = floorMaterial;
            image.color = Color.white;
        }
        else
            image.color = new Color(0.35f, 0.08f, 0.08f, 0.32f);

        image.raycastTarget = false;
    }

    static Material EnsureFloorLineBloodPulseMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(FloorLineMaterialPath);
        if (existing != null)
            return existing;

        var shader = Shader.Find(FloorLineMaterialShaderName);
        if (shader == null)
        {
            Debug.LogWarning(
                "[BloodDripTitleSceneBuilder] Shader not found: " + FloorLineMaterialShaderName
                + ". FloorLine will use fallback tint until SSU is available.");
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FloorLineMaterialPath) ?? "Assets/godlotto/Material/Title");

        var material = new Material(shader)
        {
            name = "FloorLine_BloodPulse",
        };

        material.SetColor("_Color", new Color(0.32f, 0.06f, 0.05f, 0.32f));
        material.EnableKeyword("_ENABLESINEGLOW_ON");
        material.SetFloat("_EnableSineGlow", 1f);
        material.SetFloat("_SineGlowFade", 0.32f);
        material.SetColor("_SineGlowColor", new Color(1.6f, 0.12f, 0.08f, 0f));
        material.SetFloat("_SineGlowContrast", 0.4f);
        material.SetFloat("_SineGlowFrequency", 0.9f);
        material.SetFloat("_SineGlowMin", 0.35f);
        material.SetFloat("_SineGlowMax", 0.62f);
        material.SetFloat("_EnableShine", 0f);
        material.SetFloat("_EnableUVDistort", 0f);
        material.SetFloat("_EnableFullDistortion", 0f);
        material.SetFloat("_EnablePingPongGlow", 0f);

        AssetDatabase.CreateAsset(material, FloorLineMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    static BloodPool CreateBloodPool(RectTransform stage)
    {
        var go = new GameObject("BloodPool", typeof(RectTransform), typeof(BloodPool));
        go.transform.SetParent(stage, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(0f, 24f);
        return go.GetComponent<BloodPool>();
    }

    static BloodFloodOverlay CreateBloodFloodOverlay(RectTransform stage)
    {
        var go = new GameObject(
            "BloodFloodOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(BloodFloodOverlayGraphic),
            typeof(BloodFloodOverlay));
        go.transform.SetParent(stage, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var overlay = go.GetComponent<BloodFloodOverlay>();
        var overlaySerialized = new SerializedObject(overlay);
        overlaySerialized.FindProperty("autoPlayOnEnable").boolValue = false;
        overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.ApplyLayerOrder();
        return overlay;
    }

    static void WireRenderer(
        BloodDripTitleRenderer renderer,
        TextMeshProUGUI titleText,
        RectTransform dripContainer,
        BloodPool bloodPool,
        RectTransform floorLine,
        BloodFloodOverlay bloodFloodOverlay)
    {
        var serialized = new SerializedObject(renderer);
        serialized.FindProperty("titleText").objectReferenceValue = titleText;
        serialized.FindProperty("dripContainer").objectReferenceValue = dripContainer;
        serialized.FindProperty("bloodPool").objectReferenceValue = bloodPool;
        serialized.FindProperty("bloodFloodOverlay").objectReferenceValue = bloodFloodOverlay;
        serialized.FindProperty("floorLine").objectReferenceValue = floorLine;
        serialized.FindProperty("fontRegistry").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<TitleFontRegistry>(RegistryPath);
        serialized.FindProperty("loadMockPayloadOnStart").boolValue = false;
        serialized.FindProperty("autoPlayBloodFlood").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireDemo(BloodDripTitleDemo demo, BloodDripTitleRenderer renderer)
    {
        var serialized = new SerializedObject(demo);
        serialized.FindProperty("renderer").objectReferenceValue = renderer;
        serialized.FindProperty("loadMockOnStart").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureRegistryAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TitleFontRegistry>(RegistryPath);
        if (existing != null)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(RegistryPath) ?? "Assets/godlotto/Resources");
        var registry = ScriptableObject.CreateInstance<TitleFontRegistry>();
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansPath);
        var nanum = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameFontPaths.KoreanBoldSdf);

        var serialized = new SerializedObject(registry);
        var fontEntries = serialized.FindProperty("fontEntries");
        fontEntries.arraySize = 2;
        SetFontEntry(fontEntries.GetArrayElementAtIndex(0), "cinzel", liberation);
        SetFontEntry(fontEntries.GetArrayElementAtIndex(1), "nanum", nanum);

        var languageFallbacks = serialized.FindProperty("languageFallbacks");
        languageFallbacks.arraySize = 2;
        SetLanguageFallback(languageFallbacks.GetArrayElementAtIndex(0), TitleFontRegistry.LanguageEnglish, liberation);
        SetLanguageFallback(languageFallbacks.GetArrayElementAtIndex(1), TitleFontRegistry.LanguageKorean, nanum);

        serialized.FindProperty("globalFallback").objectReferenceValue = liberation;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(registry, RegistryPath);
    }

    static void SetFontEntry(SerializedProperty entry, string key, TMP_FontAsset font)
    {
        entry.FindPropertyRelative("fontKey").stringValue = key;
        entry.FindPropertyRelative("fontAsset").objectReferenceValue = font;
    }

    static void SetLanguageFallback(SerializedProperty entry, string language, TMP_FontAsset font)
    {
        entry.FindPropertyRelative("languageCode").stringValue = language;
        entry.FindPropertyRelative("fontAsset").objectReferenceValue = font;
    }
}
#endif
