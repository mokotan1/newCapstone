using NUnit.Framework;
using UnityEngine;

public class InventoryTooltipControllerPlacementTests
{
    [Test]
    public void CalculateAbovePointerPosition_PlacesTooltipAbovePointer()
    {
        Vector2 result = InventoryTooltipController.CalculateAbovePointerPosition(
            new Vector2(100f, 120f),
            new Vector2(360f, 140f),
            new Vector2(20f, 20f),
            new Vector2(1920f, 1080f));

        Assert.AreEqual(new Vector2(120f, 140f), result);
    }

    [Test]
    public void CalculateAbovePointerPosition_ClampsInsideScreen()
    {
        Vector2 result = InventoryTooltipController.CalculateAbovePointerPosition(
            new Vector2(1880f, 1040f),
            new Vector2(360f, 140f),
            new Vector2(20f, 20f),
            new Vector2(1920f, 1080f));

        Assert.AreEqual(new Vector2(1560f, 940f), result);
    }

    [Test]
    public void SetCanvasGroupVisible_HidesTooltipWithoutDeactivatingObject()
    {
        GameObject target = new GameObject("InventoryTooltip", typeof(CanvasGroup));

        try
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();

            InventoryTooltipController.SetCanvasGroupVisible(group, false);

            Assert.IsTrue(target.activeSelf);
            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }
}
