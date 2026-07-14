using NUnit.Framework;
using UnityEngine;
using Fungus;

public class InGameSettingsPanelCleanupPolicyTests
{
    private GameObject settingsPanelObject;
    private GameObject globalSettingsObject;
    private GameObject globalVariablesObject;
    private GameObject variableManagerObject;
    private GameObject disposableObject;

    [SetUp]
    public void SetUp()
    {
        settingsPanelObject = new GameObject("InGameSettingsPanel");

        globalSettingsObject = new GameObject("GlobalSettingManager");
        globalSettingsObject.SetActive(false);
        globalSettingsObject.AddComponent<GlobalSettingManager>();

        globalVariablesObject = new GameObject("GlobalVariables");
        globalVariablesObject.SetActive(false);
        globalVariablesObject.AddComponent<GlobalVariables>();

        variableManagerObject = new GameObject("Variablemanager");
        disposableObject = new GameObject("TemporaryRuntimeObject");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(settingsPanelObject);
        Object.DestroyImmediate(globalSettingsObject);
        Object.DestroyImmediate(globalVariablesObject);
        Object.DestroyImmediate(variableManagerObject);
        Object.DestroyImmediate(disposableObject);
    }

    [Test]
    public void CleanupPolicy_PreservesOnlySettingsPanelAndGlobalSettingManager()
    {
        Assert.IsTrue(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(settingsPanelObject, settingsPanelObject));
        Assert.IsTrue(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(globalSettingsObject, settingsPanelObject));
        Assert.IsFalse(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(globalVariablesObject, settingsPanelObject));
        Assert.IsFalse(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(variableManagerObject, settingsPanelObject));
    }

    [Test]
    public void CleanupPolicy_AllowsUnrelatedRuntimeObjectsToBeDestroyed()
    {
        Assert.IsFalse(InGameSettingsPanel.ShouldPreserveDontDestroyRoot(disposableObject, settingsPanelObject));
    }
}
