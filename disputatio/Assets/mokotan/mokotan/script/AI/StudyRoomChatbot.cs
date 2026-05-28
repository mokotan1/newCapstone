using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;

public class StudyRoomChatbot : BaseChatbot
{
    [Header("StudyRoom Settings")]
    [SerializeField] public Flowchart studyFlowchart;

    public static bool IsPuzzleSolved(Flowchart flowchart)
    {
        if (flowchart == null)
            return false;

        return flowchart.GetBooleanVariable("DiarySolved")
            || flowchart.GetBooleanVariable("HaveTutorKey");
    }

    public static string BuildAlreadySolvedInstruction()
    {
        return "\n\n[현재 목표] 플레이어는 이미 공부방 문제를 풀었습니다. "
            + "\"나는 이미 문제를 풀었어\" 형식으로 짧게 말하고, 새 열쇠나 새 보상을 얻는 듯 말하지 마세요.";
    }

    protected override string BuildFinalSystemPrompt()
    {
        string finalSystemPrompt = chatHistory[0].content;

        TextAsset promptAsset = Resources.Load<TextAsset>("StudyRoomPrompt");
        if (promptAsset != null)
            finalSystemPrompt += "\n\n" + promptAsset.text;

        if (IsPuzzleSolved(studyFlowchart))
            finalSystemPrompt += BuildAlreadySolvedInstruction();

        return finalSystemPrompt;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls)
    {
        bool isComplete = false;
        Say(responseMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        if (functionCalls == null)
            yield break;

        foreach (var fc in functionCalls)
        {
            if (fc.name == "give_hint" || fc.name == "emote")
                GameLog.Log($"[{fc.name}] {JsonUtility.ToJson(fc)}");
        }
    }
}
