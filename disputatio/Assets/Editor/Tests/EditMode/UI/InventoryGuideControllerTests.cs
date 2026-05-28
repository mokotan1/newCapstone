using NUnit.Framework;
using UnityEngine;

public class InventoryGuideControllerTests
{
    [Test]
    public void ShouldShowQuestionMark_ReturnsTrue_WhenUnlockedAndInventoryNeverOpened()
    {
        Assert.IsTrue(InventoryGuideController.ShouldShowQuestionMark(true, true, false));
    }

    [Test]
    public void ShouldShowQuestionMark_ReturnsFalse_WhenInventoryWasOpened()
    {
        Assert.IsFalse(InventoryGuideController.ShouldShowQuestionMark(true, true, true));
    }

    [Test]
    public void ShouldShowQuestionMark_ReturnsFalse_WhenInventoryLocked()
    {
        Assert.IsFalse(InventoryGuideController.ShouldShowQuestionMark(true, false, false));
    }

    [Test]
    public void ShouldShowQuestionMark_ReturnsFalse_WhenDisabled()
    {
        Assert.IsFalse(InventoryGuideController.ShouldShowQuestionMark(false, true, false));
    }

    [Test]
    public void BottomRightAnchor_IsFixedToLowerRightCorner()
    {
        Assert.AreEqual(new UnityEngine.Vector2(1f, 0f), InventoryGuideController.BottomRightAnchor);
    }

    [Test]
    public void BindInventoryRoot_ReparentsExistingGuideUi_ToInventoryRootParent()
    {
        GameObject controllerObject = new GameObject("GuideController");
        GameObject canvasObject = new GameObject("CanvasRoot", typeof(RectTransform));
        GameObject inventoryRootObject = new GameObject("InventoryRoot", typeof(RectTransform));

        try
        {
            inventoryRootObject.transform.SetParent(canvasObject.transform, false);
            var controller = controllerObject.AddComponent<InventoryGuideController>();

            controller.BindInventoryRoot(inventoryRootObject.transform);

            Transform questionButton = FindTransformIncludingInactive("InventoryGuideQuestionButton");
            Transform popupRoot = FindTransformIncludingInactive("InventoryGuidePopup");
            Assert.AreSame(canvasObject.transform, questionButton.transform.parent);
            Assert.AreSame(canvasObject.transform, popupRoot.transform.parent);
        }
        finally
        {
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void ResolveGuideParent_ReturnsCanvasAncestor_WhenInventoryRootIsNested()
    {
        GameObject canvasObject = new GameObject("CanvasRoot", typeof(RectTransform), typeof(Canvas));
        GameObject panelObject = new GameObject("Panel", typeof(RectTransform));
        GameObject inventoryRootObject = new GameObject("InventoryRoot", typeof(RectTransform));

        try
        {
            panelObject.transform.SetParent(canvasObject.transform, false);
            inventoryRootObject.transform.SetParent(panelObject.transform, false);

            Transform result = InventoryGuideController.ResolveGuideParentForTest(inventoryRootObject.transform);

            Assert.AreSame(canvasObject.transform, result);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }

    [Test]
    public void SetCanvasGroupVisible_HidesWithoutDeactivatingObject()
    {
        GameObject target = new GameObject("GuideUi", typeof(CanvasGroup));

        try
        {
            CanvasGroup group = target.GetComponent<CanvasGroup>();

            InventoryGuideController.SetCanvasGroupVisible(group, false, false);

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

    private static Transform FindTransformIncludingInactive(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in transforms)
        {
            if (item.name == objectName)
                return item;
        }

        Assert.Fail($"Expected to find {objectName}.");
        return null;
    }
}
