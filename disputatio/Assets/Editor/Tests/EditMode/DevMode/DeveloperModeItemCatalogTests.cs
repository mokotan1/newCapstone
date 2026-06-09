using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DeveloperModeItemCatalogTests
{
    static DeveloperModeItemCatalogEntry Entry(int id, string name, DeveloperModeItemCategory category)
    {
        var item = ScriptableObject.CreateInstance<Item>();
        item.itemId = id;
        item.itemName = name;
        return new DeveloperModeItemCatalogEntry(item, id, name, $"{name} desc", category);
    }

    [Test]
    public void Filter_ByCategory_ReturnsMatchingEntries()
    {
        var entries = new List<DeveloperModeItemCatalogEntry>
        {
            Entry(1, "Bottle", DeveloperModeItemCategory.Consumable),
            Entry(2, "StudyRoomKey", DeveloperModeItemCategory.Key),
        };

        List<DeveloperModeItemCatalogEntry> keys = DeveloperModeItemCatalog.Filter(
            entries,
            string.Empty,
            DeveloperModeItemCategory.Key);

        Assert.AreEqual(1, keys.Count);
        Assert.AreEqual("StudyRoomKey", keys[0].DisplayName);

        foreach (DeveloperModeItemCatalogEntry entry in entries)
            Object.DestroyImmediate(entry.Item);
    }

    [Test]
    public void Filter_BySearch_MatchesIdNameAndDescription()
    {
        var entries = new List<DeveloperModeItemCatalogEntry>
        {
            Entry(9, "StudyRoomKey", DeveloperModeItemCategory.Key),
            Entry(2, "Food", DeveloperModeItemCategory.Consumable),
        };

        Assert.AreEqual(1, DeveloperModeItemCatalog.Filter(entries, "9", DeveloperModeItemCategory.All).Count);
        Assert.AreEqual(1, DeveloperModeItemCatalog.Filter(entries, "food", DeveloperModeItemCategory.All).Count);
        Assert.AreEqual(1, DeveloperModeItemCatalog.Filter(entries, "StudyRoomKey desc", DeveloperModeItemCategory.All).Count);

        foreach (DeveloperModeItemCatalogEntry entry in entries)
            Object.DestroyImmediate(entry.Item);
    }

    [Test]
    public void MatchesSearch_ReturnsFalse_ForNullEntry()
    {
        Assert.IsFalse(DeveloperModeItemCatalog.MatchesSearch(null, "test"));
    }
}
