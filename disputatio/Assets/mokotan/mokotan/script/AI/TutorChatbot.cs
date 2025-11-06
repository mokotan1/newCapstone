using UnityEngine;
using System.Collections;
using Fungus;
using Newtonsoft.Json; // JSON 파싱을 위해 필요
using System;
using UnityEngine.Events; // UnityEvent를 위해 필요

// BaseChatbot을 상속받습니다.
[System.Serializable]
public class ChatbotStatus
{
    public bool is_correct;
    public bool quiz_complete;
}
public class TutorChatbot : BaseChatbot
{
    [Header("TutorBot Settings")]
    [SerializeField] public Flowchart flowchart;

    [Header("Events")]
    public UnityEvent OnAIReponseComplete;
    public UnityEvent OnQuizCompletedEvent;

    // (Awake, Start, LoadAPIKey, Say, ParseGPTResponse 등 모든 중복 코드 삭제)

    // --- 고유 기능: 외부에서 플레이어 메시지 전달 ---
    public void AddPlayerMessageAndGetResponse(string playerMessage)
    {
        if (isRequestInProgress)
        {
            Debug.LogWarning("이미 AI 응답 요청이 진행 중입니다. 새로운 요청을 무시합니다.");
            return;
        }

        // 부모의 GetGPTResponse 코루틴을 호출
        StartCoroutine(GetGPTResponse(playerMessage));
        Debug.Log($"플레이어 답변 전송 및 GPT 응답 요청: {playerMessage}");
    }

    // ▼▼▼ BaseChatbot의 추상 함수 구현 ▼▼▼

    protected override string BuildFinalSystemPrompt()
    {
        string finalSystemPrompt = chatHistory[0].content; // 기본 시스템 프롬프트

        if (flowchart != null)
        {
            bool WindowClicked = flowchart.GetBooleanVariable("WindowClicked");
            if (WindowClicked)
            {
                TextAsset tutorRoomPromptAsset = Resources.Load<TextAsset>("TutorRoomPrompt");
                if (tutorRoomPromptAsset != null)
                {
                    int currentCorrectCount = flowchart.GetIntegerVariable("CorrectAnswerCount");
                    finalSystemPrompt += $"\n\n[현재 진행 상황] 플레이어는 현재까지 {currentCorrectCount}/5번 정답을 맞췄습니다.\n\n" + tutorRoomPromptAsset.text;
                }
                else
                {
                    Debug.LogError("TutorRoomPrompt.txt 파일을 찾을 수 없습니다!");
                    // (대체 프롬프트 로직은 원본 코드를 따름)
                    finalSystemPrompt += "\n\n[중요 지시]... (TutorRoomPrompt 내용)...";
                }
            }
        }
        return finalSystemPrompt;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage)
    {
        // TutorChatbot는 복잡한 JSON 파싱 및 이벤트 처리를 합니다.

        // 1. 메시지에서 JSON 부분 추출 및 파싱
        ChatbotStatus status = null;
        string cleanedMessage = responseMessage;

        int jsonStartIndex = responseMessage.LastIndexOf("{\"is_correct\"");
        if (jsonStartIndex != -1)
        {
            string jsonString = responseMessage.Substring(jsonStartIndex);
            try
            {
                status = JsonConvert.DeserializeObject<ChatbotStatus>(jsonString);
                cleanedMessage = responseMessage.Substring(0, jsonStartIndex).Trim();
            }
            catch (Exception e)
            {
                Debug.LogError("ChatbotStatus JSON 파싱 오류: " + e.Message + " | JSON: " + jsonString);
            }
        }

        // 2. JSON이 제거된 메시지를 SayDialog로 표시
        bool isComplete = false;
        Say(cleanedMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        // 3. 파싱된 상태에 따라 Fungus 변수 업데이트 및 이벤트 트리거
        if (status != null)
        {
            if (status.is_correct)
            {
                IncrementCorrectAnswerCount();
            }
            if (status.quiz_complete)
            {
                OnQuizCompletedEvent?.Invoke();
            }
        }

        // 4. AI 응답 표시 완료 후 일반 이벤트 호출
        OnAIReponseComplete?.Invoke();
    }

    // --- TutorChatbot 고유 헬퍼 함수 ---
    public void IncrementCorrectAnswerCount()
    {
        if (flowchart != null)
        {
            int currentCount = flowchart.GetIntegerVariable("CorrectAnswerCount");
            flowchart.SetIntegerVariable("CorrectAnswerCount", currentCount + 1);
            Debug.Log($"CorrectAnswerCount increased to: {currentCount + 1}");
        }
    }
}