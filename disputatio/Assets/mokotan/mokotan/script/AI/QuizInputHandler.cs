using UnityEngine;
using TMPro; // TextMeshPro를 사용한다면
using Fungus;

public class QuizInputHandler : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField inputField; // Unity Inspector에서 Input Field를 여기에 연결합니다.
    [SerializeField] private GameObject inputPanel; // Input Field를 감싸는 부모 패널 (Input Field 자체일 수도 있습니다)

    [Header("Fungus Integration")]
    [SerializeField] private Flowchart targetFlowchart; // 이 스크립트가 속한 Flowchart 또는 제어할 Flowchart
    [SerializeField] private string fungusVariableName = "PlayerAnswer"; // Fungus String 변수 이름
    [SerializeField] private TutorChatbot tutorChatbot; // TutorChatbot 스크립트 인스턴스

    void Awake()
    {
        if (inputField == null)
        {
            // 같은 오브젝트에 TMP_InputField가 있으면 자동으로 찾기 시도
            inputField = GetComponent<TMP_InputField>();
        }
        if (inputPanel == null)
        {
            inputPanel = inputField.gameObject; // InputField 자체를 패널로 사용
        }

        if (inputField != null)
        {
            inputField.onEndEdit.AddListener(OnInputSubmit); // 입력 완료 시 이벤트 등록
            inputPanel.SetActive(false); // 시작 시 Input Field 비활성화
        }
        else
        {
            Debug.LogError("QuizInputHandler: InputField가 연결되지 않았습니다!");
        }
    }

    private void OnInputSubmit(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            // 빈 입력은 무시하거나, 메시지를 표시할 수 있습니다.
            Debug.Log("빈 답변은 전송되지 않습니다.");
            inputField.text = ""; // 입력 필드 초기화
            inputField.Select(); // 다시 포커스
            inputField.ActivateInputField(); // 다시 활성화
            return;
        }

        // Fungus String 변수에 플레이어의 답변 저장
        if (targetFlowchart != null)
        {
            targetFlowchart.SetStringVariable(fungusVariableName, inputText);
            Debug.Log($"Fungus variable '{fungusVariableName}' set to: {inputText}");
        }
        else
        {
            Debug.LogError("QuizInputHandler: Flowchart가 연결되지 않았습니다.");
        }

        // 입력 필드 초기화 및 비활성화
        inputField.text = "";

        // TutorChatbot에 플레이어의 답변을 보냅니다.
        if (tutorChatbot != null)
        {
            // AI에게 플레이어 답변을 chatHistory에 추가하고 GPT 응답을 요청합니다.
            // 이 부분을 TutorChatbot의 새 함수로 만들 수도 있습니다.
            tutorChatbot.AddPlayerMessageAndGetResponse(inputText); // 새 함수를 만들었다고 가정
        }
        else
        {
            Debug.LogError("QuizInputHandler: TutorChatbot이 연결되지 않았습니다.");
        }
    }

    /// <summary>
    /// Fungus에서 호출하여 Input Field를 활성화하는 함수
    /// </summary>
    public void ActivateQuizInputField()
    {
        if (inputPanel != null)
        {
            inputPanel.SetActive(true);
            inputField.Select(); // Input Field에 포커스 주기
            inputField.ActivateInputField(); // 입력을 받을 수 있도록 활성화
            Debug.Log("Quiz Input Field 활성화.");
        }
        else
        {
            Debug.LogError("QuizInputHandler: Input Panel이 연결되지 않았습니다!");
        }
    }
}