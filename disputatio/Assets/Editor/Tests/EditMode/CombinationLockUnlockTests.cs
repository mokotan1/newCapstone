using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

public class CombinationLockUnlockTests
{
    private GameObject eventSystemObject;
    private GameObject selectedObject;
    private GameObject flowchartObject;
    private GameObject lockObject;

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
        Object.DestroyImmediate(lockObject);
        Object.DestroyImmediate(flowchartObject);
        Object.DestroyImmediate(selectedObject);
        Object.DestroyImmediate(eventSystemObject);
    }

    [Test]
    public void CheckAnswer_WhenCorrect_PresentsChildPickupOutsideClosedPanel()
    {
        Flowchart flowchart = CreateFlowchartWithBool("HaveChildKey", false);
        CombinationLock combinationLock = CreateSolvedCombinationLock(flowchart);
        ItemPickup pickup = CreateChildPickup(combinationLock.transform, flowchart, "HaveChildKey");

        combinationLock.CheckAnswer();

        Assert.IsTrue(pickup.gameObject.activeSelf);
        Assert.AreNotSame(combinationLock.transform, pickup.transform.parent);
        Assert.IsNotNull(pickup.GetComponent<CanvasGroup>());
        Assert.IsFalse(flowchart.GetBooleanVariable("HaveChildKey"));
    }

    [Test]
    public void CheckAnswer_WhenCorrect_ResetsClickStateWhenClosingPanel()
    {
        Flowchart flowchart = CreateFlowchartWithBool(FungusVariableKeys.IsClicked, true);
        AddBooleanVariable(flowchart, FungusVariableKeys.WindowClicked, true);
        CombinationLock combinationLock = CreateSolvedCombinationLock(flowchart);

        combinationLock.CheckAnswer();

        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.IsClicked));
        Assert.IsFalse(flowchart.GetBooleanVariable(FungusVariableKeys.WindowClicked));
        Assert.IsNull(EventSystem.current.currentSelectedGameObject);
        Assert.IsFalse(combinationLock.gameObject.activeSelf);
    }

    private Flowchart CreateFlowchartWithBool(string key, bool value)
    {
        flowchartObject = new GameObject("CombinationLockUnlockTests_Flowchart");
        Flowchart flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, "solved", false);
        AddBooleanVariable(flowchart, key, value);
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

    private CombinationLock CreateSolvedCombinationLock(Flowchart flowchart)
    {
        lockObject = new GameObject("DrawerPanel");
        CombinationLock combinationLock = lockObject.AddComponent<CombinationLock>();
        combinationLock.correctAnswer = "150405";
        combinationLock.numberOfDigits = 6;
        combinationLock.flowchart = flowchart;

        typeof(CombinationLock)
            .GetField("currentDigits", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(combinationLock, new[] { 1, 5, 0, 4, 0, 5 });

        return combinationLock;
    }

    private static ItemPickup CreateChildPickup(Transform parent, Flowchart flowchart, string variableName)
    {
        GameObject pickupObject = new GameObject("ChildRoomKey");
        pickupObject.transform.SetParent(parent);
        ItemPickup pickup = pickupObject.AddComponent<ItemPickup>();
        pickup.fungusVariableName = variableName;

        typeof(ItemPickup)
            .GetField("targetFlowchart", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(pickup, flowchart);

        return pickup;
    }
}
