using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다크 글래스 선택지 메뉴 프리팹(버튼 + 다이얼로그)을 코드로 생성하는 에디터 도구.
/// 버튼/패널 모두 "검은 반투명 + 금색 테두리"이며, 버튼은 호버 시 금색 글로우가 뜬다.
/// 메뉴: Tools ▸ Godlotto ▸ Build Glass Menu Prefabs.
/// </summary>
public static class GlassMenuPrefabBuilder
{
    const string Dir = "Assets/godlotto/Resources/Prefabs";
    const string ButtonPath = Dir + "/GlassMenuOptionButton.prefab";
    const string DialogPath = Dir + "/GlassMenuDialog.prefab";

    // 다크 글래스 팔레트
    static readonly Color PanelFill = new Color(0f, 0f, 0f, 0.55f);   // 검은 반투명 패널
    static readonly Color ButtonFill = new Color(0f, 0f, 0f, 0.45f);  // 검은 반투명 버튼
    static readonly Color GoldLine = new Color32(212, 175, 110, 255); // 금색 테두리/글로우
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
        // 루트: 검은 반투명 배경 + 금색 테두리 + Button
        var go = new GameObject("GlassMenuOptionButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 64f);

        var bg = go.GetComponent<Image>();
        bg.color = ButtonFill;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = GoldLine;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // 호버 글로우 오버레이: 흰색 이미지를 Button ColorTint로 금색으로 칠한다.
        // (검은 배경 위에 ColorTint를 직접 걸면 곱연산이라 금색이 안 나오므로 별도 오버레이 사용)
        var glowGo = new GameObject("HoverGlow",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(go.transform, false);
        var glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;
        var glow = glowGo.GetComponent<Image>();
        glow.color = Color.white;
        glow.raycastTarget = false;

        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = glow;
        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);                              // 평상시: 글로우 없음(검은 반투명만)
        colors.highlightedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.25f); // 호버: 금색 글로우
        colors.pressedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.40f);
        colors.selectedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.25f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        // 라벨(글로우 위에 올라오도록 마지막 자식)
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

        // 패널: 검은 반투명 + 금색 테두리
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
        panelOutline.effectColor = GoldLine;
        panelOutline.effectDistance = new Vector2(1.5f, 1.5f);

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
