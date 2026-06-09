using NUnit.Framework;
using UnityEngine;

public class AddItemToInventoryTests
{
    [TearDown]
    public void TearDown()
    {
        ItemRegistry.ResetCacheForTest();
    }

    [Test]
    public void FindItemById_ReturnsItem_WhenIdExists()
    {
        Item found = AddItemToInventory.FindItemById(1);

        Assert.IsNotNull(found);
        Assert.AreEqual("Bottle", found.itemName);
    }

    [Test]
    public void FindItemById_ReturnsNull_WhenIdDoesNotExist()
    {
        Item result = AddItemToInventory.FindItemById(999);

        Assert.IsNull(result);
    }

    [Test]
    public void FindItemById_ReturnsRegistryItem_ForDistinctIds()
    {
        Item holyGrail = AddItemToInventory.FindItemById(5);
        Item bedRoomKey = AddItemToInventory.FindItemById(10);

        Assert.IsNotNull(holyGrail);
        Assert.AreEqual("HolyGrail", holyGrail.itemName);
        Assert.IsNotNull(bedRoomKey);
        Assert.AreEqual("BedRoomKey", bedRoomKey.itemName);
    }

    [Test]
    public void GetSummary_ReturnsItemId()
    {
        var go = new GameObject("TestCmd");
        var cmd = go.AddComponent<AddItemToInventory>();

        string summary = cmd.GetSummary();

        Assert.IsTrue(summary.Contains("targetItemId"));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void GetButtonColor_ReturnsNonBlack()
    {
        var go = new GameObject("TestCmd");
        var cmd = go.AddComponent<AddItemToInventory>();

        Color c = cmd.GetButtonColor();

        Assert.AreNotEqual(Color.black, c);

        Object.DestroyImmediate(go);
    }
}
