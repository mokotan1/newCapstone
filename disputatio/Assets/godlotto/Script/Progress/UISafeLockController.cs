using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Fungus;

public class UISafeLockController : MonoBehaviour
{
    [Header("다이얼 (1개)")]
    public UIDialRotator dial;

    [Header("정답 순서 — 왼쪽부터 차례대로 입력")]
    public int[] targets = { 3, 0, 5 };

    [Header("단계 표시 UI (선택)")]
    public TMP_Text stepIndicatorText;
    public TMP_Text feedbackText;

    [Header("Fungus 연동 (선택)")]
    public Flowchart flowchart;
    public string allVar = "safe_all_ok";

    [Header("UI 토글 이미지")]
    public GameObject closedImage;
    public GameObject openImage;

    [Header("이벤트")]
    public UnityEvent onUnlock;
    public UnityEvent onStepCorrect;
    public UnityEvent onWrong;

    private int currentStep;
    private int currentDialValue;
    private bool unlocked;

    private const string UnlockKey = "SafeLock_Unlocked";

#if UNITY_EDITOR
    [Header("Editor only")]
    [SerializeField] private bool editorResetOnPlay;
#endif

    private void Start()
    {
#if UNITY_EDITOR
        if (editorResetOnPlay)
        {
            PlayerPrefs.DeleteKey(UnlockKey);
            PlayerPrefs.Save();
            Debug.LogWarning("[Safe] 에디터 자동 초기화 완료");
        }
#endif
        unlocked = PlayerPrefs.GetInt(UnlockKey, 0) == 1;
        currentStep = 0;
        RefreshStepUI();

        if (unlocked)
            GameLog.Log("[Safe] 이미 열린 상태");
    }

    // UIDialRotator.onDigitChanged 에 인스펙터에서 연결
    public void OnDialChanged(int val)
    {
        currentDialValue = val;
        Debug.LogWarning($"[Safe] OnDialChanged 호출됨: val={val}");
    }

    // 확인 버튼 onClick 에 연결
    public void Confirm()
    {
        Debug.LogWarning($"[Safe] Confirm 호출됨: unlocked={unlocked}, step={currentStep}, dialVal={currentDialValue}, target={targets[currentStep]}");
        if (unlocked) return;

        if (currentDialValue == targets[currentStep])
        {
            currentStep++;
            onStepCorrect?.Invoke();
            GameLog.Log($"[Safe] {currentStep}/{targets.Length} 단계 성공");

            if (currentStep >= targets.Length)
                Unlock();
            else
                RefreshStepUI();
        }
        else
        {
            GameLog.Log($"[Safe] 틀림 — {currentDialValue} (정답 {targets[currentStep]}), 처음부터 다시");
            currentStep = 0;
            onWrong?.Invoke();
            RefreshStepUI();
        }
    }

    private void Unlock()
    {
        unlocked = true;
        PlayerPrefs.SetInt(UnlockKey, 1);
        PlayerPrefs.Save();

        if (flowchart != null)
            flowchart.SetBooleanVariable(allVar, true);

        if (closedImage != null) closedImage.SetActive(false);
        if (openImage != null)   openImage.SetActive(true);

        onUnlock?.Invoke();
        GameLog.Log("[Safe] 금고 열림!");
    }

    private void RefreshStepUI()
    {
        if (stepIndicatorText != null)
            stepIndicatorText.text = $"{currentStep + 1}  /  {targets.Length}";

        if (feedbackText != null)
            feedbackText.text = currentStep == 0
                ? ""
                : $"✓  {currentStep}번 맞음";
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 20;
        style.normal.textColor = Color.yellow;
        GUI.Box(new Rect(10, 10, 220, 40),
            $"[Safe] 단계: {currentStep + 1} / {targets.Length}  (입력값: {currentDialValue})",
            style);
    }
#endif

    // 에디터에서 금고 상태 초기화용
    [ContextMenu("금고 초기화 (PlayerPrefs 삭제)")]
    public void EditorResetLock()
    {
        PlayerPrefs.DeleteKey(UnlockKey);
        PlayerPrefs.Save();
        unlocked = false;
        currentStep = 0;
        RefreshStepUI();
        GameLog.Log("[Safe] 금고 초기화됨");
    }
}
