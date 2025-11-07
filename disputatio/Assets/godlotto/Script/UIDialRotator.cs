using UnityEngine;
using UnityEngine.Events;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class UIDialRotator : MonoBehaviour
{
    [Header("한 칸 회전 각도 (기본 36° = 10단계)")]
    public float stepDegrees = 36f;

    [Header("감도 (값이 높을수록 빠름)")]
    [Range(0.1f, 5f)] public float sensitivity = 1.8f;

    [Header("숫자 변경 완료 시 호출되는 이벤트 (마우스 뗐을 때)")]
    public UnityEvent<int> onDigitChanged;

    [Header("UI 숫자 표시용 (선택사항)")]
    public TMP_Text dialText;

    private RectTransform rect;
    private bool dragging;
    private Vector2 centerScreenPos;
    private float lastAngle;
    private float totalRotation;
    private int currentDigit;
    private int finalDigit;

    private string dialKey;

#if UNITY_EDITOR
    private static bool editorInitialized = false;
#endif

    // ✅ 에디터에서 Play 버튼 눌렀을 때 딱 한 번만 초기화
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetPrefsOnPlay()
    {
        if (!editorInitialized)
        {
            Debug.Log("🧹 [UIDialRotator] 에디터 Play 시작 — 다이얼 값 초기화됨 (0 0 0)");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            editorInitialized = true;
        }
    }
#endif

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        dialKey = $"Dial_{gameObject.name}_Value"; // 예: Dial_L, Dial_M, Dial_R
    }

    private void OnEnable()
    {
        var controller = FindObjectOfType<UISafeLockController>();
        if (controller != null)
        {
            if (gameObject.name.Contains("L"))
                onDigitChanged.AddListener(controller.OnLeftChanged);
            else if (gameObject.name.Contains("M"))
                onDigitChanged.AddListener(controller.OnMiddleChanged);
            else if (gameObject.name.Contains("R"))
                onDigitChanged.AddListener(controller.OnRightChanged);
        }

        // 🔹 이전 값 복원
        int saved = PlayerPrefs.GetInt(dialKey, 0);
        currentDigit = saved;
        finalDigit = saved;
        totalRotation = saved * stepDegrees;
        rect.localEulerAngles = new Vector3(0, 0, totalRotation);

        if (dialText != null)
        {
            dialText.text = saved.ToString();
            dialText.ForceMeshUpdate();
        }

        onDigitChanged?.Invoke(saved);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition))
            {
                dragging = true;
                centerScreenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
                Vector2 dir = (Vector2)Input.mousePosition - centerScreenPos;
                lastAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }
        }

        if (dragging)
        {
            Vector2 dir = (Vector2)Input.mousePosition - centerScreenPos;
            float newAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float deltaAngle = Mathf.DeltaAngle(lastAngle, newAngle) * sensitivity;

            totalRotation -= deltaAngle;
            rect.localEulerAngles = new Vector3(0, 0, totalRotation);

            int newDigit = Mathf.FloorToInt(((totalRotation / stepDegrees) % 10f + 10f) % 10f);
            if (newDigit != currentDigit)
            {
                currentDigit = newDigit;
                if (dialText != null)
                {
                    dialText.text = currentDigit.ToString();
                    dialText.ForceMeshUpdate();
                }
            }

            lastAngle = newAngle;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (dragging)
            {
                dragging = false;
                finalDigit = currentDigit;
                Debug.Log($"🟢 [UIDialRotator] {gameObject.name} 최종 숫자: {finalDigit}");

                PlayerPrefs.SetInt(dialKey, finalDigit);
                PlayerPrefs.Save();

                onDigitChanged?.Invoke(finalDigit);
            }
        }
    }
}
