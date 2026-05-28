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
        string instruction = StudyRoomChatbot.BuildAlreadySolvedInstruction();

        StringAssert.Contains("이미 문제를 풀었어", instruction);
        StringAssert.Contains("새 열쇠", instruction);
    }

    private Flowchart CreateFlowchart()
    {
        flowchartObject = new GameObject("Flowchart");
        Flowchart flowchart = flowchartObject.AddComponent<Flowchart>();
        flowchart.Variables.Add(new BooleanVariable { Key = "DiarySolved", Value = false });
        flowchart.Variables.Add(new BooleanVariable { Key = "HaveTutorKey", Value = false });
        return flowchart;
    }
}
