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
        TextAsset introTextAsset = Resources.Load<TextAsset>("introPrompt");
        string introPrompt = "";
        
        if (introTextAsset != null)
        {
            introPrompt = introTextAsset.text;
        }
        else
        {
            Debug.LogError("introPrompt.txt 파일을 찾을 수 없습니다. Resources 폴더에 파일을 추가해주세요.");
            introPrompt = "초기 프롬프트 파일을 찾을 수 없습니다.";
        }
        
        // 프롬프트를 대화 기록에 추가
        chatHistory.Add(new GeminiMessage { role = "user", parts = new List<GeminiContent> { new GeminiContent { text = introPrompt } } });

        // 바로 사용자 입력창을 활성화
        SetInputActive(true);
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

        // 대화 기록을 기반으로 페이로드 생성
        GeminiPayload payload = new GeminiPayload
        {
            contents = chatHistory
        };
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