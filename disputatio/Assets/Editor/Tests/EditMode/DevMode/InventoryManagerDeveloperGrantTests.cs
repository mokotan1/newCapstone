using NUnit.Framework;
using UnityEngine;

public class InventoryManagerDeveloperGrantTests
{
    [Test]
    public void TryAddItemForDeveloperMode_ReturnsFalse_WhenDeveloperModeDisabled()
    {
        var managerObject = new GameObject("InventoryManagerDevGrantTest");
        var manager = managerObject.AddComponent<InventoryManager>();
        Item item = ScriptableObject.CreateInstance<Item>();
        item.itemId = 20;
        item.itemName = "DevOnlyTestItem";

        bool added = manager.TryAddItemForDeveloperMode(item);

        Assert.IsFalse(added);
        Assert.AreEqual(0, manager.Items.Count);

        Object.DestroyImmediate(item);
        Object.DestroyImmediate(managerObject);
    }
}
