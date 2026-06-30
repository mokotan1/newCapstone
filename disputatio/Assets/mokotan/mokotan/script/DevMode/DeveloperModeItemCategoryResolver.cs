using System;

/// <summary><see cref="Item.itemName"/> 기반 카테고리 추론 (런타임 Item SO에 카테고리 필드가 없음).</summary>
public static class DeveloperModeItemCategoryResolver
{
    public static DeveloperModeItemCategory Resolve(Item item)
    {
        if (item == null)
            return DeveloperModeItemCategory.Other;

        return Resolve(item.itemName);
    }

    public static DeveloperModeItemCategory Resolve(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return DeveloperModeItemCategory.Other;

        string normalized = itemName.Trim();

        if (normalized.IndexOf("seal", StringComparison.OrdinalIgnoreCase) >= 0)
            return DeveloperModeItemCategory.Seal;

        if (IsKeyItem(normalized))
            return DeveloperModeItemCategory.Key;

        if (string.Equals(normalized, "Food", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Bottle", StringComparison.OrdinalIgnoreCase))
            return DeveloperModeItemCategory.Consumable;

        if (string.Equals(normalized, "Wood", StringComparison.OrdinalIgnoreCase))
            return DeveloperModeItemCategory.Material;

        if (string.Equals(normalized, "HolyGrail", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "FilterCard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "BookmarkMirror", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "IllustratedBible", StringComparison.OrdinalIgnoreCase))
            return DeveloperModeItemCategory.Quest;

        return DeveloperModeItemCategory.Other;
    }

    public static string GetItemDisplayName(Item item)
    {
        if (item == null)
            return string.Empty;

        string normalized = string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName.Trim();

        if (string.Equals(normalized, "BookmarkMirror", StringComparison.OrdinalIgnoreCase))
            return "책갈피 거울";

        return normalized;
    }

    public static string GetDisplayName(DeveloperModeItemCategory category)
    {
        switch (category)
        {
            case DeveloperModeItemCategory.All: return "전체";
            case DeveloperModeItemCategory.Key: return "열쇠";
            case DeveloperModeItemCategory.Consumable: return "소비";
            case DeveloperModeItemCategory.Material: return "재료";
            case DeveloperModeItemCategory.Seal: return "인장";
            case DeveloperModeItemCategory.Quest: return "퀘스트";
            default: return "기타";
        }
    }

    static bool IsKeyItem(string itemName)
    {
        return itemName.EndsWith("Key", StringComparison.OrdinalIgnoreCase)
               || itemName.IndexOf("_Key", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
