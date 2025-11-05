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

    // "EffectCamera" 태그를 가진 카메라를 찾는 함수
    private void FindMainCamera()
    {
        // mainCamera가 null일 때만 검색
        if (mainCamera == null)
        {
            // "EffectCamera" 태그가 붙은 GameObject를 찾습니다.
            GameObject camObject = GameObject.FindWithTag("EffectCamera");

            // GameObject를 찾았는지 확인
            if (camObject != null)
            {
                // 찾은 GameObject에서 Camera 컴포넌트를 가져옵니다.
                mainCamera = camObject.GetComponent<Camera>();
            }

            // 위 과정 후에도 mainCamera가 null이라면 (태그를 못 찾았거나, 찾았는데 Camera 컴포넌트가 없는 경우)
            if (mainCamera == null)
            {
                Debug.LogWarning("경고: 씬에서 'EffectCamera' 태그가 붙은 카메라를 찾을 수 없습니다.");
            }
        }
    }

    void Update()
    {
        // 씬 이동 등으로 카메라가 사라졌을 경우 다시 찾도록 시도
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
            Vector3 clickPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);

            // 5. 2D 게임이나 UI처럼 보이게 하려면 Z축 위치를 고정
            clickPosition.z = 0; 

            // 6. 프리팹을 클릭 위치에 생성(Instantiate)
            Instantiate(clickEffectPrefab, clickPosition, Quaternion.identity);
        }
    }
}