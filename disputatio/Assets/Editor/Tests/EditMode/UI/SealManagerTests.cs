using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

[TestFixture]
public class SealManagerTests
{
    GameObject root;
    Flowchart flowchart;
    SealManager sealManager;
    int invokeCount;

    [SetUp]
    public void SetUp()
    {
        invokeCount = 0;
        root = new GameObject("SealManagerTestRoot");
        flowchart = root.AddComponent<Flowchart>();
        for (int i = 1; i <= 7; i++)
            AddBooleanVariable(flowchart, $"seal{i}", false);
        AddBooleanVariable(flowchart, "allSealsComplete", false);

        sealManager = root.AddComponent<SealManager>();
        sealManager.flowchart = flowchart;
        sealManager.allSealsVar = "allSealsComplete";
        sealManager.sealVariableNames = new[]
        {
            "seal1", "seal2", "seal3", "seal4", "seal5", "seal6", "seal7",
        };
        sealManager.onAllSealsComplete = new UnityEvent();
        sealManager.onAllSealsComplete.AddListener(() => invokeCount++);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Update_WhenAllSealsCompleteAlreadyTrueAtStart_DoesNotInvokeAgain()
    {
        flowchart.SetBooleanVariable("allSealsComplete", true);
        InvokeSealManagerStart();

        SetAllSealVariables(true);
        InvokeSealManagerUpdate();
        InvokeSealManagerUpdate();

        Assert.AreEqual(0, invokeCount);
    }

    [Test]
    public void Update_WhenAllSealsBecomeTrue_InvokesOnceEvenOnSubsequentUpdates()
    {
        InvokeSealManagerStart();

        SetAllSealVariables(true);
        InvokeSealManagerUpdate();
        InvokeSealManagerUpdate();

        Assert.AreEqual(1, invokeCount);
        Assert.IsTrue(flowchart.GetBooleanVariable("allSealsComplete"));
    }

    void InvokeSealManagerStart() =>
        typeof(SealManager).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(sealManager, null);

    void InvokeSealManagerUpdate() =>
        typeof(SealManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(sealManager, null);

    void SetAllSealVariables(bool value)
    {
        for (int i = 1; i <= 7; i++)
            flowchart.SetBooleanVariable($"seal{i}", value);
    }

    static void AddBooleanVariable(Flowchart targetFlowchart, string key, bool value)
    {
        var variable = targetFlowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        targetFlowchart.Variables.Add(variable);
    }
}
