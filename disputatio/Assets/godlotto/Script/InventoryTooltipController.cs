using UnityEngine;
using UnityEngine.UI;

public class InventoryTooltipController : MonoBehaviour
{
    private const string DefaultTooltipTableResourcePath = "Scenario/item_tooltip_table";

    [Header("Tooltip UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Text contentText;
    [SerializeField] private RectTransform rootRect;

    [Header("Tooltip Table")]
    [SerializeField] private TextAsset tooltipTableCsv;

    [Header("Placement")]
    [SerializeField] private Vector2 screenOffset = new Vector2(20f, 20f);

    private ItemTooltipTable tooltipTable;

    private void Awake()
    {
        EnsureTooltipUi();

        if (tooltipTableCsv == null)
            tooltipTableCsv = Resources.Load<TextAsset>(DefaultTooltipTableResourcePath);

        tooltipTable = ItemTooltipTable.FromCsv(tooltipTableCsv != null ? tooltipTableCsv.text : "");
        Hide();
    }

    public void Show(Item item, Vector2 pointerPosition)
    {
        if (item == null || root == null || contentText == null)
            return;

        if (tooltipTable == null)
            tooltipTable = ItemTooltipTable.FromCsv(tooltipTableCsv != null ? tooltipTableCsv.text : "");

        ItemTooltipContent tooltipContent = tooltipTable.GetContent(item.itemId, item.itemName, item.itemDescription);
        if (tooltipContent.rows.Count == 0 && item.tooltipRows != null)
            tooltipContent.rows.AddRange(item.tooltipRows);

        contentText.text = ItemTooltipTextFormatter.Build(
            tooltipContent.itemName,
            tooltipContent.itemDescription,
            tooltipContent.rows);
        root.SetActive(true);
        Place(pointerPosition);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void Place(Vector2 pointerPosition)
    {
        if (rootRect == null)
            return;

        rootRect.pivot = new Vector2(0f, 0f);
        rootRect.position = CalculateAbovePointerPosition(
            pointerPosition,
            GetTooltipScreenSize(rootRect),
            screenOffset,
            new Vector2(Screen.width, Screen.height));
    }

    public static Vector2 CalculateAbovePointerPosition(
        Vector2 pointerPosition,
        Vector2 tooltipSize,
        Vector2 offset,
        Vector2 screenSize)
    {
        Vector2 position = pointerPosition + offset;
        position.x = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, screenSize.x - tooltipSize.x));
        position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, screenSize.y - tooltipSize.y));
        return position;
    }

    private void EnsureTooltipUi()
    {
        if (root != null && contentText != null && rootRect != null)
            return;

        GameObject tooltipRoot = new GameObject("InventoryTooltip");
        tooltipRoot.layer = gameObject.layer;
        tooltipRoot.transform.SetParent(transform, false);

        rootRect = tooltipRoot.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(460f, 190f);
        rootRect.pivot = new Vector2(0f, 0f);

        var background = tooltipRoot.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.82f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("ContentText");
        textObject.layer = tooltipRoot.layer;
        textObject.transform.SetParent(tooltipRoot.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 10f);
        textRect.offsetMax = new Vector2(-12f, -10f);

        contentText = textObject.AddComponent<Text>();
        contentText.font = GetDefaultFont();
        contentText.fontSize = 26;
        contentText.color = Color.white;
        contentText.alignment = TextAnchor.UpperLeft;
        contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow = VerticalWrapMode.Overflow;
        contentText.raycastTarget = false;

        root = tooltipRoot;
    }

    private static Vector2 GetTooltipScreenSize(RectTransform rectTransform)
    {
        Vector2 size = rectTransform.rect.size;
        if (size.x <= 0f || size.y <= 0f)
            size = rectTransform.sizeDelta;

        Vector3 scale = rectTransform.lossyScale;
        return new Vector2(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y));
    }

    private static Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            return font;

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }
}
