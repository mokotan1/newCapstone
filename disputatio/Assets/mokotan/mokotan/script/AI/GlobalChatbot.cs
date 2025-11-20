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

    // (Awake, LoadAPIKey, Say, ParseGPTResponse 등 중복 코드 생략)

    // ▼▼▼ [수정됨] 초기화 부분 추가 ▼▼▼
    protected override void Start()
    {
        // 1. 부모(BaseChatbot)의 Start를 먼저 실행해야 
        //    API 키 로드나 기본 프롬프트 초기화가 정상적으로 작동합니다.
        base.Start(); 

        // 2. 그 다음 GlobalChatbot만의 추가 기능을 실행합니다.
        if (userInputField != null)
        {
            // 엔터 키(Submit) 이벤트에 리스너 연결
            userInputField.onSubmit.AddListener(OnSubmit);
        }
    }

    // ▼▼▼ [추가됨] 엔터 키 입력 시 호출되는 함수 ▼▼▼
    private void OnSubmit(string text)
    {
        // 엔터를 쳤을 때도 버튼을 클릭한 것과 동일한 로직 수행
        // 이미 Enter를 쳤으므로 다시 버튼을 누를 필요 없이 바로 전송 함수 호출
        OnSendButtonClick();
        
        // 전송 후 인풋 필드에 포커스를 유지하고 싶다면 아래 줄 유지
        userInputField.ActivateInputField(); 
    }

    // --- 고유 기능: UI 버튼 및 엔터 키로 메시지 전송 ---
    public void OnSendButtonClick()
    {
        string message = userInputField.text;

        // 메시지가 비어있지 않고, 현재 요청 중이 아닐 때만 실행
        if (!string.IsNullOrEmpty(message) && !isRequestInProgress)
        {
            // 부모의 GetGPTResponse 코루틴을 호출
            StartCoroutine(GetGPTResponse(message)); 
            
            userInputField.text = ""; // 입력창 비우기
            
            // 엔터를 친 후에도 계속 타이핑 할 수 있게 포커스 유지
            userInputField.ActivateInputField();
        }
    }

    // ▼▼▼ BaseChatbot의 추상 함수 구현 (기존 유지) ▼▼▼

    protected override string BuildFinalSystemPrompt()
    {
        string finalSystemPrompt = chatHistory[0].content; // 기본 시스템 프롬프트

        if (globalFlowchart != null)
        {
            bool hasBottleFlag = globalFlowchart.GetBooleanVariable("GetBottle");
            // chatHistory가 비어있을 수 있는 예외 처리를 살짝 보강하면 좋습니다.
            if(chatHistory.Count > 0) 
            {
                string lastUserMessage = chatHistory[chatHistory.Count - 1].content;

                if (hasBottleFlag && (lastUserMessage.Contains("물병") || lastUserMessage.Contains("병")))
                {
                    finalSystemPrompt += "\n\n[중요 지시] 플레이어는 '물병'에 대한 단서를 가지고 있으며, '물병'에 대해 질문했습니다. 부력에 대한 힌트를 수수께끼로 힌트를 주세요.";
                }
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
    
    // [권장] 스크립트가 파괴될 때 리스너 해제 (메모리 누수 방지)
    private void OnDestroy()
    {
        if (userInputField != null)
        {
            userInputField.onSubmit.RemoveListener(OnSubmit);
        }
    }
}