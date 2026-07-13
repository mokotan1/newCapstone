using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class WifeRoomChatbot : BaseChatbot
{
    [Header("WifeRoom Settings")]
    [SerializeField] public Flowchart wifeFlowchart;

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        string roomPrompt = CheshirePromptCatalog.Load("WifeRoomPrompt", locale);
        if (!string.IsNullOrEmpty(roomPrompt))
            finalSystemPrompt += "\n\n" + roomPrompt;

        if (wifeFlowchart != null)
        {
            if (wifeFlowchart.GetBooleanVariable("CheckedMirror"))
            {
                finalSystemPrompt += CheshireDynamicPromptFragments.WifeRoomMirrorFound(locale);
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
