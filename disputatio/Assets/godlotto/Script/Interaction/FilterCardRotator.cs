using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class FilterCardRotator : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    // 플레이어가 인지하는 각도를 저장할 변수 (0, 90, 180, 270)
    private float currentAngle = 0f;

    [SerializeField] private float buttonRotateStepDegrees = 90f;
    [SerializeField] private float fineRotateStepDegrees = 5f;
    [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
    [SerializeField] private KeyCode rotateRightKey = KeyCode.E;

    private bool pointerInside;

    /// <summary>UI에 적용된 Z축 시각 각도(0~360).</summary>
    public float CurrentVisualAngleDegrees => NormalizeVisualAngle(-currentAngle);

    /// <summary>회전 버튼으로 90° 회전이 적용된 뒤 호출된다.</summary>
    public event Action Rotated;

    private void Update()
    {
        if (!pointerInside)
            return;

        if (Input.GetKeyDown(rotateLeftKey))
            RotateVisualBy(-fineRotateStepDegrees);
        else if (Input.GetKeyDown(rotateRightKey))
            RotateVisualBy(fineRotateStepDegrees);
    }

    // 오른쪽으로 90도 회전시키는 함수
    public void RotateRight()
    {
        RotateVisualBy(buttonRotateStepDegrees);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData == null || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            return;

        RotateVisualBy(Mathf.Sign(eventData.scrollDelta.y) * fineRotateStepDegrees);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    public void RotateVisualBy(float deltaDegrees)
    {
        float visualAngle = NormalizeVisualAngle(CurrentVisualAngleDegrees + deltaDegrees);
        currentAngle = NormalizeVisualAngle(-visualAngle);
        ApplyRotation();
    }

    public void ConfigureRotationSteps(float buttonStepDegrees, float fineStepDegrees)
    {
        buttonRotateStepDegrees = Mathf.Max(0.1f, Mathf.Abs(buttonStepDegrees));
        fineRotateStepDegrees = Mathf.Max(0.1f, Mathf.Abs(fineStepDegrees));
    }

    // 왼쪽으로 90도 회전시키는 함수
    public void RotateLeft()
    {
        RotateVisualBy(-buttonRotateStepDegrees);
    }

    // 실제 회전값을 적용하는 함수
    private void ApplyRotation()
    {
        // 유니티의 Z축 회전은 반시계가 양수(+)이므로,
        // 우리가 원하는 시계 방향 회전을 위해서는 currentAngle에 음수(-)를 붙여줍니다.
        transform.localRotation = Quaternion.Euler(0, 0, -currentAngle);
        Rotated?.Invoke();
    }

    static float NormalizeVisualAngle(float angleDegrees)
    {
        float normalized = angleDegrees % 360f;
        if (normalized < 0f)
            normalized += 360f;

        return normalized;
    }
}
