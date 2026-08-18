using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class KitchenChatbot : BaseChatbot
{
    [Header("KitchenBot Settings")]
    [SerializeField] public Flowchart kitchenFlowchart;

    [Header("Kitchen puzzle — 요리책 카레 (시스템 주입, 플레이어 비표시)")]
    [Tooltip("요리책에서 카레 레시피가 나오는 첫 페이지 번호.")]
    [SerializeField] private int curryRecipePageStart = 18;
    [Tooltip("이어지는 다음 페이지(연속 장).")]
    [SerializeField] private int curryRecipePageEnd = 19;

    public void TriggerAIResponseByFlag()
    {
        if (isRequestInProgress) return;

        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        string actionText = CheshireDynamicPromptFragments.KitchenGiveFoodActionText(locale);
        StartCoroutine(GetGPTResponse(actionText));
        GameLog.Log("call");
    }

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string finalSystemPrompt = chatHistory[0].content;

        string roomPrompt = CheshirePromptCatalog.Load("KitchenPrompt", locale);
        if (!string.IsNullOrEmpty(roomPrompt))
            finalSystemPrompt += "\n\n" + roomPrompt;

        if (kitchenFlowchart != null)
        {
            bool giveFood = kitchenFlowchart.GetBooleanVariable("giveFood");
            if (giveFood)
            {
                finalSystemPrompt += BuildGiveFoodSecretDesignBlock(
                    locale, curryRecipePageStart, curryRecipePageEnd);
                finalSystemPrompt += CheshireDynamicPromptFragments.KitchenGiveFoodPostInstruction(locale);
            }
        }
        return finalSystemPrompt;
    }

    /// <summary>플레이어에게 노출되지 않는 퍼즐 진실. LLM만 읽는다.</summary>
    public static string BuildGiveFoodSecretDesignBlock(int pageA, int pageB)
        => BuildGiveFoodSecretDesignBlock(
            CheshireLocaleResolver.ResolveCurrentLocale(), pageA, pageB);

    public static string BuildGiveFoodSecretDesignBlock(string locale, int pageA, int pageB)
        => CheshireDynamicPromptFragments.KitchenGiveFoodSecret(locale, pageA, pageB);

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

        if (chatSayDialog != null)
        {
            chatSayDialog.gameObject.SetActive(false);
        }
    }
}
