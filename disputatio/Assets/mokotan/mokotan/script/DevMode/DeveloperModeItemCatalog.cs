using System;
using System.Collections.Generic;

/// <summary>개발자 모드 아이템 목록 구성·검색·필터.</summary>
public static class DeveloperModeItemCatalog
{
    public static List<DeveloperModeItemCatalogEntry> BuildGrantableEntries(IEnumerable<Item> source)
    {
        var scanReport = new DeveloperModeItemGrantReport();
        List<Item> grantable = DeveloperModeItemGrantService.CollectGrantableItems(source, scanReport);
        var entries = new List<DeveloperModeItemCatalogEntry>(grantable.Count);

        foreach (Item item in grantable)
        {
            DeveloperModeItemCatalogEntry entry = DeveloperModeItemCatalogEntry.FromItem(item);
            if (entry != null)
                entries.Add(entry);
        }

        return entries;
    }

    public static List<DeveloperModeItemCatalogEntry> Filter(
        IReadOnlyList<DeveloperModeItemCatalogEntry> entries,
        string searchQuery,
        DeveloperModeItemCategory categoryFilter)
    {
        var filtered = new List<DeveloperModeItemCatalogEntry>();
        if (entries == null)
            return filtered;

        string query = searchQuery?.Trim() ?? string.Empty;

        for (int i = 0; i < entries.Count; i++)
        {
            DeveloperModeItemCatalogEntry entry = entries[i];
            if (entry == null)
                continue;

            if (categoryFilter != DeveloperModeItemCategory.All && entry.Category != categoryFilter)
                continue;

            if (!MatchesSearch(entry, query))
                continue;

            filtered.Add(entry);
        }

        return filtered;
    }

    internal static bool MatchesSearch(DeveloperModeItemCatalogEntry entry, string query)
    {
        if (entry == null)
            return false;

        if (string.IsNullOrEmpty(query))
            return true;

        if (entry.ItemId.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (entry.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        string categoryLabel = DeveloperModeItemCategoryResolver.GetDisplayName(entry.Category);
        if (categoryLabel.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        if (!string.IsNullOrEmpty(entry.Description)
            && entry.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
