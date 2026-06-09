using NUnit.Framework;
using UnityEngine;

public class ElectricSwitchUiSyncTests
{
    [Test]
    public void ApplySwitchVisibility_ShowsOnGraphic_WhenElectricOnIsTrue()
    {
        GameObject onGraphic = CreateSwitchObject("on_switch");
        GameObject offGraphic = CreateSwitchObject("off_Switch");

        ElectricSwitchUiSync.ApplySwitchVisibility(true, onGraphic, offGraphic);

        Assert.IsTrue(onGraphic.activeSelf);
        Assert.IsFalse(offGraphic.activeSelf);
    }

    [Test]
    public void ApplySwitchVisibility_ShowsOffGraphic_WhenElectricOnIsFalse()
    {
        GameObject onGraphic = CreateSwitchObject("on_switch");
        GameObject offGraphic = CreateSwitchObject("off_Switch");

        ElectricSwitchUiSync.ApplySwitchVisibility(false, onGraphic, offGraphic);

        Assert.IsFalse(onGraphic.activeSelf);
        Assert.IsTrue(offGraphic.activeSelf);
    }

    [Test]
    public void ApplySwitchVisibility_TogglesFromOnToOff_WhenStateChanges()
    {
        GameObject onGraphic = CreateSwitchObject("on_switch");
        GameObject offGraphic = CreateSwitchObject("off_Switch");

        ElectricSwitchUiSync.ApplySwitchVisibility(true, onGraphic, offGraphic);
        ElectricSwitchUiSync.ApplySwitchVisibility(false, onGraphic, offGraphic);

        Assert.IsFalse(onGraphic.activeSelf);
        Assert.IsTrue(offGraphic.activeSelf);
    }

    private static GameObject CreateSwitchObject(string objectName)
    {
        return new GameObject(objectName);
    }
}
