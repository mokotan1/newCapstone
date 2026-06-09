using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class ItemLookupTests
{
    [TearDown]
    public void TearDown()
    {
        ItemRegistry.ResetCacheForTest();
    }

    [Test]
    public void IsProductionFallbackCandidate_RejectsTestPrefixedAssets()
    {
        Item testItem = ScriptableObject.CreateInstance<Item>();
        testItem.name = "Test_Key";
        testItem.itemId = 17;
        testItem.itemName = "Test";

        Assert.IsFalse(ItemLookup.IsProductionFallbackCandidate(testItem));

        Object.DestroyImmediate(testItem);
    }

    [Test]
    public void FindById_ReturnsNull_WhenIdMissing()
    {
        Assert.IsNull(ItemLookup.FindById(999));
    }

    [Test]
    public void FindById_ReturnsIllustratedBible_FromRegistry()
    {
        Item bible = ItemLookup.FindById(19);

        Assert.IsNotNull(bible);
        Assert.AreEqual("IllustratedBible", bible.itemName);
    }

    [Test]
    public void GetAllItems_IncludesProductionItems_ExcludesTestPrefixedAssets()
    {
        IReadOnlyList<Item> items = ItemLookup.GetAllItems();

        Assert.GreaterOrEqual(items.Count, 17);
        Assert.IsNotNull(ItemLookup.FindById(1));
        Assert.IsNull(items.FirstOrDefault(i => i != null && i.name.StartsWith("Test_")));
    }

    [Test]
    public void CollectFallbackItems_FiltersInvalidIds()
    {
        Item invalid = ScriptableObject.CreateInstance<Item>();
        invalid.itemId = 0;
        invalid.itemName = "Bad";

        var report = new DeveloperModeItemGrantReport();
        var grantable = DeveloperModeItemGrantService.CollectGrantableItems(new[] { invalid, null }, report);

        Assert.AreEqual(0, grantable.Count);
        Assert.AreEqual(2, report.SkippedInvalidCount);

        Object.DestroyImmediate(invalid);
    }
}
