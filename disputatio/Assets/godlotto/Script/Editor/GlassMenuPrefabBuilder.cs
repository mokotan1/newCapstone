using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택지 메뉴 다이얼로그 프리팹을 코드로 생성하는 에디터 도구.
/// 옵션 버튼은 게임의 기존 버튼 견본 <c>MainMenuButtonB.prefab</c>을 그대로 재사용한다
/// (스프라이트·폰트·색이 게임과 100% 일치). 패널 배경은 투명이라 버튼만 보인다.
/// 메뉴: Tools ▸ Godlotto ▸ Build Glass Menu Prefabs.
/// </summary>
public static class GlassMenuPrefabBuilder
{
    const string Dir = "Assets/godlotto/Resources/Prefabs";
    const string DialogPath = Dir + "/GlassMenuDialog.prefab";

    // 선택지 버튼 견본(게임 기존 버튼). 이 프리팹을 옵션 버튼으로 재사용한다.
    const string OptionButtonPrefabPath = "Assets/godlotto/Prefab/MainMenuButtonB.prefab";

    [MenuItem("Tools/Godlotto/Build Glass Menu Prefabs")]
    public static void Build()
    {
        var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionButtonPrefabPath);
        if (buttonPrefab == null)
        {
            Debug.LogError("[GlassMenuPrefabBuilder] 옵션 버튼 견본을 찾을 수 없습니다: " + OptionButtonPrefabPath);
            return;
        }

        var button = buttonPrefab.GetComponent<Button>();
        if (button == null)
        {
            Debug.LogError("[GlassMenuPrefabBuilder] 견본 프리팹에 Button 컴포넌트가 없습니다: " + OptionButtonPrefabPath);
            return;
        }

        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        BuildDialogPrefab(button);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GlassMenuPrefabBuilder] 프리팹 생성 완료: " + DialogPath);
    }

    static void BuildDialogPrefab(Button optionButton)
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

        // 패널 = 위치를 잡고 버튼을 쌓는 컨테이너. 배경 이미지 없음(투명) → 견본 버튼만 보인다.
        var panel = new GameObject("Panel",
            typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 120f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childAlignment = TextAnchor.LowerCenter;
        // 견본 버튼의 고유 크기(420x92)를 유지하기 위해 레이아웃이 크기를 통제하지 않는다.
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 컨테이너 = 패널 자신(버튼이 패널의 VerticalLayoutGroup에 쌓이도록)
        var dialog = root.GetComponent<GlassMenuDialog>();
        SetSerialized(dialog, "panelRoot", panelRt);
        SetSerialized(dialog, "optionContainer", panelRt);
        SetSerialized(dialog, "optionButtonPrefab", optionButton);

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
