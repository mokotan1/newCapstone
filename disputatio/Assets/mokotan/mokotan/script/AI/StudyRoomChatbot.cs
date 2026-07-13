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
        => BuildAlreadySolvedInstruction(CheshireLocaleResolver.ResolveCurrentLocale());

    public static string BuildAlreadySolvedInstruction(string locale)
        => CheshireDynamicPromptFragments.StudyAlreadySolved(locale);

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        string roomPrompt = CheshirePromptCatalog.Load("StudyRoomPrompt", locale);
        if (!string.IsNullOrEmpty(roomPrompt))
            finalSystemPrompt += "\n\n" + roomPrompt;

        if (IsPuzzleSolved(studyFlowchart))
            finalSystemPrompt += BuildAlreadySolvedInstruction(locale);

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
