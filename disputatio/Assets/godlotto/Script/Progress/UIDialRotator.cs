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

    [Header("Fixed circular drag speed")]
    [Min(1f)] public float fixedDegreesPerSecond = 120f;
    [Min(0f)] public float centerDeadZoneRadius = 8f;
    [Min(0f)] public float minimumPointerAngleDelta = 0.1f;

    [Header("Drag hit area")]
    [SerializeField] private RectTransform rotationCenter;
    [SerializeField] private RectTransform dragArea;
    [SerializeField] private RectTransform[] ignoredDragTargets;

    [Header("숫자 변경 완료 시 호출되는 이벤트 (마우스 뗐을 때)")]
    public UnityEvent<int> onDigitChanged;

    [Header("UI 숫자 표시용 (선택사항)")]
    public TMP_Text dialText;

    private RectTransform rect;
    private bool dragging;
    private Vector2 centerScreenPos;
    private Vector2 lastPointerScreenPos;
    private float totalRotation;
    private int currentDigit;
    private int finalDigit;

    private string dialKey;

    public static int ResolveDigitFromRotation(float rotationDegrees, float stepDegrees)
    {
        if (stepDegrees <= 0f)
            return 0;

        const int digitCount = 10;
        float halfStep = stepDegrees * 0.5f;
        float fullRange = stepDegrees * digitCount;
        float normalizedRotation = Mathf.Repeat(rotationDegrees, fullRange);

        if (normalizedRotation <= halfStep || normalizedRotation >= fullRange - halfStep)
            return 0;

        for (int digit = 1; digit < digitCount; digit++)
        {
            float center = digit * stepDegrees;
            if (normalizedRotation > center - halfStep && normalizedRotation <= center + halfStep)
                return digit;
        }

        return 0;
    }

    public static bool ShouldBeginDrag(bool pointerInsideDragArea, bool pointerInsideIgnoredTarget)
    {
        return pointerInsideDragArea && !pointerInsideIgnoredTarget;
    }

    public static Vector2 ResolveCenterScreenPosition(
        Vector2 dialCenterScreenPos,
        Vector2 configuredCenterScreenPos,
        bool hasConfiguredCenter)
    {
        return hasConfiguredCenter ? configuredCenterScreenPos : dialCenterScreenPos;
    }

    public static float ResolveRotationDeltaFromCircularDirectionDrag(
        Vector2 centerScreenPos,
        Vector2 previousPointerScreenPos,
        Vector2 currentPointerScreenPos,
        float fixedDegreesPerSecond,
        float sensitivity,
        float deltaTime,
        float centerDeadZoneRadius,
        float minimumPointerAngleDelta)
    {
        if (fixedDegreesPerSecond <= 0f || sensitivity <= 0f || deltaTime <= 0f)
            return 0f;

        Vector2 previousRadial = previousPointerScreenPos - centerScreenPos;
        Vector2 currentRadial = currentPointerScreenPos - centerScreenPos;
        float deadZoneSqr = centerDeadZoneRadius * centerDeadZoneRadius;
        if (previousRadial.sqrMagnitude <= deadZoneSqr || currentRadial.sqrMagnitude <= deadZoneSqr)
            return 0f;

        float previousAngle = Mathf.Atan2(previousRadial.y, previousRadial.x) * Mathf.Rad2Deg;
        float currentAngle = Mathf.Atan2(currentRadial.y, currentRadial.x) * Mathf.Rad2Deg;
        float pointerAngleDelta = Mathf.DeltaAngle(previousAngle, currentAngle);

        if (Mathf.Abs(pointerAngleDelta) < minimumPointerAngleDelta)
            return 0f;

        return Mathf.Sign(pointerAngleDelta) * fixedDegreesPerSecond * sensitivity * deltaTime;
    }

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
            Vector2 pointerScreenPos = Input.mousePosition;
            if (ShouldBeginDrag(IsPointerInsideDragArea(pointerScreenPos), IsPointerInsideIgnoredTarget(pointerScreenPos)))
            {
                dragging = true;
                centerScreenPos = GetCenterScreenPosition();
                lastPointerScreenPos = pointerScreenPos;
            }
        }

        if (dragging)
        {
            Vector2 pointerScreenPos = Input.mousePosition;
            float rotationDelta = ResolveRotationDeltaFromCircularDirectionDrag(
                centerScreenPos,
                lastPointerScreenPos,
                pointerScreenPos,
                fixedDegreesPerSecond,
                sensitivity,
                Time.deltaTime,
                centerDeadZoneRadius,
                minimumPointerAngleDelta);

            if (!Mathf.Approximately(rotationDelta, 0f))
            {
                totalRotation += rotationDelta;
                rect.localEulerAngles = new Vector3(0, 0, totalRotation);
                int newDigit = ResolveDigitFromRotation(totalRotation, stepDegrees);

                if (newDigit != currentDigit)
                {
                    currentDigit = newDigit;
                    if (dialText != null)
                    {
                        dialText.text = currentDigit.ToString();
                        dialText.ForceMeshUpdate();
                    }
                }
            }

            lastPointerScreenPos = pointerScreenPos;
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

    private bool IsPointerInsideDragArea(Vector2 pointerScreenPos)
    {
        RectTransform activeDragArea = dragArea != null ? dragArea : rect;
        return activeDragArea != null && activeDragArea.gameObject.activeInHierarchy &&
               RectTransformUtility.RectangleContainsScreenPoint(activeDragArea, pointerScreenPos);
    }

    private Vector2 GetCenterScreenPosition()
    {
        Vector2 dialCenter = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        bool hasRotationCenter = rotationCenter != null && rotationCenter.gameObject.activeInHierarchy;
        Vector2 configuredCenter = hasRotationCenter
            ? RectTransformUtility.WorldToScreenPoint(null, rotationCenter.position)
            : dialCenter;

        return ResolveCenterScreenPosition(dialCenter, configuredCenter, hasRotationCenter);
    }

    private bool IsPointerInsideIgnoredTarget(Vector2 pointerScreenPos)
    {
        if (ignoredDragTargets == null)
            return false;

        for (int i = 0; i < ignoredDragTargets.Length; i++)
        {
            RectTransform ignoredTarget = ignoredDragTargets[i];
            if (ignoredTarget != null &&
                ignoredTarget.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(ignoredTarget, pointerScreenPos))
            {
                return true;
            }
        }

        return false;
    }
}
