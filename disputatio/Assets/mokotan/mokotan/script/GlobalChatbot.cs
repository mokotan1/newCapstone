using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Fungus;
using System;
using Newtonsoft.Json;
using TMPro;
// OpenAI API 데이터 구조 (이 부분은 다른 스크립트와 중복되지 않게 한 곳에만 두는 것이 좋습니다)
// [System.Serializable] public class OpenAIMessage ... (위와 동일)

public class GlobalChatbot : MonoBehaviour
{
    // --- Unity & Fungus 연결 변수 ---
    [SerializeField] private SayDialog chatSayDialog;
    [SerializeField] public Flowchart globalFlowchart; // 글로벌 flowchart만 참조
    [SerializeField] private TMP_InputField userInputField;

    // --- OpenAI API 설정 ---
    [SerializeField] private string API_KEY = "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"; // OpenAI 키를 입력하세요
    [SerializeField] private string modelName = "gpt-4o";
    private const string API_URL = "https://api.openai.com/v1/chat/completions";

    // --- 내부 변수 ---
    private List<OpenAIMessage> chatHistory = new List<OpenAIMessage>();
    private bool isRequestInProgress = false;

    private void Start()
    {
        InitializeChatHistory();
    }

    private void InitializeChatHistory()
    {
        chatHistory.Clear();
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt_global"); // 글로벌용 프롬프트
        string basePrompt = introTextAsset != null ? introTextAsset.text : "You are a helpful assistant.";
        chatHistory.Add(new OpenAIMessage { role = "system", content = basePrompt });
    }

    // 이 스크립트는 플레이어의 직접 입력으로만 작동한다고 가정
    public void SendMessageToChatbot(string userMessage) // Fungus 입력이나 UI 입력에서 호출
    {
        if (isRequestInProgress) return;
        if (string.IsNullOrEmpty(userMessage)) return;

        chatHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
        StartCoroutine(GetGPTResponse());
    }

    public void OnSendButtonClick()
    {
        // InputField의 텍스트를 가져옵니다.
        string message = userInputField.text;
        
        // 비어있지 않다면 메시지 전송 함수를 호출합니다.
        if (!string.IsNullOrEmpty(message))
        {
            SendMessageToChatbot(message);
            
            // 입력 필드를 다시 비워줍니다.
            userInputField.text = "";
            userInputField.ActivateInputField(); // 다시 입력하기 편하도록 포커스 설정
        }
    }

    private IEnumerator GetGPTResponse()
    {
        isRequestInProgress = true;
        Say("...", null);

        string finalSystemPrompt = chatHistory[0].content;

        // --- 글로벌 관련 로직만 남김 ---
        if (globalFlowchart != null)
        {
            bool hasBottleFlag = globalFlowchart.GetBooleanVariable("GetBottle");
            string lastUserMessage = chatHistory[chatHistory.Count - 1].content;

            if (hasBottleFlag && (lastUserMessage.Contains("물병") || lastUserMessage.Contains("병")))
            {
                finalSystemPrompt += "\n\n[중요 지시] 플레이어는 '물병'에 대한 단서를 가지고 있으며, '물병'에 대해 질문했습니다. 부력에 대한 힌트를 수수께끼로 힌트를 주세요.";
            }
        }

        List<OpenAIMessage> requestMessages = new List<OpenAIMessage>(chatHistory);
        requestMessages[0] = new OpenAIMessage { role = "system", content = finalSystemPrompt };

        OpenAIPayload payload = new OpenAIPayload { model = this.modelName, messages = requestMessages };
        string payloadJson = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest(API_URL, "POST"))
        {
            // ... (API 요청 부분은 KitchenChatbot과 동일)
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + API_KEY);

            yield return request.SendWebRequest();

            string chatbotResponse;
            if (request.result != UnityWebRequest.Result.Success)
            {
                chatbotResponse = "오류: " + request.error;
                Debug.LogError("Error: " + request.error + " | Response: " + request.downloadHandler.text);
            }
            else
            {
                chatbotResponse = ParseGPTResponse(request.downloadHandler.text);
                chatHistory.Add(new OpenAIMessage { role = "assistant", content = chatbotResponse });
            }

            isRequestInProgress = false;
            yield return StartCoroutine(DisplayMessageChunks(chatbotResponse));
        }
    }
     private string ParseGPTResponse(string json)
    {
        try
        {
            OpenAIResponse response = JsonConvert.DeserializeObject<OpenAIResponse>(json);
            return response.choices[0].message.content;
        }
        catch (Exception e)
        {
            Debug.LogError("JSON 파싱 오류: " + e.Message);
            return "답변 파싱 실패";
        }
    }

    private IEnumerator DisplayMessageChunks(string message)
    {
        bool isComplete = false;
        Say(message, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);
    }
    
    private void Say(string message, System.Action onComplete = null)
    {
        if (chatSayDialog != null)
        {
            if (!chatSayDialog.gameObject.activeInHierarchy) chatSayDialog.gameObject.SetActive(true);
            chatSayDialog.Say(message, true, true, false, true, true, null, onComplete);
        }
    }
}
