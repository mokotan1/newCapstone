using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다크 글래스 선택지 메뉴 프리팹(버튼 + 다이얼로그)을 코드로 생성하는 에디터 도구.
/// 메뉴: Tools ▸ Godlotto ▸ Build Glass Menu Prefabs.
/// </summary>
public static class GlassMenuPrefabBuilder
{
    const string Dir = "Assets/godlotto/Resources/Prefabs";
    const string ButtonPath = Dir + "/GlassMenuOptionButton.prefab";
    const string DialogPath = Dir + "/GlassMenuDialog.prefab";

    // 다크 글래스 팔레트
    static readonly Color PanelFill = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color GlassFill = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color GoldLine = new Color32(212, 175, 110, 255);
    static readonly Color LightText = new Color32(238, 242, 248, 255);

    [MenuItem("Tools/Godlotto/Build Glass Menu Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        var buttonPrefab = BuildButtonPrefab();
        BuildDialogPrefab(buttonPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GlassMenuPrefabBuilder] 프리팹 생성 완료: " + DialogPath);
    }

    static GameObject BuildButtonPrefab()
    {
        var go = new GameObject("GlassMenuOptionButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 64f);

        var img = go.GetComponent<Image>();
        img.color = GlassFill;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.55f);
        outline.effectDistance = new Vector2(1f, 1f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = GlassFill;
        colors.highlightedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.20f);
        colors.pressedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.34f);
        colors.selectedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.20f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(20f, 0f);
        labelRt.offsetMax = new Vector2(-20f, 0f);
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Option";
        label.color = LightText;
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.MidlineLeft;

        var saved = PrefabUtility.SaveAsPrefabAsset(go, ButtonPath);
        Object.DestroyImmediate(go);
        return saved;
    }

    static void BuildDialogPrefab(GameObject buttonPrefab)
    {
        // 루트: 자체 Canvas(자동 스폰 시 단독 렌더 가능)
        var root = new GameObject("GlassMenuDialog",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(GlassMenuDialog));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 패널(위치 잡히는 다크 글래스 루트)
        var panel = new GameObject("Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 120f);
        panel.GetComponent<Image>().color = PanelFill;

        var panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.55f);
        panelOutline.effectDistance = new Vector2(1f, 1f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(14, 14, 14, 14);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 컨테이너 = 패널 자신(버튼이 패널의 VerticalLayoutGroup에 쌓이도록)
        var dialog = root.GetComponent<GlassMenuDialog>();
        SetSerialized(dialog, "panelRoot", panelRt);
        SetSerialized(dialog, "optionContainer", panelRt);
        SetSerialized(dialog, "optionButtonPrefab", buttonPrefab.GetComponent<Button>());

        PrefabUtility.SaveAsPrefabAsset(root, DialogPath);
        Object.DestroyImmediate(root);
    }

    static void SetSerialized(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
