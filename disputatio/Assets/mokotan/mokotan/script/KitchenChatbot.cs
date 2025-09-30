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
[System.Serializable]
public class OpenAIMessage { public string role; public string content; }
[System.Serializable]
public class OpenAIPayload { public string model; public List<OpenAIMessage> messages; }
[System.Serializable]
public class OpenAIChoice { public OpenAIMessage message; }
[System.Serializable]
public class OpenAIResponse { public List<OpenAIChoice> choices; }

public class KitchenChatbot : MonoBehaviour
{
    // --- Unity & Fungus 연결 변수 ---
    [SerializeField] private SayDialog chatSayDialog;
    [SerializeField] public Flowchart kitchenFlowchart; // 주방 flowchart만 참조
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
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt_kitchen"); // 주방용 프롬프트
        string basePrompt = introTextAsset != null ? introTextAsset.text : "You are a helpful kitchen assistant.";
        chatHistory.Add(new OpenAIMessage { role = "system", content = basePrompt });
    }

    public void TriggerAIResponseByFlag()
    {
        if (isRequestInProgress) return;
        string actionText = "(플레이어가 나에게 음식을 주었다.)";
        chatHistory.Add(new OpenAIMessage { role = "user", content = actionText });
        StartCoroutine(GetGPTResponse());
    }

    private IEnumerator GetGPTResponse()
    {
        isRequestInProgress = true;
        Say("...", null);

        string finalSystemPrompt = chatHistory[0].content;
        
        // --- 주방 관련 로직만 남김 ---
        if (kitchenFlowchart != null)
        {
            bool giveFood = kitchenFlowchart.GetBooleanVariable("giveFood");
            if (giveFood)
            {
                finalSystemPrompt += "\n\n[중요 지시] 플레이어는 당신에게 먹이를 주었습니다. 그에 대한 감사로 '카레'에 대한 힌트를 수수께끼로 내주세요.";
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
        // ... (대사 나누기 로직은 기존과 동일)
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