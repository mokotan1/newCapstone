using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Fungus;
using System;
using Newtonsoft.Json;
using TMPro;


// OpenAI API 데이터 구조 클래스들은 별도 파일에 있어야 합니다.

public class GlobalChatbot : MonoBehaviour
{
    // --- Unity & Fungus 연결 변수 ---
    [SerializeField] private SayDialog chatSayDialog;
    [SerializeField] public Flowchart globalFlowchart;
    [SerializeField] private TMP_InputField userInputField;

    // --- OpenAI API 설정 ---
    private string API_KEY; // ◀ 값을 직접 할당하지 않고, 파일에서 읽어올 변수
    [SerializeField] private string modelName = "gpt-4o";
    private const string API_URL = "https://api.openai.com/v1/chat/completions";

    // --- 내부 변수 ---
    private List<OpenAIMessage> chatHistory = new List<OpenAIMessage>();
    private bool isRequestInProgress = false;

    // ▼▼▼ API 키를 로드하기 위한 Awake() 함수 추가 ▼▼▼
    private void Awake()
    {
        LoadAPIKey();
    }

    private void Start()
    {
        InitializeChatHistory();
    }
    
    // ▼▼▼ API 키를 파일에서 읽어오는 함수 추가 ▼▼▼
    private void LoadAPIKey()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("APIKey"); // Resources 폴더의 APIKey.txt 로드
        if (keyFile != null)
        {
            API_KEY = keyFile.text.Trim(); // Trim()으로 공백 제거
            Debug.Log("OpenAI API Key loaded successfully.");
        }
        else
        {
            Debug.LogError("API 키 파일을 찾을 수 없습니다! Assets/Resources/APIKey.txt 경로를 확인해주세요.");
        }
    }

    private void InitializeChatHistory()
    {
        chatHistory.Clear();
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt");
        string basePrompt = introTextAsset != null ? introTextAsset.text : "You are a helpful assistant.";
        chatHistory.Add(new OpenAIMessage { role = "system", content = basePrompt });
    }

    public void SendMessageToChatbot(string userMessage)
    {
        if (isRequestInProgress) return;
        if (string.IsNullOrEmpty(userMessage)) return;

        chatHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
        StartCoroutine(GetGPTResponse());
    }

    public void OnSendButtonClick()
    {
        string message = userInputField.text;
        if (!string.IsNullOrEmpty(message))
        {
            SendMessageToChatbot(message);
            userInputField.text = "";
            userInputField.ActivateInputField();
        }
    }

    private IEnumerator GetGPTResponse()
    {
        // (이하 GetGPTResponse 함수 및 다른 함수들은 기존과 동일합니다)
        isRequestInProgress = true;
        Say("...", null);

        string finalSystemPrompt = chatHistory[0].content;

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