using UnityEngine;
using System.Collections;
using Fungus;

// BaseChatbot을 상속받습니다.
public class KitchenChatbot : BaseChatbot
{
    [Header("KitchenBot Settings")]
    [SerializeField] public Flowchart kitchenFlowchart;

    // (Awake, Start, LoadAPIKey, Say, ParseGPTResponse 등 모든 중복 코드 삭제)

    // --- 고유 기능: 플래그로 AI 응답 트리거 ---
    public void TriggerAIResponseByFlag()
    {
        if (isRequestInProgress) return;
        
        string actionText = "(플레이어가 나에게 음식을 주었다.)";
        // 부모의 GetGPTResponse 코루틴을 호출
        StartCoroutine(GetGPTResponse(actionText));
        Debug.Log("call");
    }

    // ▼▼▼ BaseChatbot의 추상 함수 구현 ▼▼▼

    protected override string BuildFinalSystemPrompt()
    {
        string finalSystemPrompt = chatHistory[0].content; // 기본 시스템 프롬프트

        if (kitchenFlowchart != null)
        {
            bool giveFood = kitchenFlowchart.GetBooleanVariable("giveFood");
            if (giveFood)
            {
                finalSystemPrompt += "\n\n[중요 지시] 플레이어는 당신에게 먹이를 주었습니다. 그에 대한 감사로 '카레'에 대한 힌트를 수수께끼로 내주세요. 수수께끼는 내지만 음식이라는 것은 알 수 있도록 내주세요.";
            }
        }
        return finalSystemPrompt;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage)
    {
        // KitchenChatbot는 메시지 표시 후 대화창을 닫습니다.
        bool isComplete = false;
        Say(responseMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        if (chatSayDialog != null)
        {
            chatSayDialog.gameObject.SetActive(false);
        }
    }
}