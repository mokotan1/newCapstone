using UnityEngine;
using UnityEngine.UI;

public class InventoryGuideController : MonoBehaviour
{
    private const string InventoryOpenedPrefsKey = "InventoryGuide.InventoryOpened";

    public static readonly Vector2 BottomRightAnchor = new Vector2(1f, 0f);

    [Header("Guide UI")]
    [SerializeField] private Button questionButton;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup questionButtonGroup;
    [SerializeField] private CanvasGroup popupGroup;

    [Header("Content")]
    [SerializeField] private string title = "인벤토리 안내";
    [TextArea]
    [SerializeField] private string body =
        "획득한 아이템은 인벤토리에 보관됩니다.\n" +
        "아이템 위에 커서를 올리면 상세 정보를 확인할 수 있습니다.\n" +
        "Tab 키로 인벤토리를 열고 닫을 수 있습니다.";
    [SerializeField] private bool showQuestionMark = true;

    private Transform guideParent;
    private bool lastQuestionVisible;

    private void Awake()
    {
        EnsureGuideUi();
        RefreshQuestionMark();
        HidePopup();
    }

    private void Update()
    {
        RefreshQuestionMark();
    }

    private void OnDestroy()
    {
        if (questionButton != null)
            questionButton.onClick.RemoveListener(ShowPopup);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePopup);
    }

    public void BindInventoryRoot(Transform root)
    {
        guideParent = ResolveGuideParent(root);
        EnsureGuideUi();
        ReparentGuideUi(guideParent);
        RefreshQuestionMark();
    }

    public void OnInventoryOpened()
    {
        PlayerPrefs.SetInt(InventoryOpenedPrefsKey, 1);
        PlayerPrefs.Save();
        HideGuide();
    }

    public void HideGuide()
    {
        if (questionButton != null)
            SetCanvasGroupVisible(questionButtonGroup, false, false);

        HidePopup();
        lastQuestionVisible = false;
    }

    public void ShowPopup()
    {
        EnsureGuideUi();

        if (popupRoot == null)
            return;

        if (titleText != null)
            titleText.text = title;

        if (bodyText != null)
            bodyText.text = body;

        EnsureActive(popupRoot);
        SetCanvasGroupVisible(popupGroup, true, true);
    }

    public void HidePopup()
    {
        if (popupRoot != null)
        {
            EnsureActive(popupRoot);
            SetCanvasGroupVisible(popupGroup, false, false);
        }
    }

    public static bool ShouldShowQuestionMark(bool enabled, bool isInventoryUnlocked, bool hasOpenedInventory)
    {
        return enabled && isInventoryUnlocked && !hasOpenedInventory;
    }

    private void RefreshQuestionMark()
    {
        if (questionButton == null)
            return;

        bool hasOpenedInventory = PlayerPrefs.GetInt(InventoryOpenedPrefsKey, 0) == 1;
        bool shouldShow = ShouldShowQuestionMark(showQuestionMark, InventoryAccessState.IsUnlocked, hasOpenedInventory);
        if (lastQuestionVisible == shouldShow && questionButton.gameObject.activeSelf)
            return;

        EnsureActive(questionButton.gameObject);
        SetCanvasGroupVisible(questionButtonGroup, shouldShow, shouldShow);
        if (!shouldShow)
            HidePopup();

        lastQuestionVisible = shouldShow;
    }

    private void EnsureGuideUi()
    {
        Font font = GetDefaultFont();
        Transform parent = guideParent != null ? guideParent : transform;

        if (questionButton == null)
            questionButton = CreateQuestionButton(parent, font);
        else
            questionButtonGroup = EnsureCanvasGroup(questionButton.gameObject);

        if (popupRoot == null)
            popupRoot = CreatePopup(parent, font);
        else
            popupGroup = EnsureCanvasGroup(popupRoot);
    }

    private void ReparentGuideUi(Transform parent)
    {
        if (parent == null)
            return;

        if (questionButton != null)
        {
            questionButton.transform.SetParent(parent, false);
            questionButton.transform.SetAsLastSibling();
        }

        if (popupRoot != null)
        {
            popupRoot.transform.SetParent(parent, false);
            popupRoot.transform.SetAsLastSibling();
        }
    }

    private Button CreateQuestionButton(Transform parent, Font font)
    {
        GameObject buttonObject = new GameObject("InventoryGuideQuestionButton");
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(parent, false);
        questionButtonGroup = EnsureCanvasGroup(buttonObject);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = BottomRightAnchor;
        rect.anchorMax = BottomRightAnchor;
        rect.pivot = BottomRightAnchor;
        rect.anchoredPosition = new Vector2(-32f, 32f);
        rect.sizeDelta = new Vector2(54f, 54f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.02f, 0.82f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(ShowPopup);

        Text label = CreateText("Label", buttonObject.transform, font, 32, TextAnchor.MiddleCenter);
        label.text = "?";
        label.raycastTarget = false;

        return button;
    }

    private GameObject CreatePopup(Transform parent, Font font)
    {
        GameObject panelObject = new GameObject("InventoryGuidePopup");
        panelObject.layer = gameObject.layer;
        panelObject.transform.SetParent(parent, false);
        popupGroup = EnsureCanvasGroup(panelObject);

        RectTransform rect = panelObject.AddComponent<RectTransform>();
        rect.anchorMin = BottomRightAnchor;
        rect.anchorMax = BottomRightAnchor;
        rect.pivot = BottomRightAnchor;
        rect.anchoredPosition = new Vector2(-32f, 98f);
        rect.sizeDelta = new Vector2(520f, 245f);

        Image background = panelObject.AddComponent<Image>();
        background.color = new Color(0.02f, 0.02f, 0.02f, 0.9f);

        titleText = CreateText("Title", panelObject.transform, font, 28, TextAnchor.UpperLeft);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-40f, 40f);

        bodyText = CreateText("Body", panelObject.transform, font, 23, TextAnchor.UpperLeft);
        RectTransform bodyRect = bodyText.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(20f, 58f);
        bodyRect.offsetMax = new Vector2(-20f, -70f);

        closeButton = CreateCloseButton(panelObject.transform, font);
        return panelObject;
    }

    private Button CreateCloseButton(Transform parent, Font font)
    {
        GameObject buttonObject = new GameObject("CloseButton");
        buttonObject.layer = gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(128f, 40f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(HidePopup);

        Text label = CreateText("Label", buttonObject.transform, font, 22, TextAnchor.MiddleCenter);
        label.text = "확인";
        label.raycastTarget = false;

        return button;
    }

    private Text CreateText(string name, Transform parent, Font font, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    internal static void SetCanvasGroupVisible(CanvasGroup group, bool visible, bool receivesInput)
    {
        if (group == null)
            return;

        group.alpha = visible ? 1f : 0f;
        group.interactable = receivesInput;
        group.blocksRaycasts = receivesInput;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        EnsureActive(target);
        if (!target.TryGetComponent(out CanvasGroup group))
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    private static void EnsureActive(GameObject target)
    {
        if (target != null && !target.activeSelf)
            target.SetActive(true);
    }

    internal static Transform ResolveGuideParentForTest(Transform inventoryRoot)
    {
        return ResolveGuideParent(inventoryRoot);
    }

    private static Transform ResolveGuideParent(Transform inventoryRoot)
    {
        if (inventoryRoot == null)
            return null;

        Canvas parentCanvas = inventoryRoot.GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
            return parentCanvas.transform;

        Transform parent = inventoryRoot.parent;
        return parent != null ? parent : inventoryRoot;
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            return font;

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
