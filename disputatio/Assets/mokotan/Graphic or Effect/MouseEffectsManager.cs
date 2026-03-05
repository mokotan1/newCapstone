using UnityEngine;

public class MouseEffectsManager : MonoBehaviour
{
    [Header("클릭 이펙트")]
    public GameObject clickEffectPrefab; // 2단계에서 만든 'ClickEffect' 프리팹

    [Header("마우스 꼬리")]
    public Transform mouseTrailObject;   // 1단계에서 만든 'MouseTrail' 오브젝트

    private TrailRenderer trailRenderer; // 꼬리 컴포넌트를 제어할 변수
    private Camera mainCamera;

    void Start()
    {
        // 메인 카메라를 찾아옴
        mainCamera = Camera.main;

        // 'MouseTrail' 오브젝트에서 TrailRenderer 컴포넌트를 가져옴
        if (mouseTrailObject != null)
        {
            trailRenderer = mouseTrailObject.GetComponent<TrailRenderer>();
            
            // 시작할 때는 꼬리를 꺼둠
            trailRenderer.emitting = false;
        }
    }

    void Update()
    {
        // 1. 마우스 위치를 3D 월드 좌표로 변환
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0; // 2D 환경이므로 Z값 고정

        // 2. 'MouseTrail' 오브젝트를 마우스 위치로 계속 이동시킴
        if (mouseTrailObject != null)
        {
            mouseTrailObject.position = mouseWorldPos;
        }

        // 3. 클릭 이펙트 (마우스를 *누르는 순간*)
        if (Input.GetMouseButtonDown(0))
        {
            if (clickEffectPrefab != null)
            {
                // 클릭한 위치에 'ClickEffect' 프리팹 생성
                Instantiate(clickEffectPrefab, mouseWorldPos, Quaternion.identity);
            }
        }

        // 4. 드래그 꼬리 (마우스를 *누르고 있는 동안*)
        if (trailRenderer != null)
        {
            // GetMouseButton(0)은 누르고 있는 '동안' true를 반환
            trailRenderer.emitting = Input.GetMouseButton(0);
        }
    }
}