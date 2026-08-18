using Fungus;
using NUnit.Framework;
using UnityEngine;

public class StudyRoomChatbotTests
{
    private GameObject flowchartObject;

    [TearDown]
    public void TearDown()
    {
        if (flowchartObject != null)
            Object.DestroyImmediate(flowchartObject);
    }

    [Test]
    public void IsPuzzleSolved_ReturnsTrue_WhenDiarySolvedIsTrue()
    {
        Flowchart flowchart = CreateFlowchart();
        flowchart.SetBooleanVariable("DiarySolved", true);

        Assert.IsTrue(StudyRoomChatbot.IsPuzzleSolved(flowchart));
    }

    [Test]
    public void BuildAlreadySolvedInstruction_UsesChesterAlreadySolvedMessage()
    {
        string instruction = StudyRoomChatbot.BuildAlreadySolvedInstruction(CheshireLocaleResolver.Korean);

        StringAssert.Contains("이미 문제를 풀었어", instruction);
        StringAssert.Contains("새 열쇠", instruction);
    }

    [Test]
    public void BuildAlreadySolvedInstruction_English_DoesNotContainKoreanGoalHeader()
    {
        string instruction = StudyRoomChatbot.BuildAlreadySolvedInstruction(CheshireLocaleResolver.English);

        Assert.IsFalse(instruction.Contains("[현재 목표]"), instruction);
        StringAssert.Contains("Current goal", instruction);
        StringAssert.Contains("already", instruction.ToLowerInvariant());
    }

    private Flowchart CreateFlowchart()
    {
        flowchartObject = new GameObject("Flowchart");
        Flowchart flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, "DiarySolved", false);
        AddBooleanVariable(flowchart, "HaveTutorKey", false);
        return flowchart;
    }

    private static void AddBooleanVariable(Flowchart targetFlowchart, string key, bool value)
    {
        BooleanVariable variable = targetFlowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Scope = VariableScope.Public;
        variable.Value = value;
        targetFlowchart.Variables.Add(variable);
    }
}
