using Fungus;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class BibleCommentaryPanelHintButton : MonoBehaviour
{
    private const int BibleCommentaryItemId = 21;
    private Button memoButton;
    private GameObject popup;

    private void Awake()
    {
        EnsureButton();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        EnsureButton();
        RefreshVisibility();

    }

    private void OnDisable()
    {
        if (popup != null)
            popup.SetActive(false);
    }

    public void RefreshVisibility()
    {
        EnsureButton();

        bool visible = HasBibleCommentary();
        memoButton.gameObject.SetActive(visible);

        if (!visible && popup != null)
            popup.SetActive(false);
    }

    private void EnsureButton()
    {
        if (memoButton != null)
            return;

        Transform existing = transform.Find("BibleMemoHintButton");
        GameObject buttonObject = existing != null ? existing.gameObject : CreateButtonObject();

        memoButton = buttonObject.GetComponent<Button>();
        if (memoButton == null)
            memoButton = buttonObject.AddComponent<Button>();

        memoButton.onClick.RemoveListener(TogglePopup);
        memoButton.onClick.AddListener(TogglePopup);

        var targetGraphic = buttonObject.GetComponent<Image>();
        if (targetGraphic != null)
        {
            targetGraphic.raycastTarget = true;
            memoButton.targetGraphic = targetGraphic;
        }
    }

    private GameObject CreateButtonObject()
    {
        var buttonObject = new GameObject(
            "BibleMemoHintButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(transform, false);
        buttonObject.transform.SetAsLastSibling();

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(18f, -18f);
        rect.sizeDelta = new Vector2(128f, 128f);

        var hitArea = buttonObject.GetComponent<Image>();
        hitArea.color = new Color(1f, 1f, 1f, 0.01f);
        hitArea.raycastTarget = true;

        CreateFoldVisual(buttonObject.transform);
        CreateGlyph(buttonObject.transform);

        return buttonObject;
    }

    private static void CreateFoldVisual(Transform parent)
    {
        var visualObject = new GameObject("FoldVisual", typeof(RectTransform), typeof(BibleMemoCornerFoldGraphic));
        visualObject.transform.SetParent(parent, false);

        var rect = visualObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var graphic = visualObject.GetComponent<BibleMemoCornerFoldGraphic>();
        graphic.raycastTarget = false;
    }

    private static void CreateGlyph(Transform parent)
    {
        var glyphObject = new GameObject("Glyph", typeof(RectTransform), typeof(TextMeshProUGUI));
        glyphObject.transform.SetParent(parent, false);

        var rect = glyphObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 42f);
        rect.offsetMax = new Vector2(-44f, -10f);

        var glyph = glyphObject.GetComponent<TextMeshProUGUI>();
        glyph.raycastTarget = false;
        glyph.text = "?";
        glyph.fontSize = 34f;
        glyph.fontStyle = FontStyles.Bold;
        glyph.alignment = TextAlignmentOptions.Center;
        glyph.color = new Color(0.98f, 0.89f, 0.64f, 1f);
    }

    private void TogglePopup()
    {
        if (!HasBibleCommentary())
            return;

        EnsurePopup();
        popup.SetActive(!popup.activeSelf);
    }

    private void EnsurePopup()
    {
        if (popup != null)
            return;

        popup = new GameObject("BibleMemoHintPopup", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        popup.transform.SetParent(transform, false);

        var rect = popup.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(154f, -24f);
        rect.sizeDelta = new Vector2(320f, 132f);

        var image = popup.GetComponent<Image>();
        image.color = new Color(0.16f, 0.10f, 0.06f, 0.96f);

        var group = popup.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        CreatePopupText(popup.transform);
        CreateCloseButton(popup.transform);
        popup.transform.SetAsLastSibling();
        popup.SetActive(false);
    }

    private static void CreatePopupText(Transform parent)
    {
        var textObject = new GameObject("MemoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16f, 12f);
        rect.offsetMax = new Vector2(-40f, -12f);

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.text = "성경 메모\n2015년 부활절 = 04 / 05\n월과 일을 이어 적는다.";
        text.fontSize = 18f;
        text.lineSpacing = 6f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = new Color(0.91f, 0.78f, 0.52f, 1f);
    }

    private void CreateCloseButton(Transform parent)
    {
        var closeObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObject.transform.SetParent(parent, false);

        var rect = closeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-8f, -8f);
        rect.sizeDelta = new Vector2(28f, 28f);

        var image = closeObject.GetComponent<Image>();
        image.color = new Color(0.36f, 0.23f, 0.13f, 1f);

        var button = closeObject.GetComponent<Button>();
        button.onClick.AddListener(() => popup.SetActive(false));

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(closeObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.text = "X";
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.84f, 0.64f, 1f);
    }

    private static bool HasBibleCommentary()
    {
        Flowchart flowchart = FlowchartLocator.Find();
        if (flowchart != null)
        {
            if (ItemAcquisitionTracker.IsAcquired(flowchart, BibleCommentaryItemId))
                return true;

            if (flowchart.GetBooleanVariable(FungusVariableKeys.GetBibleCommentary))
                return true;
        }

        return FlowchartLocator.GetFungusGlobalBoolean(FungusVariableKeys.GetBibleCommentary);
    }
}

public sealed class BibleMemoCornerFoldGraphic : Graphic
{
    private static readonly Color FoldColor = new Color(0.56f, 0.35f, 0.17f, 0.94f);
    private static readonly Color EdgeColor = new Color(0.95f, 0.78f, 0.42f, 1f);
    private static readonly Color ShadowColor = new Color(0.08f, 0.04f, 0.02f, 0.82f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        Vector2 topLeft = new Vector2(r.xMin, r.yMax);
        Vector2 topRight = new Vector2(r.xMax, r.yMax);
        Vector2 bottomLeft = new Vector2(r.xMin, r.yMin);

        AddTriangle(vh, topLeft, topRight, bottomLeft, ShadowColor, new Vector2(4f, -4f));
        AddTriangle(vh, topLeft, topRight, bottomLeft, FoldColor, Vector2.zero);
        AddLine(vh, topLeft + new Vector2(9f, -7f), topRight + new Vector2(-9f, -7f), 3f, EdgeColor);
        AddLine(vh, topLeft + new Vector2(7f, -9f), bottomLeft + new Vector2(7f, 9f), 3f, EdgeColor);
        AddLine(vh, topLeft + new Vector2(18f, -42f), topLeft + new Vector2(42f, -18f), 3f, EdgeColor);
    }

    private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color, Vector2 offset)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a + offset, color, Vector2.zero);
        vh.AddVert(b + offset, color, Vector2.zero);
        vh.AddVert(c + offset, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);

        int index = vh.currentVertCount;
        vh.AddVert(start - normal, color, Vector2.zero);
        vh.AddVert(start + normal, color, Vector2.zero);
        vh.AddVert(end + normal, color, Vector2.zero);
        vh.AddVert(end - normal, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}

public sealed class BibleCommentaryPanelHintRuntime : MonoBehaviour
{
    private static BibleCommentaryPanelHintRuntime instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject("BibleCommentaryPanelHintRuntime");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<BibleCommentaryPanelHintRuntime>();
        instance.BindCurrentScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindScene(scene);
    }

    public static void RefreshAll()
    {
        var buttons = Resources.FindObjectsOfTypeAll<BibleCommentaryPanelHintButton>();
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].RefreshVisibility();
        }
    }

    private void BindCurrentScene()
    {
        BindScene(SceneManager.GetActiveScene());
    }

    private static void BindScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (scene.name == "WifeRoom")
            AttachToPanel(scene, "DrawerPanel");
        else if (scene.name == "BedRoom")
            AttachToPanel(scene, "SafePanel");
    }

    private static void AttachToPanel(Scene scene, string panelName)
    {
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.name != panelName || t.gameObject.scene != scene)
                continue;

            if (t.GetComponent<BibleCommentaryPanelHintButton>() == null)
                t.gameObject.AddComponent<BibleCommentaryPanelHintButton>();
        }
    }
}
