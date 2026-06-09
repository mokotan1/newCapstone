using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="ItemRegistry"/> 우선, 없으면 <c>godlotto/Item</c> 스캔 폴백으로 아이템을 조회합니다.
/// </summary>
public static class ItemLookup
{
    public const string ProductionItemFolder = "Assets/godlotto/Item";

    public static IReadOnlyList<Item> GetAllItems()
    {
        ItemRegistry registry = ItemRegistry.GetOrCreate();
        if (registry != null && registry.Count > 0)
            return registry.Items;

        return CollectFallbackItems();
    }

    public static Item FindById(int itemId)
    {
        ItemRegistry registry = ItemRegistry.GetOrCreate();
        if (registry != null && registry.Count > 0)
        {
            Item registered = registry.FindById(itemId);
            if (registered != null)
                return registered;
        }

        List<Item> scanned = CollectFallbackItems();
        for (int i = 0; i < scanned.Count; i++)
        {
            Item item = scanned[i];
            if (item != null && item.itemId == itemId)
                return item;
        }

        return null;
    }

    internal static List<Item> CollectFallbackItems()
    {
        var grantable = new List<Item>();
        var seenIds = new HashSet<int>();
        Item[] allItems = Resources.FindObjectsOfTypeAll<Item>();

        for (int i = 0; i < allItems.Length; i++)
        {
            Item item = allItems[i];
            if (!IsProductionFallbackCandidate(item))
                continue;

            if (!ItemGrantRules.IsValidInventoryItem(item, out _))
                continue;

            if (!seenIds.Add(item.itemId))
                continue;

            grantable.Add(item);
        }

        grantable.Sort((a, b) => a.itemId.CompareTo(b.itemId));
        return grantable;
    }

    internal static bool IsProductionFallbackCandidate(Item item)
    {
        if (item == null)
            return false;

        if ((item.hideFlags & HideFlags.HideAndDontSave) != 0)
            return false;

        string assetName = item.name;
        if (assetName.StartsWith("Test_"))
            return false;

        return true;
    }
}
