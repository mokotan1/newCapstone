using UnityEngine;

public class ClickEffectPlayer : MonoBehaviour
{
    // 1. 인스펙터 창에서 파티클 프리팹을 연결할 변수
    public GameObject clickEffectPrefab;

    // 2. 파티클을 생성할 카메라 (씬 이동 시마다 자동으로 찾아짐)
    public Camera mainCamera;

    void Start()
    {
        // 씬이 로드되거나 오브젝트가 활성화될 때 한 번 실행
        FindMainCamera();
    }

    // 씬이 이동되거나 리로드될 때마다 카메라를 찾을 수 있도록
    // Start()에서 호출하거나 필요할 때 호출하는 전용 함수 생성
    private void FindMainCamera()
    {
        // mainCamera가 이미 할당되어 있다면 다시 찾지 않음
        // 하지만 씬 이동으로 인해 카메라가 파괴되었다면 null이 됨
        if (mainCamera == null)
        {
            // "MainCamera" 태그가 붙은 카메라를 찾음
            // 씬 이동 후 새로운 씬의 카메라를 자동으로 찾게 됩니다.
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning("경고: 씬에서 'MainCamera' 태그가 붙은 카메라를 찾을 수 없습니다.");
            }
        }
    }

    void Update()
    {
        // 씬 이동 등으로 카메라가 사라졌을 경우 다시 찾도록 시도
        // (빈번한 씬 이동이 없다면 FindMainCamera()를 Update에서 자주 호출할 필요는 없습니다.
        // Start()에서 찾는 것만으로 대부분 충분합니다.)
        if (mainCamera == null)
        {
            FindMainCamera();
            // 카메라를 찾지 못했다면 이 프레임에서는 효과 생성 스킵
            if (mainCamera == null) return;
        }

        // 3. 마우스 왼쪽 버튼을 클릭했을 때
        if (Input.GetMouseButtonDown(0))
        {
            // 4. 마우스의 2D 화면 위치를 3D 월드 위치로 변환
            // Z값은 카메라와의 거리이므로 적절히 조절해야 함
            Vector3 clickPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);

            // 5. 2D 게임이나 UI처럼 보이게 하려면 Z축 위치를 고정
            // 2D 게임의 경우 0으로 설정
            clickPosition.z = 0;

            // 6. 프리팹을 클릭 위치에 생성(Instantiate)
            // Quaternion.identity는 '회전 없음'을 의미
            Instantiate(clickEffectPrefab, clickPosition, Quaternion.identity);
        }
    }
}