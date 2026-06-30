using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class DeveloperModeItemGrantServiceTests
{
    static Item CreateItem(int id, string name, HideFlags hideFlags = HideFlags.None)
    {
        var item = ScriptableObject.CreateInstance<Item>();
        item.itemId = id;
        item.itemName = name;
        item.hideFlags = hideFlags;
        return item;
    }

    [Test]
    public void GrantAllItems_ReturnsBlocked_WhenDeveloperModeDisabled()
    {
        DeveloperModeItemGrantReport report = DeveloperModeItemGrantService.GrantAllItems();

        Assert.IsTrue(report.WasBlockedByDevMode);
        Assert.AreEqual(0, report.GrantedCount);
    }

    [Test]
    public void GrantSelectedItem_ReturnsBlocked_WhenDeveloperModeDisabled()
    {
        Item item = CreateItem(25, "DevPickTest");

        DeveloperModeItemSelectionGrantResult result =
            DeveloperModeItemGrantService.GrantSelectedItem(item, 1);

        Assert.IsTrue(result.WasBlockedByDevMode);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(0, result.GrantedQuantity);

        Object.DestroyImmediate(item);
    }

    [Test]
    public void GetCatalogEntries_ReturnsEmpty_WhenDeveloperModeDisabled()
    {
        Assert.AreEqual(0, DeveloperModeItemGrantService.GetCatalogEntries().Count);
    }

    [Test]
    public void ClampGrantQuantity_EnforcesMinimumOne()
    {
        Assert.AreEqual(1, DeveloperModeItemGrantService.ClampGrantQuantity(0));
        Assert.AreEqual(1, DeveloperModeItemGrantService.ClampGrantQuantity(-5));
    }

    [Test]
    public void IsGrantableItem_RejectsNullInvalidIdAndEmptyName()
    {
        Assert.IsFalse(DeveloperModeItemGrantService.IsGrantableItem(null, out _));

        Item outOfRange = CreateItem(0, "Bad");
        Assert.IsFalse(DeveloperModeItemGrantService.IsGrantableItem(outOfRange, out _));

        Item emptyName = CreateItem(1, " ");
        Assert.IsFalse(DeveloperModeItemGrantService.IsGrantableItem(emptyName, out _));

        Object.DestroyImmediate(outOfRange);
        Object.DestroyImmediate(emptyName);
    }

    [Test]
    public void IsGrantableItem_AcceptsValidItem()
    {
        Item bottle = CreateItem(1, "Bottle");

        Assert.IsTrue(DeveloperModeItemGrantService.IsGrantableItem(bottle, out string skipReason));
        Assert.IsNull(skipReason);

        Object.DestroyImmediate(bottle);
    }

    [Test]
    public void CollectGrantableItems_FiltersInvalidAndDuplicateIds()
    {
        var report = new DeveloperModeItemGrantReport();
        Item validA = CreateItem(1, "A");
        Item validB = CreateItem(2, "B");
        Item duplicateId = CreateItem(1, "Duplicate");
        Item invalid = CreateItem(99, "Invalid");

        List<Item> result = DeveloperModeItemGrantService.CollectGrantableItems(
            new[] { invalid, validB, duplicateId, validA, null },
            report);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result[0].itemId);
        Assert.AreEqual(2, result[1].itemId);
        Assert.AreEqual(3, report.SkippedInvalidCount);

        Object.DestroyImmediate(validA);
        Object.DestroyImmediate(validB);
        Object.DestroyImmediate(duplicateId);
        Object.DestroyImmediate(invalid);
    }

    [Test]
    public void Report_ToString_ShowsBlockedMessage_WhenDevModeOff()
    {
        var report = new DeveloperModeItemGrantReport { WasBlockedByDevMode = true };

        StringAssert.Contains("개발자 모드", report.ToString());
    }

    [Test]
    public void Report_ToString_ShowsCounts_WhenGrantRan()
    {
        var report = new DeveloperModeItemGrantReport
        {
            CandidateCount = 16,
            GrantedCount = 10,
            SkippedDuplicateCount = 4,
            SkippedInvalidCount = 1,
            FailedCount = 1
        };

        StringAssert.Contains("지급 10", report.ToString());
        StringAssert.Contains("후보 16", report.ToString());
        Assert.IsTrue(report.HasFailures);
    }

    [TearDown]
    public void TearDown()
    {
        ItemRegistry.ResetCacheForTest();
    }

    [Test]
    public void IsGrantableItem_AcceptsBookmarkMirror_FromProductionRegistry()
    {
        Item bookmarkMirror = ItemLookup.FindById(17);
        Assert.IsNotNull(bookmarkMirror);

        Assert.IsTrue(DeveloperModeItemGrantService.IsGrantableItem(bookmarkMirror, out string skipReason));
        Assert.IsNull(skipReason);
    }

    [Test]
    public void IsGrantableItem_StillAcceptsFilterCard()
    {
        Item filterCard = ItemLookup.FindById(4);
        Assert.IsNotNull(filterCard);

        Assert.IsTrue(DeveloperModeItemGrantService.IsGrantableItem(filterCard, out string skipReason));
        Assert.IsNull(skipReason);
    }

    [Test]
    public void CollectGrantableItems_IncludesBookmarkMirrorAndFilterCard()
    {
        Item bookmarkMirror = ItemLookup.FindById(17);
        Item filterCard = ItemLookup.FindById(4);
        Assert.IsNotNull(bookmarkMirror);
        Assert.IsNotNull(filterCard);

        var report = new DeveloperModeItemGrantReport();
        List<Item> result = DeveloperModeItemGrantService.CollectGrantableItems(
            new[] { bookmarkMirror, filterCard },
            report);

        Assert.AreEqual(2, result.Count);
        Assert.IsNotNull(result.FirstOrDefault(item => item.itemName == "BookmarkMirror"));
        Assert.IsNotNull(result.FirstOrDefault(item => item.itemName == "FilterCard"));
        Assert.AreEqual(0, report.SkippedInvalidCount);
    }
}
