using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DeveloperModeRuntimePolicyTests
{
    [TearDown]
    public void TearDown()
    {
        DeveloperModeController.ResetTestOverrides();
        CleanupDeveloperModeObjects();
    }

    [Test]
    public void CanUseDeveloperModeRuntime_InEditor_IsTrueByDefault()
    {
        Assert.IsTrue(DeveloperModeController.CanUseDeveloperModeRuntime);
    }

    [Test]
    public void CanUseDeveloperModeRuntime_WhenForcedFalse_ReturnsFalse()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = false;

        Assert.IsFalse(DeveloperModeController.CanUseDeveloperModeRuntime);
    }

    [Test]
    public void CanUseDeveloperModeRuntime_WhenForcedTrue_ReturnsTrue()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;

        Assert.IsTrue(DeveloperModeController.CanUseDeveloperModeRuntime);
    }

    [Test]
    public void ToggleDeveloperMode_WhenRuntimeAllowed_CreatesOverlayAndSetsVisible()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        var controllerObject = new GameObject("DeveloperModeControllerTest");
        var controller = controllerObject.AddComponent<DeveloperModeController>();

        controller.ToggleDeveloperMode();

        InGameDeveloperOverlay overlay =
            Object.FindFirstObjectByType<InGameDeveloperOverlay>(FindObjectsInactive.Include);

        Assert.IsNotNull(overlay);
        Assert.IsTrue(DeveloperModeController.IsDeveloperModeEnabled);
        Assert.IsTrue(overlay.IsVisible);
    }

    [Test]
    public void ToggleDeveloperMode_WhenRuntimeBlocked_DoesNotEnableDeveloperMode()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = false;
        var controllerObject = new GameObject("DeveloperModeControllerTest");
        var controller = controllerObject.AddComponent<DeveloperModeController>();

        controller.ToggleDeveloperMode();

        Assert.IsFalse(DeveloperModeController.IsDeveloperModeEnabled);
        Assert.IsNull(Object.FindFirstObjectByType<InGameDeveloperOverlay>(FindObjectsInactive.Include));
    }

    [Test]
    public void CanGrant_Blocked_WhenRuntimeNotAllowed_EvenIfDeveloperModeEnabled()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = false;
        DeveloperModeController.SetIsDeveloperModeEnabledForTests(true);

        Assert.IsFalse(DeveloperModeItemGrantService.CanGrant);
        Assert.AreEqual(0, DeveloperModeItemGrantService.GetCatalogEntries().Count);
    }

    [Test]
    public void Bootstrap_SkipsControllerCreation_WhenRuntimeNotAllowed()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = false;
        CleanupDeveloperModeObjects();

        InvokeBootstrapEnsureController();

        Assert.IsNull(Object.FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include));
    }

    [Test]
    public void Bootstrap_CreatesController_WhenRuntimeAllowed()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        CleanupDeveloperModeObjects();

        InvokeBootstrapEnsureController();

        Assert.IsNotNull(Object.FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include));
    }

    [Test]
    public void Bootstrap_UsesSameRuntimeGateAsController()
    {
        DeveloperModeController.RuntimeAvailabilityOverrideForTests = false;
        CleanupDeveloperModeObjects();

        Assert.IsFalse(DeveloperModeController.CanUseDeveloperModeRuntime);
        InvokeBootstrapEnsureController();
        Assert.IsNull(Object.FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include));

        DeveloperModeController.RuntimeAvailabilityOverrideForTests = true;
        Assert.IsTrue(DeveloperModeController.CanUseDeveloperModeRuntime);
        InvokeBootstrapEnsureController();
        Assert.IsNotNull(Object.FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include));
    }

    static void InvokeBootstrapEnsureController()
    {
        MethodInfo method = typeof(DeveloperModeBootstrap).GetMethod(
            "EnsureDeveloperModeController",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.IsNotNull(method, "DeveloperModeBootstrap.EnsureDeveloperModeController not found.");
        method.Invoke(null, null);
    }

    static void CleanupDeveloperModeObjects()
    {
        DeveloperModeController[] controllers = Object.FindObjectsByType<DeveloperModeController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (DeveloperModeController controller in controllers)
            Object.DestroyImmediate(controller.gameObject);

        InGameDeveloperOverlay[] overlays = Object.FindObjectsByType<InGameDeveloperOverlay>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (InGameDeveloperOverlay overlay in overlays)
            Object.DestroyImmediate(overlay.gameObject);
    }
}
