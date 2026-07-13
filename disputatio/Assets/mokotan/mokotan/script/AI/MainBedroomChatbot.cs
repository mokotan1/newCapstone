using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class MainBedroomChatbot : BaseChatbot
{
    [Header("Main Bedroom Puzzle Settings")]
    [SerializeField] public Flowchart mainFlowchart;

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        string roomPrompt = CheshirePromptCatalog.Load("MainBedroomPrompt", locale);
        if (!string.IsNullOrEmpty(roomPrompt))
            finalSystemPrompt += "\n\n" + roomPrompt;

        if (mainFlowchart != null)
        {
            bool diaryRead = mainFlowchart.GetBooleanVariable("DiaryRead");
            bool safeSolved = mainFlowchart.GetBooleanVariable("SafeSolved");

            if (!diaryRead)
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.MainBedroomGoalDiaryUnread(locale);
            }
            else if (diaryRead && !safeSolved)
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.MainBedroomGoalSafeLocked(locale);
            }
            else if (safeSolved)
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.MainBedroomGoalSafeOpen(locale);
            }
        }
        return finalSystemPrompt;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls)
    {
        bool isComplete = false;
        Say(responseMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        if (functionCalls != null)
        {
            foreach (var fc in functionCalls)
            {
                if (fc.name == "give_hint" || fc.name == "emote")
                    GameLog.Log($"[{fc.name}] {JsonUtility.ToJson(fc)}");
            }
        }
    }
}
