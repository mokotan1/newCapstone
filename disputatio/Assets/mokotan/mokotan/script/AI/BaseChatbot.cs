// BaseChatbot.cs
// 이 스크립트는 모든 챗봇의 '부모' 역할을 합니다.
// MonoBehaviour를 상속받으며 'abstract' 키워드를 사용합니다.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Fungus;
using System;
using Newtonsoft.Json;
using TMPro;

// (OpenAIMessage, OpenAIResponse 등 JSON용 클래스들은 별도 파일에 있다고 가정)

public abstract class BaseChatbot : MonoBehaviour
{
    // --- 공통 Unity & Fungus 연결 변수 ---
    [Header("Base Settings")]
    [SerializeField] protected SayDialog chatSayDialog;

    // --- 공통 OpenAI API 설정 ---
    protected string API_KEY; 
    [SerializeField] protected string modelName = "gpt-4o";
    protected const string API_URL = "https://api.openai.com/v1/chat/completions";

    // --- 공통 내부 변수 ---
    protected List<OpenAIMessage> chatHistory = new List<OpenAIMessage>();
    protected bool isRequestInProgress = false;

    // ▼▼▼ 모든 자식 클래스가 공통으로 사용하는 함수들 ▼▼▼

    protected virtual void Awake()
    {
        LoadAPIKey();
    }

    protected virtual void Start()
    {
        InitializeChatHistory();
    }

    protected virtual void LoadAPIKey()
    {
        TextAsset keyFile = Resources.Load<TextAsset>("APIKey"); 
        if (keyFile != null)
        {
            API_KEY = keyFile.text.Trim(); 
            Debug.Log($"OpenAI API Key loaded for {this.GetType().Name}.");
        }
        else
        {
            Debug.LogError("API 키 파일을 찾을 수 없습니다! Assets/Resources/APIKey.txt");
        }
    }

    protected virtual void InitializeChatHistory()
    {
        chatHistory.Clear();
        // 각 챗봇이 다른 introPrompt를 사용할 수 있으므로, 자식 클래스에서 재정의(override) 가능
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt"); 
        string basePrompt = introTextAsset != null ? introTextAsset.text : "You are a helpful assistant.";
        chatHistory.Add(new OpenAIMessage { role = "system", content = basePrompt });
    }

    protected void Say(string message, System.Action onComplete = null)
    {
        if (chatSayDialog != null)
        {
            if (!chatSayDialog.gameObject.activeInHierarchy) chatSayDialog.gameObject.SetActive(true);
            chatSayDialog.Say(message, true, true, false, true, true, null, onComplete);
        }
    }

    protected string ParseGPTResponse(string json)
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

    // --- 핵심 로직: API 요청 템플릿 ---
    
    /// <summary>
    /// GPT 응답을 요청하는 공통 코루틴입니다.
    /// </summary>
    /// <param name="userMessage">플레이어가 입력한 메시지. (null 가능)</param>
    protected IEnumerator GetGPTResponse(string userMessage = null)
    {
        if (isRequestInProgress) yield break;
        isRequestInProgress = true;
        Say("...", null); // 대기 중 표시

        // 1. 유저 메시지가 있다면 대화 기록에 추가
        if (!string.IsNullOrEmpty(userMessage))
        {
            chatHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
        }

        // 2. ★★★ 프롬프트 조립 (자식 클래스에서 구현) ★★★
        string finalSystemPrompt = BuildFinalSystemPrompt();

        List<OpenAIMessage> requestMessages = new List<OpenAIMessage>(chatHistory);
        requestMessages[0] = new OpenAIMessage { role = "system", content = finalSystemPrompt };

        OpenAIPayload payload = new OpenAIPayload { model = this.modelName, messages = requestMessages };
        string payloadJson = JsonConvert.SerializeObject(payload);

        // 3. (공통) 웹 요청 전송
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

            // 4. ★★★ 응답 처리 (자식 클래스에서 구현) ★★★
            yield return StartCoroutine(HandleChatbotResponse(chatbotResponse));
        }
    }

    // ▼▼▼ 자식 클래스가 반드시 구현해야 하는 '추상' 함수들 ▼▼▼

    /// <summary>
    /// 각 챗봇의 상황(Fungus 변수 등)에 맞게 최종 시스템 프롬프트를 조립합니다.
    /// </summary>
    protected abstract string BuildFinalSystemPrompt();

    /// <summary>
    /// GPT의 응답(chatbotResponse)을 받은 후,
    /// 메시지를 어떻게 표시하고 어떤 후속 처리를 할지 정의합니다.
    /// </summary>
    protected abstract IEnumerator HandleChatbotResponse(string responseMessage);
}