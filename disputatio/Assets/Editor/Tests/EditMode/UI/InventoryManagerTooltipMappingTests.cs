using NUnit.Framework;
using UnityEngine;

public class InventoryManagerTooltipMappingTests
{
    [Test]
    public void SelectTooltipController_ReturnsAssigned_WhenAssignedExists()
    {
        var assignedObject = new GameObject("assigned");
        var discoveredObject = new GameObject("discovered");
        var assigned = assignedObject.AddComponent<InventoryTooltipController>();
        var discovered = discoveredObject.AddComponent<InventoryTooltipController>();

        InventoryTooltipController result = InventoryManager.SelectTooltipController(assigned, discovered);

        Assert.AreSame(assigned, result);

        Object.DestroyImmediate(assignedObject);
        Object.DestroyImmediate(discoveredObject);
    }

    [Test]
    public void SelectTooltipController_ReturnsDiscovered_WhenAssignedMissing()
    {
        var discoveredObject = new GameObject("discovered");
        var discovered = discoveredObject.AddComponent<InventoryTooltipController>();

        InventoryTooltipController result = InventoryManager.SelectTooltipController(null, discovered);

        Assert.AreSame(discovered, result);

        Object.DestroyImmediate(discoveredObject);
    }

    [Test]
    public void SelectTooltipController_ReturnsNull_WhenBothMissing()
    {
        InventoryTooltipController result = InventoryManager.SelectTooltipController(null, null);

        Assert.IsNull(result);
    }
}
