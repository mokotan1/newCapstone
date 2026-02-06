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
    // --- 로컬 서버 설정 ---
    [Header("Local AI Settings")]
    [SerializeField] protected string localServerUrl = "http://localhost:5000/chat";

    // --- 공통 Unity & Fungus 연결 변수 ---
    [Header("Base Settings")]
    [SerializeField] protected SayDialog chatSayDialog;
    [SerializeField] protected TMP_InputField targetInputField; 

    [Header("Animation Settings")]
    [SerializeField] protected Animator npcAnimator; 
    [SerializeField] protected string talkTriggerName = "TalkTrigger";

    protected List<OpenAIMessage> chatHistory = new List<OpenAIMessage>();
    protected bool isRequestInProgress = false;

    [Serializable]
    public class LocalLlamaPayload
    {
        public string prompt;
        public string system;
    }

    [Serializable]
    public class LocalLlamaResponse
    {
        public string response;
    }

    [Serializable]
    public class OpenAIMessage
    {
        public string role;
        public string content;
    }

    protected virtual void Awake() { }

    protected virtual void Start()
    {
        InitializeChatHistory();
    }

    protected virtual void InitializeChatHistory()
    {
        chatHistory.Clear();
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt"); 
        string basePrompt = introTextAsset != null ? introTextAsset.text : "당신은 저택의 도우미입니다.";
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

    protected IEnumerator GetGPTResponse(string userMessage = null)
    {
        if (isRequestInProgress) yield break;
        isRequestInProgress = true;

        SetInputState(false);

        if (npcAnimator != null) npcAnimator.SetTrigger(talkTriggerName);

        Say("...", null);

        string finalSystemPrompt = BuildFinalSystemPrompt();

        LocalLlamaPayload payload = new LocalLlamaPayload 
        { 
            prompt = userMessage ?? "", 
            system = finalSystemPrompt 
        };
        string payloadJson = JsonConvert.SerializeObject(payload);

        using (UnityWebRequest request = new UnityWebRequest(localServerUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("서버로부터 받은 원본 데이터: " + request.downloadHandler.text);
    // ... 기존 코드
            }

            string chatbotResponse;
            if (request.result != UnityWebRequest.Result.Success)
            {
                chatbotResponse = "로컬 서버 오류: " + request.error;
            }
            else
            {
                LocalLlamaResponse responseData = JsonConvert.DeserializeObject<LocalLlamaResponse>(request.downloadHandler.text);
                chatbotResponse = responseData.response;

                if (!string.IsNullOrEmpty(userMessage))
                    chatHistory.Add(new OpenAIMessage { role = "user", content = userMessage });
                
                chatHistory.Add(new OpenAIMessage { role = "assistant", content = chatbotResponse });
            }

            isRequestInProgress = false;
            SetInputState(true);

            yield return StartCoroutine(HandleChatbotResponse(chatbotResponse));
        }
    }

    private void SetInputState(bool state)
    {
        if (targetInputField != null)
        {
            targetInputField.interactable = state;
            if (state) targetInputField.ActivateInputField();
        }
    }

    protected abstract string BuildFinalSystemPrompt();
    protected abstract IEnumerator HandleChatbotResponse(string responseMessage);
}