using UnityEngine;

public class ClickEffectPlayer : MonoBehaviour
{
    // 1. 인스펙터 창에서 파티클 프리팹을 연결할 변수
    public GameObject clickEffectPrefab;

    // 2. 파티클을 생성할 카메라 (보통 메인 카메라)
    public Camera mainCamera;

    void Start()
    {
        // mainCamera가 할당되지 않았다면 자동으로 Camera.main을 찾음
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        // 3. 마우스 왼쪽 버튼을 클릭했을 때
        if (Input.GetMouseButtonDown(0))
        {
            // 4. 마우스의 2D 화면 위치를 3D 월드 위치로 변환
            //    (주의: Z값은 카메라와의 거리이므로 적절히 조절해야 함)
            Vector3 clickPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);

            // 5. 2D 게임이나 UI처럼 보이게 하려면 Z축 위치를 고정
            clickPosition.z = 0; // 2D 게임의 경우 0으로 설정

            // 6. 프리팹을 클릭 위치에 생성(Instantiate)
            //    Quaternion.identity는 '회전 없음'을 의미
            Instantiate(clickEffectPrefab, clickPosition, Quaternion.identity);
        }
    }
}