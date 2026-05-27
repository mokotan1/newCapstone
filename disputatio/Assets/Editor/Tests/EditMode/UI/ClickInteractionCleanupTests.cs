using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClickInteractionCleanupTests
{
    private GameObject eventSystemObject;
    private GameObject selectedObject;
    private GameObject firstFlowchartObject;
    private GameObject secondFlowchartObject;

    [SetUp]
    public void SetUp()
    {
        eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();

        selectedObject = new GameObject("SelectedUi");
        EventSystem.current.SetSelectedGameObject(selectedObject);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(selectedObject);
        Object.DestroyImmediate(eventSystemObject);
        Object.DestroyImmediate(firstFlowchartObject);
        Object.DestroyImmediate(secondFlowchartObject);
    }

    [Test]
    public void ResetAfterUiBoundary_ClearsCurrentEventSystemSelection()
    {
        ClickInteractionCleanup.ResetAfterUiBoundary();

        Assert.IsNull(EventSystem.current.currentSelectedGameObject);
    }

    [Test]
    public void ResetAfterUiBoundary_ClearsClickFlagsOnEveryLoadedSceneFlowchart()
    {
        Flowchart first = CreateFlowchartWithClickFlags("FirstFlowchart");
        Flowchart second = CreateFlowchartWithClickFlags("SecondFlowchart");

        ClickInteractionCleanup.ResetAfterUiBoundary(first);

        Assert.IsFalse(first.GetBooleanVariable(FungusVariableKeys.IsClicked));
        Assert.IsFalse(first.GetBooleanVariable(FungusVariableKeys.WindowClicked));
        Assert.IsFalse(second.GetBooleanVariable(FungusVariableKeys.IsClicked));
        Assert.IsFalse(second.GetBooleanVariable(FungusVariableKeys.WindowClicked));
    }

    [Test]
    public void ResetAfterUiBoundary_ClearsUppercaseIsCalledUsedBySomeScenes()
    {
        Flowchart flowchart = CreateFlowchartWithClickFlags("UppercaseIsCalledFlowchart");
        AddBooleanVariable(flowchart, "IsCalled", true);

        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart);

        Assert.IsFalse(flowchart.GetBooleanVariable("IsCalled"));
    }

    [Test]
    public void ResetAfterUiBoundary_WhenWindowClickedResetSkipped_PreservesWindowClicked()
    {
        Flowchart flowchart = CreateFlowchartWithClickFlags("PreserveWindowClickedFlowchart");

        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked: false);

        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
        Assert.IsTrue(flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked));
    }

    private Flowchart CreateFlowchartWithClickFlags(string name)
    {
        GameObject flowchartObject = new GameObject(name);
        if (firstFlowchartObject == null)
            firstFlowchartObject = flowchartObject;
        else
            secondFlowchartObject = flowchartObject;

        Flowchart flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, FungusVariableKeys.IsClicked, true);
        AddBooleanVariable(flowchart, FungusVariableKeys.WindowClicked, true);
        return flowchart;
    }

    private static void AddBooleanVariable(Flowchart flowchart, string key, bool value)
    {
        BooleanVariable variable = flowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        flowchart.Variables.Add(variable);
    }
}
