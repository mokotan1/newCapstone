using UnityEngine;

public class SwingMotion : MonoBehaviour
{
    public float swingAngle = 90f; // 휘두를 총 각도
    private float duration;
    private float elapsed = 0f;
    private Quaternion startRot;
    private Quaternion endRot;

    void Start()
    {
        // MiniGamePlayer에서 설정한 지속 시간을 가져옵니다.
        duration = 0.2f; // 기획안의 attackDuration 기준
        
        // 현재 각도를 기준으로 -45도에서 +45도까지 회전 설정
        startRot = transform.rotation * Quaternion.Euler(0, 0, -swingAngle / 2);
        endRot = transform.rotation * Quaternion.Euler(0, 0, swingAngle / 2);
        transform.rotation = startRot;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        // 시간에 따라 시작 각도에서 끝 각도로 부드럽게 회전
        transform.rotation = Quaternion.Lerp(startRot, endRot, elapsed / duration);
    }
}