using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 사용하기 위해 필요
using UnityEngine.Events; // UnityEvent를 사용하기 위해 필요
using Fungus;
using System.Collections.Generic;

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
    [SerializeField] private bool presentChildPickupsOnUnlock = true;
    [SerializeField] private ItemPickup[] rewardPickups;
    [SerializeField] private float rewardFadeSeconds = 1f;
    bool solved;

    private int[] currentDigits;

    void Start()
    {
        currentDigits = new int[numberOfDigits];
        solved = flowchart.GetBooleanVariable("solved");
        GameLog.Log(solved.ToString());
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
            GameLog.Log("Success! Lock opened.");
            if (onUnlockSuccess != null)
            {
                onUnlockSuccess.Invoke(); // 성공 이벤트 실행
            }

            PresentUnlockRewards();
            if (flowchart != null)
                flowchart.SetBooleanVariable("solved", true);

            ClickInteractionCleanup.ResetAfterUiBoundary(flowchart);
            gameObject.SetActive(false); // 자물쇠 UI 끄기
            DeferredClickCleanup.Run(flowchart);
        }
        else
        {
            GameLog.Log("Failed. Try again.");
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

    private void PresentUnlockRewards()
    {
        HashSet<ItemPickup> pickups = new HashSet<ItemPickup>();

        if (rewardPickups != null)
        {
            foreach (ItemPickup pickup in rewardPickups)
            {
                if (pickup != null)
                    pickups.Add(pickup);
            }
        }

        if (presentChildPickupsOnUnlock)
        {
            foreach (ItemPickup pickup in GetComponentsInChildren<ItemPickup>(true))
            {
                if (pickup != null)
                    pickups.Add(pickup);
            }
        }

        foreach (ItemPickup pickup in pickups)
            PresentPickup(pickup);
    }

    private void PresentPickup(ItemPickup pickup)
    {
        if (pickup == null)
            return;

        GameObject rewardObject = pickup.gameObject;
        Transform rewardParent = transform.parent != null ? transform.parent : transform;
        rewardObject.transform.SetParent(rewardParent, false);
        rewardObject.transform.SetAsLastSibling();
        rewardObject.SetActive(true);

        RectTransform rectTransform = rewardObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
        else
        {
            rewardObject.transform.localPosition = Vector3.zero;
            rewardObject.transform.localScale = Vector3.one;
        }

        RewardFadePresenter presenter = rewardObject.GetComponent<RewardFadePresenter>();
        if (presenter == null)
            presenter = rewardObject.AddComponent<RewardFadePresenter>();

        presenter.Play(rewardFadeSeconds);
    }

}
