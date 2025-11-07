using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Fungus;

public class UISafeLockController : MonoBehaviour
{
    [Header("다이얼 3개 연결")]
    public UIDialRotator dialL;
    public UIDialRotator dialM;
    public UIDialRotator dialR;

    [Header("숫자 텍스트 표시용")]
    public TMP_Text textL;
    public TMP_Text textM;
    public TMP_Text textR;

    [Header("정답 조합 (예: 3-0-5)")]
    public int leftTarget = 3;
    public int middleTarget = 0;
    public int rightTarget = 5;

    [Header("Fungus 연동 (선택)")]
    public Flowchart flowchart;
    public string leftVar = "safeL_ok";
    public string middleVar = "safeM_ok";
    public string rightVar = "safeR_ok";
    public string allVar = "safe_all_ok";

    [Header("UI 토글 이미지")]
    public GameObject closedImage;
    public GameObject openImage;

    [Header("금고가 열릴 때 호출되는 이벤트")]
    public UnityEvent onUnlock;

    private bool lOK, mOK, rOK, unlocked;
    private const string unlockKey = "SafeLock_Unlocked"; // PlayerPrefs 저장용 키

    private void Start()
    {
        // 🔹 이전에 이미 금고가 열린 상태인지 확인
        unlocked = PlayerPrefs.GetInt(unlockKey, 0) == 1;
        if (unlocked)
        {
            Debug.Log("🔓 금고는 이미 열린 상태 — Fungus 블록은 실행되지 않음");
        }
    }

    public void OnLeftChanged(int val)
    {
        if (textL) textL.text = val.ToString();
        lOK = (val == leftTarget);
        SetFungusBool(leftVar, lOK);
        TryUnlock();
    }

    public void OnMiddleChanged(int val)
    {
        if (textM) textM.text = val.ToString();
        mOK = (val == middleTarget);
        SetFungusBool(middleVar, mOK);
        TryUnlock();
    }

    public void OnRightChanged(int val)
    {
        if (textR) textR.text = val.ToString();
        rOK = (val == rightTarget);
        SetFungusBool(rightVar, rOK);
        TryUnlock();
    }

    private void TryUnlock()
    {
        // 🔸 이미 열린 상태라면 다시 블록 실행 안 함
        if (unlocked) return;

        if (lOK && mOK && rOK)
        {
            unlocked = true;
            PlayerPrefs.SetInt(unlockKey, 1);
            PlayerPrefs.Save();

            SetFungusBool(allVar, true);
            OnUnlock();
        }
    }

    // 🔓 금고 해제 처리
    public void OnUnlock()
    {
        onUnlock?.Invoke(); // Fungus 블록 실행은 여기 이벤트로 연결
        Debug.Log("🔓 금고가 열렸습니다!");
    }

    private void SetFungusBool(string name, bool value)
    {
        if (flowchart == null) return;
        flowchart.SetBooleanVariable(name, value);
    }
}
