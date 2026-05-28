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

    [Test]
    public void SelectGuideController_ReturnsAssigned_WhenAssignedExists()
    {
        var assignedObject = new GameObject("assignedGuide");
        var discoveredObject = new GameObject("discoveredGuide");
        var assigned = assignedObject.AddComponent<InventoryGuideController>();
        var discovered = discoveredObject.AddComponent<InventoryGuideController>();

        InventoryGuideController result = InventoryManager.SelectGuideController(assigned, discovered);

        Assert.AreSame(assigned, result);

        Object.DestroyImmediate(assignedObject);
        Object.DestroyImmediate(discoveredObject);
    }

    [Test]
    public void SelectGuideController_ReturnsDiscovered_WhenAssignedMissing()
    {
        var discoveredObject = new GameObject("discoveredGuide");
        var discovered = discoveredObject.AddComponent<InventoryGuideController>();

        InventoryGuideController result = InventoryManager.SelectGuideController(null, discovered);

        Assert.AreSame(discovered, result);

        Object.DestroyImmediate(discoveredObject);
    }

    [Test]
    public void SelectGuideController_ReturnsNull_WhenBothMissing()
    {
        InventoryGuideController result = InventoryManager.SelectGuideController(null, null);

        Assert.IsNull(result);
    }

    [Test]
    public void NormalizeInventoryCanvasTransform_RestoresZeroScaledCanvas()
    {
        GameObject canvasObject = new GameObject("InventoryCanvas", typeof(RectTransform), typeof(Canvas));
        GameObject inventoryRootObject = new GameObject("InventoryRoot", typeof(RectTransform));

        try
        {
            inventoryRootObject.transform.SetParent(canvasObject.transform, false);
            canvasObject.transform.localScale = Vector3.zero;

            InventoryManager.NormalizeInventoryCanvasTransform(inventoryRootObject.transform);

            Assert.AreEqual(Vector3.one, canvasObject.transform.localScale);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void EnsureInventoryRootActive_ReactivatesInventoryWithoutRequiringTabUnlock()
    {
        GameObject inventoryRootObject = new GameObject("InventoryRoot");

        try
        {
            inventoryRootObject.SetActive(false);

            InventoryManager.EnsureInventoryRootActive(inventoryRootObject);

            Assert.IsTrue(inventoryRootObject.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(inventoryRootObject);
        }
    }
}
