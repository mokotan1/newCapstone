using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class BackspaceUiPrefabBuilder
{
    private const string KoreanFontAssetPath = "Assets/Font/JalnanGothic SDF.asset";
    public const int SceneBackCanvasSortingOrder = -10;
    public const int PanelBackCanvasSortingOrder = 30;

    private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    private static readonly Color Ink = new Color(0.13f, 0.09f, 0.06f, 1f);
    private static readonly Color Paper = new Color(0.91f, 0.81f, 0.62f, 0.96f);
    private static readonly Color PaperLight = new Color(1f, 0.93f, 0.75f, 0.98f);
    private static readonly Color Gold = new Color(0.78f, 0.58f, 0.29f, 1f);
    private static readonly Color DarkVeil = new Color(0.05f, 0.045f, 0.04f, 0.86f);
    private static readonly Color BlueGrey = new Color(0.45f, 0.57f, 0.62f, 1f);

    [MenuItem("Tools/godlotto/UI/Generate Backspace UI Prefabs")]
    public static void GenerateAll()
    {
        EnsureDirectory(BackspaceUiStyleCatalog.PrefabRoot);

        SavePrefab(CreatePanelClosePrefab(), BackspaceUiStyleCatalog.PanelClosePrefabPath);
        SavePrefab(CreateChatNameplatePrefab(), BackspaceUiStyleCatalog.ChatClosePrefabPath);
        SavePrefab(CreateSceneBackRibbonPrefab(), BackspaceUiStyleCatalog.SceneBackPrefabPath);

        foreach (var variant in BackspaceUiStyleCatalog.InteractionPanelVariants)
            SavePrefab(CreateInteractionPanelPrefab(variant.style), variant.prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BackspaceUiPrefabBuilder] Backspace UI prefabs generated.");
    }

    private static GameObject CreatePanelClosePrefab()
    {
        var root = CreateCanvasRoot("PanelBackspace_CornerFold");
        var panel = CreatePanel(root.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(720f, 420f), Paper);
        CreateCornerFoldClose(panel.transform, panel);
        CreateLabel(panel.transform, "Title", "접힌 페이지", 24f, new Vector2(0f, 1f), new Vector2(28f, -34f), new Vector2(420f, 48f), Ink, TextAlignmentOptions.Left);
        CreateLabel(panel.transform, "Body", "채택된 패널 내부 백스페이스입니다. 버튼은 패널 우상단 모서리에 붙고, 누르면 이 패널만 닫힙니다.", 16f, new Vector2(0f, 1f), new Vector2(28f, -96f), new Vector2(560f, 120f), new Color(0.33f, 0.23f, 0.16f), TextAlignmentOptions.TopLeft);
        return root;
    }

    private static GameObject CreateChatNameplatePrefab()
    {
        var root = CreateCanvasRoot("ChatbotBackspace_Nameplate");
        var panel = CreatePanel(root.transform, "ChatbotPanel", new Vector2(0.5f, 0f), new Vector2(1080f, 260f), DarkVeil);
        SetAnchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(1080f, 260f));

        var face = CreateImage(panel.transform, "SpeakerBadge", new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(82f, 82f), new Color(0.83f, 0.73f, 0.54f, 0.18f));
        AddOutline(face, new Color(0.9f, 0.82f, 0.66f, 0.36f));
        CreateLabel(face.transform, "SpeakerInitial", "AI", 25f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(78f, 48f), new Color(0.9f, 0.8f, 0.62f), TextAlignmentOptions.Center);

        CreateLabel(panel.transform, "SpeakerName", "가정교사", 20f, new Vector2(0f, 1f), new Vector2(140f, -34f), new Vector2(260f, 40f), new Color(0.98f, 0.9f, 0.75f), TextAlignmentOptions.Left);
        CreateLabel(panel.transform, "ChatText", "플레이어가 언제든 대화를 닫을 수 있도록 상단 명패형 백스페이스를 사용합니다.", 18f, new Vector2(0f, 1f), new Vector2(140f, -84f), new Vector2(780f, 96f), new Color(0.82f, 0.75f, 0.64f), TextAlignmentOptions.TopLeft);

        var button = CreateButton(panel.transform, "BackspaceNameplate", new Vector2(1f, 1f), new Vector2(-38f, 18f), new Vector2(126f, 42f), Paper);
        BindPanelClose(button, root);
        CreateLabel(button.transform, "Label", "닫기  X", 15f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(104f, 30f), Ink, TextAlignmentOptions.Center);
        return root;
    }

    private static GameObject CreateSceneBackRibbonPrefab()
    {
        var root = CreateCanvasRoot("SceneBackNavigator_Ribbon");
        root.GetComponent<Canvas>().sortingOrder = SceneBackCanvasSortingOrder;

        var button = CreateButton(root.transform, "SceneBackRibbon", new Vector2(0f, 1f), new Vector2(42f, -42f), new Vector2(280f, 72f), new Color(0.07f, 0.065f, 0.06f, 0.82f));
        var navigator = button.gameObject.AddComponent<BackNavigator>();
        UnityEventTools.AddPersistentListener(button.onClick, navigator.GoBack);
        var marker = CreateImage(button.transform, "RibbonMarker", new Vector2(0f, 0.5f), new Vector2(5f, 0f), new Vector2(10f, 58f), BlueGrey);
        marker.raycastTarget = false;
        CreateLabel(button.transform, "Label", "<  이전 공간", 24f, new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(220f, 46f), new Color(0.96f, 0.88f, 0.74f), TextAlignmentOptions.Center);
        return root;
    }

    private static GameObject CreateInteractionPanelPrefab(BackspaceInteractionPanelStyle style)
    {
        var root = CreateCanvasRoot("InteractionPanel_" + style);
        var panel = CreatePanel(root.transform, "InteractionPanel", new Vector2(0.5f, 0.5f), new Vector2(760f, 460f), new Color(0.06f, 0.05f, 0.045f, 0.78f));
        var info = CreatePanel(panel.transform, "DescriptionPlate", new Vector2(0.5f, 0f), new Vector2(620f, 108f), Paper);
        SetAnchor(info.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(620f, 108f));
        CreateLabel(info.transform, "Title", GetInteractionTitle(style), 18f, new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(260f, 30f), Ink, TextAlignmentOptions.Left);
        CreateLabel(info.transform, "Body", GetInteractionBody(style), 14f, new Vector2(0f, 1f), new Vector2(18f, -48f), new Vector2(500f, 44f), new Color(0.33f, 0.23f, 0.16f), TextAlignmentOptions.TopLeft);

        switch (style)
        {
            case BackspaceInteractionPanelStyle.CornerFold:
                CreateCornerFoldClose(panel.transform, root);
                break;
            case BackspaceInteractionPanelStyle.SideTab:
                CreateSideTabClose(panel.transform, root);
                break;
            case BackspaceInteractionPanelStyle.BottomKey:
                CreateBottomKeyClose(panel.transform, root);
                break;
        }

        return root;
    }

    private static string GetInteractionTitle(BackspaceInteractionPanelStyle style)
    {
        switch (style)
        {
            case BackspaceInteractionPanelStyle.CornerFold:
                return "접힌 모서리";
            case BackspaceInteractionPanelStyle.SideTab:
                return "우측 사이드 탭";
            case BackspaceInteractionPanelStyle.BottomKey:
                return "하단 키 버튼";
            default:
                return style.ToString();
        }
    }

    private static string GetInteractionBody(BackspaceInteractionPanelStyle style)
    {
        switch (style)
        {
            case BackspaceInteractionPanelStyle.CornerFold:
                return "채택된 패널 닫기 문법을 일반 상호작용 패널에도 확장한 기본안입니다.";
            case BackspaceInteractionPanelStyle.SideTab:
                return "중앙 조작 영역을 가리지 않도록 오른쪽 손잡이처럼 배치합니다.";
            case BackspaceInteractionPanelStyle.BottomKey:
                return "오브젝트를 크게 보여주는 조사 패널에서 하단에 분리해 배치합니다.";
            default:
                return string.Empty;
        }
    }

    private static GameObject CreateCanvasRoot(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var rect = root.GetComponent<RectTransform>();
        SetAnchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = PanelBackCanvasSortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        return root;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 pivotAnchor, Vector2 size, Color color)
    {
        var panel = CreateImage(parent, name, pivotAnchor, Vector2.zero, size, color).gameObject;
        AddOutline(panel.GetComponent<Image>(), new Color(0.93f, 0.82f, 0.62f, 0.32f));
        return panel;
    }

    private static Image CreateImage(Transform parent, string name, Vector2 pivotAnchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        SetAnchor(go.GetComponent<RectTransform>(), pivotAnchor, pivotAnchor, anchoredPosition, size);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static Button CreateButton(Transform parent, string name, Vector2 pivotAnchor, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var image = CreateImage(parent, name, pivotAnchor, anchoredPosition, size, color);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.82f, 1f);
        colors.pressedColor = new Color(0.78f, 0.67f, 0.48f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        return button;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float fontSize, Vector2 pivotAnchor, Vector2 anchoredPosition, Vector2 size, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetAnchor(go.GetComponent<RectTransform>(), pivotAnchor, pivotAnchor, anchoredPosition, size);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        var fontAsset = LoadKoreanFontAsset();
        if (fontAsset != null)
        {
            label.font = fontAsset;
            label.fontSharedMaterial = fontAsset.material;
        }
        return label;
    }

    private static TMP_FontAsset LoadKoreanFontAsset()
    {
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
    }

    private static void CreateCornerFoldClose(Transform parent, GameObject target)
    {
        var button = CreateButton(parent, "BackspaceCornerFold", new Vector2(1f, 1f), Vector2.zero, new Vector2(64f, 64f), Gold);
        button.image.sprite = GetCornerFoldSprite();
        button.image.type = Image.Type.Simple;
        BindPanelClose(button, target);
        var label = CreateLabel(button.transform, "Label", "X", 18f, new Vector2(1f, 1f), new Vector2(-16f, -15f), new Vector2(28f, 28f), Ink, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
    }

    private static void CreateSideTabClose(Transform parent, GameObject target)
    {
        var button = CreateButton(parent, "BackspaceSideTab", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(48f, 84f), DarkVeil);
        BindPanelClose(button, target);
        CreateLabel(button.transform, "Label", "X", 20f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f), new Color(0.96f, 0.88f, 0.74f), TextAlignmentOptions.Center);
    }

    private static void CreateBottomKeyClose(Transform parent, GameObject target)
    {
        var button = CreateButton(parent, "BackspaceBottomKey", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(132f, 42f), DarkVeil);
        BindPanelClose(button, target);
        CreateLabel(button.transform, "Label", "닫기", 15f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 30f), new Color(0.96f, 0.88f, 0.74f), TextAlignmentOptions.Center);
    }

    private static void BindPanelClose(Button button, GameObject target)
    {
        var closer = button.gameObject.AddComponent<PanelBackspaceCloser>();
        var serialized = new SerializedObject(closer);
        serialized.FindProperty("targetPanel").objectReferenceValue = target;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddOutline(Image image, Color color)
    {
        var outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static Sprite GetCornerFoldSprite()
    {
        const string spritePath = BackspaceUiStyleCatalog.PrefabRoot + "/CornerFoldTriangle.png";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite != null)
            return sprite;

        EnsureDirectory(BackspaceUiStyleCatalog.PrefabRoot);

        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        var transparent = Clear;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool inFold = x >= y;
                texture.SetPixel(x, y, inFold ? Gold : transparent);
            }
        }

        texture.Apply();
        File.WriteAllBytes(ToProjectAbsolutePath(spritePath), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(spritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64f;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    }

    private static string ToProjectAbsolutePath(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }

    private static void EnsureDirectory(string assetPath)
    {
        var parts = assetPath.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
