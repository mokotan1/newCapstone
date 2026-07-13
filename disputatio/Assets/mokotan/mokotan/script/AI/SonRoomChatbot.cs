using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class SonRoomChatbot : BaseChatbot
{
    /// <summary><see cref="ItemAcquisitionTracker"/> 레거시 매핑(HasBible)과 동일한 성경 아이템 ID.</summary>
    private const int IllustratedBibleItemId = 19;

    [Header("Son's Room Puzzle Settings")]
    [SerializeField] public Flowchart sonFlowchart;

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        string roomPrompt = CheshirePromptCatalog.Load("SonRoomPrompt", locale);
        if (!string.IsNullOrEmpty(roomPrompt))
            finalSystemPrompt += "\n\n" + roomPrompt;

        if (sonFlowchart != null)
        {
            // 성경: Variablemanager(AcquiredItemsMask)에 기록됨. ChildRoom 플로우차트에는 HasBible이 없을 수 있음.
            Flowchart globalFc = FlowchartLocator.Find();
            bool hasBible =
                (globalFc != null && ItemAcquisitionTracker.IsAcquired(globalFc, IllustratedBibleItemId))
                || sonFlowchart.GetBooleanVariable("HasBible");

            // 실제 씬 퍼즐은 seal1~7 + SealManager → allSealsComplete (HorsesPlacedCount는 미사용·미갱신이었음).
            bool sealsComplete = sonFlowchart.GetBooleanVariable("allSealsComplete");

            if (!hasBible)
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.SonRoomGoalNeedBible(locale);
            }
            else if (!sealsComplete)
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.SonRoomGoalSealsIncomplete(locale);
            }
            else
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.SonRoomGoalComplete(locale);
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
