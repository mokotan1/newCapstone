using System;
using UnityEngine;

public class FilterCardRotator : MonoBehaviour
{
    // 플레이어가 인지하는 각도를 저장할 변수 (0, 90, 180, 270)
    private float currentAngle = 0f;

    /// <summary>UI에 적용된 Z축 시각 각도(0~360).</summary>
    public float CurrentVisualAngleDegrees => NormalizeVisualAngle(-currentAngle);

    /// <summary>회전 버튼으로 90° 회전이 적용된 뒤 호출된다.</summary>
    public event Action Rotated;

    // 오른쪽으로 90도 회전시키는 함수
    public void RotateRight()
    {
        // 사용자가 인지하는 각도는 90도씩 더해줍니다.
        currentAngle += 90f;

        // 360도가 되면 0으로 되돌립니다.
        if (currentAngle >= 360f)
        {
            currentAngle = 0f;
        }

        // 실제 회전을 적용합니다. 시계 방향으로 돌리기 위해 음수(-) 값을 사용합니다.
        ApplyRotation();
    }

    // 왼쪽으로 90도 회전시키는 함수
    public void RotateLeft()
    {
        // 사용자가 인지하는 각도는 90도씩 빼줍니다.
        currentAngle -= 90f;

        // 0도보다 작아지면 270도로 순환시킵니다.
        if (currentAngle < 0f)
        {
            currentAngle = 270f;
        }

        // 실제 회전을 적용합니다.
        ApplyRotation();
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