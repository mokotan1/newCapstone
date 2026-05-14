using UnityEngine;
using UnityEngine.UI;

public class InventoryTooltipController : MonoBehaviour
{
    [Header("Tooltip UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private Text contentText;
    [SerializeField] private RectTransform rootRect;

    [Header("Placement")]
    [SerializeField] private Vector2 screenOffset = new Vector2(20f, -20f);

    private void Awake()
    {
        Hide();
    }

    public void Show(Item item, Vector2 pointerPosition)
    {
        if (item == null || root == null || contentText == null)
            return;

        contentText.text = ItemTooltipTextFormatter.Build(item.itemName, item.itemDescription, item.tooltipRows);
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

        rootRect.position = pointerPosition + screenOffset;
    }
}
