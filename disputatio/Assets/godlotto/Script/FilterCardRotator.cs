using UnityEngine;
using TMPro;

public class FilterCardRotator : MonoBehaviour
{
    [Header("UI Components")]
    public TextMeshProUGUI angleText;

    private float currentZRotation = 0f;

    void Start()
    {
        // 시작 시 각도 텍스트를 숨깁니다.
        if (angleText != null)
        {
            angleText.gameObject.SetActive(false);
        }
    }

    // 오른쪽으로 90도 회전시키는 함수
    public void RotateRight()
    {
        // 시계 방향으로 90도를 더합니다.
        currentZRotation += 90f;

        // 만약 각도가 360도 이상이 되면 0으로 되돌립니다.
        if (currentZRotation >= 360f)
        {
            currentZRotation = 0f;
        }

        transform.localRotation = Quaternion.Euler(0, 0, currentZRotation);
        UpdateAngleText(); // 회전 후 텍스트 업데이트
    }

    // 왼쪽으로 90도 회전시키는 함수
    public void RotateLeft()
    {
        // 반시계 방향으로 90도를 뺍니다.
        currentZRotation -= 90f;

        // 만약 각도가 0도보다 작아지면 270도로 순환시킵니다.
        if (currentZRotation < 0f)
        {
            currentZRotation = 270f;
        }

        transform.localRotation = Quaternion.Euler(0, 0, currentZRotation);
        UpdateAngleText(); // 회전 후 텍스트 업데이트
    }

    // (이 아래는 기존 코드와 동일합니다)

    private void UpdateAngleText()
    {
        if (angleText != null && angleText.gameObject.activeSelf)
        {
            angleText.text = currentZRotation + "°";
        }
    }

    public void ShowAngleText()
    {
        if (angleText != null)
        {
            angleText.gameObject.SetActive(true);
            UpdateAngleText();
        }
    }

    public void HideAngleText()
    {
        if (angleText != null)
        {
            angleText.gameObject.SetActive(false);
        }
    }
}