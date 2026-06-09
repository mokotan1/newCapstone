#if UNITY_EDITOR
using System.IO;
using Mokotan.StandingDialogue;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RPG 스탠딩 대사 UI 프리팹을 한 번에 생성합니다. 레이아웃·Canvas 설정은 Fungus 기본 SayDialog 프리팹
/// (Assets/Fungus/Resources/Prefabs/SayDialog.prefab)의 Panel·NameText·StoryText를 벤치마크합니다.
/// Unity 메뉴에서 실행하세요.
/// </summary>
public static class StandingDialoguePrefabCreator
{
    const string PrefabPath = "Assets/mokotan/mokotan/script/StandingDialogue/StandingDialogueCanvas.prefab";
    /// <summary>Resources.Load("Prefabs/StandingDialogueCanvas") — GetStandingDialogue() 자동 생성용.</summary>
    const string ResourcesPrefabPath = "Assets/mokotan/mokotan/Resources/Prefabs/StandingDialogueCanvas.prefab";
    const string DialogSpritePath = "Assets/Fungus/Textures/DialogBoxSliced.png";
    const string TmpFontPath = Godlotto.Constants.GameFontPaths.KoreanRegularSdf;

    // SayDialog.prefab — Panel / NameText / StoryText (참조용 수치)
    const float SayDialogPanelWidth = 1500f;
    const float SayDialogPanelHeight = 335f;
    const float SayDialogNameTextWidth = 1106f;
    const float SayDialogNameTextHeight = 71f;

    /// <summary>
    /// CI/배치: Unity -batchmode -executeMethod StandingDialoguePrefabCreator.CreatePrefabBatch -quit
    /// </summary>
    public static void CreatePrefabBatch()
    {
        CreatePrefab();
    }

    [MenuItem("Mokotan/Standing Dialogue/Create StandingDialogueCanvas Prefab", false, 100)]
    public static void CreatePrefab()
    {
        Sprite uiSprite = LoadFirstSprite(DialogSpritePath);
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);

        var root = new GameObject(
            "StandingDialogueCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(StandingDialogueManager));

        root.layer = 5;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referencePixelsPerUnit = 32f;
        scaler.referenceResolution = new Vector2(1600f, 1200f);
        scaler.matchWidthOrHeight = 1f;

        var rtRoot = root.GetComponent<RectTransform>();
        StretchFull(rtRoot);

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        Image characterDimBackdrop = CreateUiImage("CharacterDimBackdrop", root.transform, uiSprite, preserveAspect: false);
        characterDimBackdrop.raycastTarget = false;
        characterDimBackdrop.color = new Color(0f, 0f, 0f, IntroStandingDialogueOffsetPolicy.BackdropAlpha);
        characterDimBackdrop.gameObject.SetActive(false);

        var leftSlot = CreateUiChild("LeftSlot", root.transform);
        StretchLeftHalf(leftSlot.GetComponent<RectTransform>());

        Image leftOverlay = CreateUiImage("LeftOverlay", leftSlot.transform, uiSprite, preserveAspect: false);
        leftOverlay.raycastTarget = false;
        leftOverlay.color = new Color(0f, 0f, 0f, 0f);

        Image leftChar = CreateUiImage("LeftCharImage", leftSlot.transform, uiSprite, preserveAspect: true);
        leftChar.raycastTarget = false;
        SetImageAlpha(leftChar, 0f);
        leftChar.gameObject.SetActive(false);

        var rightSlot = CreateUiChild("RightSlot", root.transform);
        StretchRightHalf(rightSlot.GetComponent<RectTransform>());

        Image rightOverlay = CreateUiImage("RightOverlay", rightSlot.transform, uiSprite, preserveAspect: false);
        rightOverlay.raycastTarget = false;
        rightOverlay.color = new Color(0f, 0f, 0f, 0f);

        Image rightChar = CreateUiImage("RightCharImage", rightSlot.transform, uiSprite, preserveAspect: true);
        rightChar.raycastTarget = false;
        SetImageAlpha(rightChar, 0f);
        rightChar.gameObject.SetActive(false);

        // DialogueBox = SayDialog "Panel" (동일 RectTransform·배경)
        var dialogueBox = CreateUiChild("DialogueBox", root.transform);
        var dbRt = dialogueBox.GetComponent<RectTransform>();
        dbRt.anchorMin = new Vector2(0.5f, 0f);
        dbRt.anchorMax = new Vector2(0.5f, 0f);
        dbRt.pivot = new Vector2(0f, 0f);
        dbRt.anchoredPosition = new Vector2(-750f, 0f);
        dbRt.sizeDelta = new Vector2(SayDialogPanelWidth, SayDialogPanelHeight);

        var dbImg = dialogueBox.AddComponent<Image>();
        if (uiSprite != null)
        {
            dbImg.sprite = uiSprite;
            dbImg.type = Image.Type.Sliced;
            dbImg.preserveAspect = true;
        }

        dbImg.color = Color.white;

        // NameText — SayDialog NameText (좌상단 앵커, 1106×71)
        var nameTextGo = CreateUiChild("NameText", dialogueBox.transform);
        var nameRt = nameTextGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(0f, 1f);
        nameRt.pivot = new Vector2(0.5f, 0.5f);
        nameRt.anchoredPosition = new Vector2(586.5f, -38.37f);
        nameRt.sizeDelta = new Vector2(SayDialogNameTextWidth, SayDialogNameTextHeight);

        var nameText = nameTextGo.AddComponent<TextMeshProUGUI>();
        nameText.text = "Character Name";
        nameText.fontSize = 50f;
        nameText.alignment = TextAlignmentOptions.TopLeft;
        nameText.color = new Color(0.25882354f, 0.25490198f, 0.2627451f, 1f);
        if (fontAsset != null)
        {
            nameText.font = fontAsset;
        }

        // DialogueText — SayDialog StoryText (본문 영역)
        var dialogueTextGo = CreateUiChild("DialogueText", dialogueBox.transform);
        var dtRt = dialogueTextGo.GetComponent<RectTransform>();
        dtRt.anchorMin = new Vector2(0f, 0f);
        dtRt.anchorMax = new Vector2(1f, 0.78700006f);
        dtRt.pivot = new Vector2(0.5f, 0.5f);
        dtRt.anchoredPosition = new Vector2(3f, 14.13f);
        dtRt.sizeDelta = new Vector2(-61f, -63f);

        var dialogueText = dialogueTextGo.AddComponent<TextMeshProUGUI>();
        dialogueText.text = "";
        dialogueText.fontSize = 45f;
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.enableWordWrapping = true;
        dialogueText.color = Color.white;
        if (fontAsset != null)
        {
            dialogueText.font = fontAsset;
        }

        dialogueBox.SetActive(false);

        var mgr = root.GetComponent<StandingDialogueManager>();
        var so = new SerializedObject(mgr);
        so.FindProperty("leftCharImage").objectReferenceValue = leftChar;
        so.FindProperty("rightCharImage").objectReferenceValue = rightChar;
        so.FindProperty("characterDimBackdrop").objectReferenceValue = characterDimBackdrop;
        so.FindProperty("dialogueBox").objectReferenceValue = dialogueBox;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("dialogueTextField").objectReferenceValue = dialogueText;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.ApplyModifiedPropertiesWithoutUndo();

        string dir = Path.GetDirectoryName(PrefabPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string resourcesDir = Path.GetDirectoryName(ResourcesPrefabPath);
        if (!string.IsNullOrEmpty(resourcesDir))
        {
            Directory.CreateDirectory(resourcesDir);
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.SaveAsPrefabAsset(root, ResourcesPrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StandingDialogue] Prefabs saved: " + PrefabPath + " ; " + ResourcesPrefabPath);
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(PrefabPath));
    }

    static GameObject CreateUiChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image CreateUiImage(string name, Transform parent, Sprite sprite, bool preserveAspect)
    {
        var go = CreateUiChild(name, parent);
        StretchFull(go.GetComponent<RectTransform>());
        var image = go.AddComponent<Image>();
        if (sprite != null)
        {
            image.sprite = sprite;
        }

        image.preserveAspect = preserveAspect;
        image.type = Image.Type.Simple;
        return image;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchLeftHalf(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchRightHalf(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetImageAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    static Sprite LoadFirstSprite(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object o in assets)
        {
            if (o is Sprite s)
            {
                return s;
            }
        }

        return null;
    }
}
#endif
