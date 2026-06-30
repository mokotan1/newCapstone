using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class DeveloperModeItemCategoryResolverTests
{
    [Test]
    public void Resolve_ClassifiesKnownItemNames()
    {
        Assert.AreEqual(DeveloperModeItemCategory.Key, DeveloperModeItemCategoryResolver.Resolve("StudyRoomKey"));
        Assert.AreEqual(DeveloperModeItemCategory.Consumable, DeveloperModeItemCategoryResolver.Resolve("Bottle"));
        Assert.AreEqual(DeveloperModeItemCategory.Material, DeveloperModeItemCategoryResolver.Resolve("Wood"));
        Assert.AreEqual(DeveloperModeItemCategory.Seal, DeveloperModeItemCategoryResolver.Resolve("5th seal"));
        Assert.AreEqual(DeveloperModeItemCategory.Quest, DeveloperModeItemCategoryResolver.Resolve("HolyGrail"));
        Assert.AreEqual(DeveloperModeItemCategory.Quest, DeveloperModeItemCategoryResolver.Resolve("IllustratedBible"));
        Assert.AreEqual(DeveloperModeItemCategory.Quest, DeveloperModeItemCategoryResolver.Resolve("FilterCard"));
        Assert.AreEqual(DeveloperModeItemCategory.Quest, DeveloperModeItemCategoryResolver.Resolve("BookmarkMirror"));
    }

    [Test]
    public void GetItemDisplayName_ReturnsKoreanLabel_ForBookmarkMirror()
    {
        var item = ScriptableObject.CreateInstance<Item>();
        item.itemName = "BookmarkMirror";

        Assert.AreEqual("책갈피 거울", DeveloperModeItemCategoryResolver.GetItemDisplayName(item));

        Object.DestroyImmediate(item);
    }
}
