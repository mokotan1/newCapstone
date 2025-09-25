using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 사용하기 위해 필요
using UnityEngine.Events; // UnityEvent를 사용하기 위해 필요
using Fungus;

public class CombinationLock : MonoBehaviour
{
    [Header("Settings")]
    public string correctAnswer = "1234"; // 정답 비밀번호
    public int numberOfDigits = 4; // 자릿수

    [Header("UI References")]
    public TextMeshProUGUI[] digitDisplays; // 4개의 숫자 텍스트

    [Header("Events")]
    public UnityEvent onUnlockSuccess; // 정답일 때 실행될 이벤트
    public UnityEvent onUnlockFail;    // 오답일 때 실행될 이벤트
    [SerializeField]
    public Flowchart flowchart;
    bool solved;

    private int[] currentDigits;

    void Start()
    {
        currentDigits = new int[numberOfDigits];
        solved = flowchart.GetBooleanVariable("solved");
        Debug.Log(solved);
        UpdateDisplay();
    }

    // 특정 자릿수의 숫자를 올리는 함수 (위쪽 버튼에 연결)
    public void IncrementDigit(int digitIndex)
    {
        currentDigits[digitIndex]++;
        if (currentDigits[digitIndex] > 9)
        {
            currentDigits[digitIndex] = 0; // 9 다음은 0으로 순환
        }
        UpdateDisplay();
    }

    // 특정 자릿수의 숫자를 내리는 함수 (아래쪽 버튼에 연결)
    public void DecrementDigit(int digitIndex)
    {
        currentDigits[digitIndex]--;
        if (currentDigits[digitIndex] < 0)
        {
            currentDigits[digitIndex] = 9; // 0 이전은 9로 순환
        }
        UpdateDisplay();
    }

    // 정답을 확인하는 함수 (확인 버튼에 연결)
    public void CheckAnswer()
    {
        string currentInput = "";
        foreach (int digit in currentDigits)
        {
            currentInput += digit.ToString();
        }

        if (currentInput == correctAnswer)
        {
            Debug.Log("Success! Lock opened.");
            if (onUnlockSuccess != null)
            {
                onUnlockSuccess.Invoke(); // 성공 이벤트 실행
                flowchart.SetBooleanVariable("solved", true);
            }
            gameObject.SetActive(false); // 자물쇠 UI 끄기
        }
        else
        {
            Debug.Log("Failed. Try again.");
            if (onUnlockFail != null)
            {
                onUnlockFail.Invoke(); // 실패 이벤트 실행 (예: '철컥' 소리)
            }
        }
    }

    // 화면의 숫자 텍스트를 업데이트하는 함수
    private void UpdateDisplay()
    {
        for (int i = 0; i < numberOfDigits; i++)
        {
            digitDisplays[i].text = currentDigits[i].ToString();
        }
    }
}