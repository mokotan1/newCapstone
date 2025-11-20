using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Fungus;
using System;
using Newtonsoft.Json;
using TMPro;

public abstract class BaseChatbot : MonoBehaviour
{
    // --- 공통 Unity & Fungus 연결 변수 ---
    [Header("Base Settings")]
    [SerializeField] protected SayDialog chatSayDialog;

    // ▼▼▼ [수정됨] 애니메이션 설정 (Trigger 방식) ▼▼▼
    [Header("Animation Settings")]
    [SerializeField] protected Animator npcAnimator; 
    [SerializeField] protected string talkTriggerName = "TalkTrigger"; // Bool이 아니라 Trigger 이름을 씁니다.

    // --- 공통 OpenAI API 설정 ---
    protected string API_KEY; 
    [SerializeField] protected string modelName = "gpt-4o";
    protected const string API_URL = "https://api.openai.com/v1/chat/completions";

    protected List<OpenAIMessage> chatHistory = new List<OpenAIMessage>();
    protected bool isRequestInProgress = false;

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
        if (keyFile != null) API_KEY = keyFile.text.Trim(); 
    }

    protected virtual void InitializeChatHistory()
    {
        chatHistory.Clear();
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
            return "답변 파싱 실패";
        }
    }

    // --- 핵심 로직: API 요청 템플릿 ---
    
    protected IEnumerator GetGPTResponse(string userMessage = null)
    {
        if (isRequestInProgress) yield break;
        isRequestInProgress = true;

        // ▼▼▼ [핵심 수정] 요청 시작 시 애니메이션 1회 재생 ▼▼▼
        if (npcAnimator != null)
        {
            // Trigger는 "한 번 실행하라"는 신호입니다.
            npcAnimator.SetTrigger(talkTriggerName);
        }

        Say("...", null); // 생각 중 표시

        if (!string.IsNullOrEmpty(userMessage))
        {
            chatHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
        }

        string finalSystemPrompt = BuildFinalSystemPrompt();

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
            }
            else
            {
                chatbotResponse = ParseGPTResponse(request.downloadHandler.text);
                chatHistory.Add(new OpenAIMessage { role = "assistant", content = chatbotResponse });
            }

            isRequestInProgress = false;

            yield return StartCoroutine(HandleChatbotResponse(chatbotResponse));
        }
    }

    protected abstract string BuildFinalSystemPrompt();
    protected abstract IEnumerator HandleChatbotResponse(string responseMessage);
}