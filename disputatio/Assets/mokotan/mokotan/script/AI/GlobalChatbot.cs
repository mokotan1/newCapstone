using UnityEngine;
using System.Collections;
using Fungus;
using TMPro;

// BaseChatbot을 상속받습니다.
public class GlobalChatbot : BaseChatbot
{
    [Header("GlobalBot Settings")]
    [SerializeField] public Flowchart globalFlowchart;
    [SerializeField] private TMP_InputField userInputField;

    // (Awake, Start, LoadAPIKey, Say, ParseGPTResponse 등 모든 중복 코드 삭제)

    // --- 고유 기능: UI 버튼으로 메시지 전송 ---
    public void OnSendButtonClick()
    {
        string message = userInputField.text;
        if (!string.IsNullOrEmpty(message) && !isRequestInProgress)
        {
            // 부모의 GetGPTResponse 코루틴을 호출
            StartCoroutine(GetGPTResponse(message)); 
            userInputField.text = "";
            userInputField.ActivateInputField();
        }
    }

    // ▼▼▼ BaseChatbot의 추상 함수 구현 ▼▼▼

    protected override string BuildFinalSystemPrompt()
    {
        string finalSystemPrompt = chatHistory[0].content; // 기본 시스템 프롬프트

        if (globalFlowchart != null)
        {
            bool hasBottleFlag = globalFlowchart.GetBooleanVariable("GetBottle");
            string lastUserMessage = chatHistory[chatHistory.Count - 1].content;

            if (hasBottleFlag && (lastUserMessage.Contains("물병") || lastUserMessage.Contains("병")))
            {
                finalSystemPrompt += "\n\n[중요 지시] 플레이어는 '물병'에 대한 단서를 가지고 있으며, '물병'에 대해 질문했습니다. 부력에 대한 힌트를 수수께끼로 힌트를 주세요.";
            }
        }
        return finalSystemPrompt;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage)
    {
        // GlobalChatbot는 단순히 메시지를 표시하고 끝납니다.
        bool isComplete = false;
        Say(responseMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);
    }
}