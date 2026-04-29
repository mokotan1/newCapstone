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
    [Header("Editor only")]
    [Tooltip("플레이 시 이 오브젝트의 다이얼 PlayerPrefs 키만 삭제합니다. 전역 DeleteAll은 사용하지 않습니다.")]
    [SerializeField] private bool editorResetThisDialPrefsOnPlay;
#endif

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        dialKey = $"Dial_{gameObject.name}_Value"; // 예: Dial_L, Dial_M, Dial_R

#if UNITY_EDITOR
        if (editorResetThisDialPrefsOnPlay)
        {
            PlayerPrefs.DeleteKey(dialKey);
            PlayerPrefs.Save();
        }
#endif
    }

    private void OnEnable()
    {
        // 이전 값 복원
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

                PlayerPrefs.SetInt(dialKey, finalDigit);
                PlayerPrefs.Save();

                onDigitChanged?.Invoke(finalDigit);
            }
        }
    }
}
