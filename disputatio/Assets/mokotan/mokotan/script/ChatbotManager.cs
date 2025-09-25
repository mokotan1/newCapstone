using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Fungus;
using System;
using Newtonsoft.Json;
using TMPro; // [수정] TextMeshPro 네임스페이스 추가

[System.Serializable]
public class GeminiContent
{
    public string text;
}

[System.Serializable]
public class GeminiMessage
{
    public string role;
    public List<GeminiContent> parts;
}

[System.Serializable]
public class GeminiPayload
{
    public List<GeminiMessage> contents;
}


[System.Serializable]
public class GeminiCandidate
{
    public GeminiMessage content;
}

[System.Serializable]
public class GeminiResponse
{
    public List<GeminiCandidate> candidates;
}

public class ChatbotManager : MonoBehaviour
{
    // Fungus의 SayDialog를 참조하기 위한 변수
    [SerializeField]
    private SayDialog chatSayDialog;

    // 유저 입력 필드 및 관련 UI를 참조하기 위한 변수
    [SerializeField]
    private TMP_InputField userInputField; // [수정] InputField를 TMP_InputField로 변경
    
    [SerializeField]
    private Button sendButton;

    // 유저 입력 패널
    [SerializeField]
    private GameObject inputPanel;

    // 대사를 일정 길이로 자르기 위한 변수 (유니티 인스펙터에서 조절 가능)
    [SerializeField]
    private int dialogueChunkSize = 150;
    
    // API 키와 URL (보안을 위해 실제 프로젝트에서는 더 안전한 곳에 보관하세요)
    private const string API_KEY = "AIzaSyCtDnwmH_m99jmzUE8QB3xShfi8XtkNJSQ"; // 실제 API 키로 교체해야 합니다.
    private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash-latest:generateContent?key=";

    [SerializeField]
    public Flowchart globalFlowchart;
    // Gemini API 호출을 위한 메시지 리스트
    private List<GeminiMessage> chatHistory = new List<GeminiMessage>();

    // API 요청이 진행 중인지 여부
    private bool isRequestInProgress = false;

    // Start() 함수는 게임 오브젝트가 활성화될 때 한 번 호출됩니다.
    private void Start()
    {
        // 버튼 클릭 이벤트에 함수 연결
        sendButton.onClick.AddListener(SendMessageToChatbot);
        InitializeChatHistory();
    }

    private void Update()
    {
        // 입력 필드가 활성화된 상태에서 Enter 키를 누르면 메시지 전송
        if (userInputField.isFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SendMessageToChatbot();
        }
    }

    // 게임 시작 시 초기 대화를 설정하는 함수
   private void InitializeChatHistory()
    {
        chatHistory.Clear();
        // 리소스에서 기본 프롬프트 텍스트를 불러옵니다.
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt");
        string basePrompt = "";
        
        if (introTextAsset != null)
        {
            basePrompt = introTextAsset.text;
        }
        else
        {
            Debug.LogError("introPrompt.txt 파일을 찾을 수 없습니다. Resources 폴더에 파일을 추가해주세요.");
            basePrompt = "초기 프롬프트 파일을 찾을 수 없습니다.";
        }

        // [삭제] GetBottle 변수를 확인하고 프롬프트를 수정하는 로직은
        // 이제 GetGeminiResponse 함수가 담당하므로 여기서는 필요 없습니다.

        // 챗봇의 기본 역할(페르소나)만 대화 기록에 추가합니다.
        chatHistory.Add(new GeminiMessage { role = "model", parts = new List<GeminiContent> { new GeminiContent { text = basePrompt } } });
    }

    // 유저가 메시지를 전송할 때 호출되는 함수
    private void SendMessageToChatbot()
    {
        if (isRequestInProgress)
        {
            Debug.Log("API 요청이 진행 중입니다. 잠시 기다려 주세요.");
            return;
        }

        string userMessage = userInputField.text.Trim();

        if (string.IsNullOrEmpty(userMessage))
        {
            return;
        }
        
        // 입력 필드 초기화
        userInputField.text = "";

        // 대화 기록에 사용자의 메시지 추가
        chatHistory.Add(new GeminiMessage { role = "user", parts = new List<GeminiContent> { new GeminiContent { text = userMessage } } });

        // API 호출을 위한 코루틴 시작
        StartCoroutine(GetGeminiResponse());
    }

    // Gemini API로부터 응답을 받는 코루틴
    private IEnumerator GetGeminiResponse()
    {
        isRequestInProgress = true;
        Debug.Log("API 요청 시작: isRequestInProgress = " + isRequestInProgress);

        // 챗봇 응답을 로딩 중임을 표시
        Say("...", null);

         // 1. 기본 프롬프트를 불러옵니다.
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt");
        string finalPrompt = introTextAsset != null ? introTextAsset.text : "You are a helpful assistant.";

        // 2. 가장 최근의 사용자 메시지를 가져옵니다.
        string lastUserMessage = "";
        if (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].role == "user")
        {
            lastUserMessage = chatHistory[chatHistory.Count - 1].parts[0].text;
        }

        // 3. Fungus 변수와 사용자 메시지 키워드를 확인하여 프롬프트를 조립합니다.
        if (globalFlowchart != null)
        {
            bool hasBottleFlag = globalFlowchart.GetBooleanVariable("GetBottle"); // 예시 변수 이름

            // 물병 플래그가 True이고, 사용자가 "물병"에 대해 물어봤을 경우
            if (hasBottleFlag && lastUserMessage.Contains("물병") || lastUserMessage.Contains("병"))
            {
                // 프롬프트에 수수께끼를 내라는 특별 지시를 추가합니다.
                finalPrompt += "\n\n[중요 지시] 플레이어는 '물병'에 대한 단서를 가지고 있으며, '물병'에 대해 질문했습니다. 부력에 대한 힌트로 수수께끼로 힌트를 주세요.";
            }
            // 여기에 다른 아이템에 대한 Else If 조건을 계속 추가할 수 있습니다.
            // else if (hasKeyFlag && lastUserMessage.Contains("문")) { ... }
        }
        else
        {
            Debug.LogError("Global Flowchart가 ChatbotManager에 연결되지 않았습니다!");
        }

        // --- 프롬프트 생성 로직 끝 ---

        // 최종 조립된 프롬프트를 포함하여 페이로드 생성
        GeminiPayload payload = new GeminiPayload
        {
            contents = chatHistory
        };
        // 참고: Gemini API는 systemInstruction을 지원하지만,
        // 여기서는 대화 흐름에 직접 지시를 추가하는 방식으로 구현합니다.
        // 만약 systemInstruction을 사용하려면 payload 생성 로직을 수정해야 합니다.

        string payloadJson = JsonConvert.SerializeObject(payload);


        int retryCount = 0;
        float delay = 1.0f;
        UnityWebRequest request = null;

        while (retryCount < 3)
        {
            request = new UnityWebRequest(API_URL + API_KEY, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payloadJson);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.responseCode == 429)
            {
                Debug.LogWarning("API rate limit exceeded. Retrying in " + delay + " seconds...");
                yield return new WaitForSeconds(delay);
                delay *= 2f; // 지수 백오프 적용
                retryCount++;
            }
            else
            {
                break; // 429 외의 다른 응답을 받으면 루프를 종료
            }
        }
        
        string chatbotResponse = string.Empty;

        try
        {
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + request.error + " | Response: " + request.downloadHandler.text);
                chatbotResponse = "API 요청 중 오류가 발생했습니다.";
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("Received JSON Response: " + jsonResponse);
                chatbotResponse = ParseGeminiResponse(jsonResponse);
                
                chatHistory.Add(new GeminiMessage { role = "model", parts = new List<GeminiContent> { new GeminiContent { text = chatbotResponse } } });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("API 요청 중 예외 발생: " + e.Message);
            chatbotResponse = "API 요청 중 예상치 못한 오류가 발생했습니다.";
        }
        finally
        {
            isRequestInProgress = false;
            Debug.Log("API 요청 완료: isRequestInProgress = " + isRequestInProgress);
        }
        
        // API 응답을 받은 후, Fungus가 대사를 표시할 때까지 기다림
        yield return StartCoroutine(DisplayMessageChunks(chatbotResponse, true));
    }

    private IEnumerator DisplayMessageChunks(string message, bool isUserTurn = false)
    {
        // 메시지를 문장 단위로 자름
        List<string> chunks = new List<string>();
        int currentIndex = 0;
        while (currentIndex < message.Length)
        {
            int endIndex = currentIndex + dialogueChunkSize;
            if (endIndex >= message.Length)
            {
                chunks.Add(message.Substring(currentIndex));
                break;
            }
            
            // 문장이 끝나는 부분을 찾음
            int sentenceEndIndex = message.LastIndexOfAny(new char[] { '.', '?', '!', '…' }, endIndex, dialogueChunkSize);
            
            if (sentenceEndIndex > currentIndex)
            {
                chunks.Add(message.Substring(currentIndex, sentenceEndIndex - currentIndex + 1).Trim());
                currentIndex = sentenceEndIndex + 1;
            }
            else
            {
                // 문장 끝을 찾지 못하면 기존처럼 글자 수로 나눔
                chunks.Add(message.Substring(currentIndex, dialogueChunkSize).Trim());
                currentIndex = endIndex;
            }
        }
        
        // 각 조각을 순서대로 표시
        foreach (var chunk in chunks)
        {
            bool isComplete = false;
            Say(chunk, () => isComplete = true);
            while (!isComplete)
            {
                yield return null;
            }
        }
        
        // 모든 대사 표시가 끝나면 입력 패널을 활성화
        SetInputActive(true);
    }
    
    // Gemini API 응답을 파싱하는 함수
    private string ParseGeminiResponse(string json)
    {
        try
        {
            GeminiResponse response = JsonConvert.DeserializeObject<GeminiResponse>(json);
            
            if (response != null && response.candidates != null && response.candidates.Count > 0)
            {
                if (response.candidates[0].content != null && response.candidates[0].content.parts != null && response.candidates[0].content.parts.Count > 0)
                {
                    return response.candidates[0].content.parts[0].text;
                }
            }
            return "죄송합니다. 답변을 파싱하는 데 실패했습니다.";
        }
        catch (JsonSerializationException e)
        {
            Debug.LogError("JSON 파싱 오류: " + e.Message + " (원본 JSON: " + json + ")");
            return "JSON 파싱 중 오류가 발생했습니다.";
        }
    }

    private void Say(string message, System.Action onComplete = null)
    {
        if (chatSayDialog != null)
        {
            if (!chatSayDialog.gameObject.activeInHierarchy)
            {
                chatSayDialog.gameObject.SetActive(true);
            }
            chatSayDialog.Say(message, true, true, false, true, true, null, onComplete);
        }
    }

    public void SetInputActive(bool isActive)
    {
        if (inputPanel != null)
        {
            inputPanel.SetActive(isActive);
        }
        if (isActive)
        {
            userInputField.Select();
            userInputField.ActivateInputField();
        }
    }
}