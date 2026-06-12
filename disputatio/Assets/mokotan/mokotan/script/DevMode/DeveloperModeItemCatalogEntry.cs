using UnityEngine;

/// <summary>개발자 모드 아이템 선택 목록에 표시할 항목.</summary>
public sealed class DeveloperModeItemCatalogEntry
{
    public Item Item { get; }
    public int ItemId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public DeveloperModeItemCategory Category { get; }

    public DeveloperModeItemCatalogEntry(
        Item item,
        int itemId,
        string displayName,
        string description,
        DeveloperModeItemCategory category)
    {
        Item = item;
        ItemId = itemId;
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        Category = category;
    }

    public static DeveloperModeItemCatalogEntry FromItem(Item item)
    {
        if (item == null)
            return null;

        string displayName = DeveloperModeItemCategoryResolver.GetItemDisplayName(item);
        string description = string.IsNullOrWhiteSpace(item.itemDescription)
            ? string.Empty
            : item.itemDescription.Trim();

        return new DeveloperModeItemCatalogEntry(
            item,
            item.itemId,
            displayName,
            description,
            DeveloperModeItemCategoryResolver.Resolve(item));
    }

    public string ListLabel => $"[{ItemId}] {DisplayName} ({DeveloperModeItemCategoryResolver.GetDisplayName(Category)})";

    public string ShortDescription
    {
        get
        {
            if (string.IsNullOrEmpty(Description))
                return "(설명 없음)";

            return Description.Length <= 48 ? Description : Description.Substring(0, 45) + "...";
        }
    }
}
