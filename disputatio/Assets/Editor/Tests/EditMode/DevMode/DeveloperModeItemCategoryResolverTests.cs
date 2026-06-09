using NUnit.Framework;

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
    }
}
