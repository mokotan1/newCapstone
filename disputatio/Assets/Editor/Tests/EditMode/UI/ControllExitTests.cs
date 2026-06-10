using Fungus;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ControllExitTests
{
    GameObject root;
    ControllExit controller;
    Flowchart flowchart;
    GameObject panel;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("ControllExitTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        controller = root.AddComponent<ControllExit>();

        panel = new GameObject("MapPanel");
        panel.transform.SetParent(root.transform);
        panel.SetActive(true);

        controller.flowchart = flowchart;
        controller.penel = panel;

        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);
        AddBooleanVariable(flowchart, FungusVariableKeys.WindowClicked, true);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var runner in Object.FindObjectsByType<DeferredClickCleanup>(FindObjectsSortMode.None))
            Object.DestroyImmediate(runner.gameObject);

        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void WhenClicked_AlwaysResetsIsClickedToFalse_EvenWhenAlreadyTrue()
    {
        controller.whenClicked();

        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void WhenClicked_PreservesWindowClicked()
    {
        controller.whenClicked();

        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked));
    }

    [Test]
    public void WhenClicked_RepeatedCallsKeepIsClickedFalse()
    {
        controller.whenClicked();
        controller.whenClicked();

        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        BooleanVariable variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
