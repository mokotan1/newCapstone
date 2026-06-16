using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대사 로그 패널의 "대사" / "체셔" 탭. HTML Dialogue Log Tab Spec 레이아웃을 따른다.
/// </summary>
public class DialogueLogTabBar : MonoBehaviour
{
    const string TabBarObjectName = "DialogueLogTabBar";
    const string UnderlineChildName = "ActiveUnderline";

    [SerializeField] Button dialogueTabButton;
    [SerializeField] Button cheshireTabButton;
    [SerializeField] TMP_Text dialogueTabLabel;
    [SerializeField] TMP_Text cheshireTabLabel;
    [SerializeField] Image dialogueTabUnderline;
    [SerializeField] Image cheshireTabUnderline;
    [SerializeField] Image tabBarBorder;

    DialogueLogPanel panel;
    DialogueLogVisualStyle style = DialogueLogVisualStyle.ParchmentCodex;

    public void Bind(DialogueLogPanel owner, DialogueLogVisualStyle visualStyle)
    {
        panel = owner;
        style = visualStyle;

        if (dialogueTabButton != null)
        {
            dialogueTabButton.onClick.RemoveAllListeners();
            dialogueTabButton.onClick.AddListener(() => panel?.SelectContentTab(DialogueLogContentTab.Dialogue));
        }

        if (cheshireTabButton != null)
        {
            cheshireTabButton.onClick.RemoveAllListeners();
            cheshireTabButton.onClick.AddListener(() => panel?.SelectContentTab(DialogueLogContentTab.Cheshire));
        }
    }

    public void SetSelected(DialogueLogContentTab tab)
    {
        if (dialogueTabLabel != null)
            DialogueLogTypography.ApplyTab(dialogueTabLabel, style);
        if (cheshireTabLabel != null)
            DialogueLogTypography.ApplyTab(cheshireTabLabel, style);

        ApplyTabVisual(dialogueTabLabel, dialogueTabUnderline, tab == DialogueLogContentTab.Dialogue);
        ApplyTabVisual(cheshireTabLabel, cheshireTabUnderline, tab == DialogueLogContentTab.Cheshire);
    }

    static void ApplyTabVisual(TMP_Text label, Image underline, bool selected)
    {
        if (label != null)
        {
            label.fontStyle = FontStyles.Bold;
            label.color = selected ? DialogueLogTabSpec.TabActiveColor : DialogueLogTabSpec.TabInactiveColor;
        }

        if (underline != null)
            underline.gameObject.SetActive(selected);
    }

    /// <summary>
    /// 패널 루트 아래에 탭 바가 없으면 HTML 스펙대로 생성한다.
    /// </summary>
    public static DialogueLogTabBar EnsureUnder(
        Transform panelRoot,
        DialogueLogPanel owner,
        DialogueLogVisualStyle visualStyle)
    {
        if (panelRoot == null || owner == null)
            return null;

        Transform host = FindTabHost(panelRoot);
        if (host == null)
            host = panelRoot;

        Transform existing = host.Find(TabBarObjectName);
        if (existing != null)
        {
            var bound = existing.GetComponent<DialogueLogTabBar>();
            var existingRect = existing as RectTransform;
            bool dimensionsMatch = existingRect != null
                && Mathf.Approximately(existingRect.sizeDelta.x, DialogueLogTabSpec.TabBarWidth)
                && Mathf.Approximately(existingRect.sizeDelta.y, DialogueLogTabSpec.TabBarHeight);

            if (bound != null && bound.dialogueTabUnderline != null && dimensionsMatch)
            {
                bound.Bind(owner, visualStyle);
                return bound;
            }

            DestroyImmediateSafe(existing.gameObject);
        }

        var tabBarGo = new GameObject(
            TabBarObjectName,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(DialogueLogTabBar));
        tabBarGo.transform.SetParent(host, false);

        var rect = tabBarGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -DialogueLogTabSpec.TabBarAnchoredTop);
        rect.sizeDelta = new Vector2(DialogueLogTabSpec.TabBarWidth, DialogueLogTabSpec.TabBarHeight);

        var layout = tabBarGo.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 0f;
        layout.padding = new RectOffset(0, 0, 0, 0);

        var borderGo = new GameObject(
            UnderlineChildName + "_BarBorder",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        borderGo.transform.SetParent(tabBarGo.transform, false);
        var borderRect = borderGo.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0f, 0f);
        borderRect.anchorMax = new Vector2(1f, 0f);
        borderRect.pivot = new Vector2(0.5f, 0f);
        borderRect.anchoredPosition = Vector2.zero;
        borderRect.sizeDelta = new Vector2(0f, 1f);
        var borderImage = borderGo.GetComponent<Image>();
        borderImage.color = DialogueLogTabSpec.TabBarBorderColor;
        borderImage.raycastTarget = false;

        var tabBar = tabBarGo.GetComponent<DialogueLogTabBar>();
        tabBar.tabBarBorder = borderImage;
        tabBar.dialogueTabButton = CreateTabButton(tabBarGo.transform, "DialogueTab", "대사", visualStyle, out tabBar.dialogueTabLabel, out tabBar.dialogueTabUnderline);
        tabBar.cheshireTabButton = CreateTabButton(tabBarGo.transform, "CheshireTab", "체셔", visualStyle, out tabBar.cheshireTabLabel, out tabBar.cheshireTabUnderline);
        tabBar.Bind(owner, visualStyle);
        tabBar.SetSelected(DialogueLogContentTab.Dialogue);
        return tabBar;
    }

    static Transform FindTabHost(Transform panelRoot)
    {
        Transform parchment = panelRoot.Find("CodexFrame/ParchmentBackground");
        if (parchment != null)
            return parchment;

        Transform codex = panelRoot.Find("CodexFrame");
        if (codex != null)
            return codex;

        return panelRoot;
    }

    static Button CreateTabButton(
        Transform parent,
        string name,
        string labelText,
        DialogueLogVisualStyle visualStyle,
        out TMP_Text label,
        out Image underline)
    {
        var buttonGo = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonGo.transform.SetParent(parent, false);

        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(DialogueLogTabSpec.TabWidth, DialogueLogTabSpec.TabBarHeight);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var layoutElement = buttonGo.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = DialogueLogTabSpec.TabWidth;
        layoutElement.minWidth = DialogueLogTabSpec.TabWidth;
        layoutElement.minHeight = DialogueLogTabSpec.TabBarHeight;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(buttonGo.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = labelText;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = DialogueLogTabSpec.TabInactiveColor;
        label.raycastTarget = false;
        DialogueLogTypography.ApplyTab(label, visualStyle);

        var underlineGo = new GameObject(UnderlineChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        underlineGo.transform.SetParent(buttonGo.transform, false);

        var underlineRect = underlineGo.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0f, 0f);
        underlineRect.anchorMax = new Vector2(1f, 0f);
        underlineRect.pivot = new Vector2(0.5f, 0f);
        underlineRect.anchoredPosition = new Vector2(0f, 0f);
        underlineRect.sizeDelta = new Vector2(
            -DialogueLogTabSpec.TabUnderlineInset * 2f,
            DialogueLogTabSpec.TabUnderlineHeight);

        underline = underlineGo.GetComponent<Image>();
        underline.color = DialogueLogTabSpec.TabUnderlineColor;
        underline.raycastTarget = false;
        underline.gameObject.SetActive(false);

        var button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    static void DestroyImmediateSafe(GameObject go)
    {
        if (go == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Object.DestroyImmediate(go);
        else
#endif
            Object.Destroy(go);
    }
}
