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

        // 이 오브젝트 자신과 자식의 모든 렌더러를 최상위 레이어로 설정
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerName = "Setting";
            r.sortingOrder = 100;
        }

        SetParticleSystemsUseUnscaledTime(gameObject);
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
                GameLog.LogWarning("경고: 씬에서 'EffectCamera' 태그가 붙은 카메라를 찾을 수 없습니다.");
            }
        }
    }

    private GameObject activeClickEffect;

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
            if (activeClickEffect != null) Destroy(activeClickEffect);
            activeClickEffect = Instantiate(clickEffectPrefab, clickPosition, Quaternion.identity);
            ConfigureFxInstance(activeClickEffect);
        }

        // 마우스를 떼는 순간 이펙트 제거 (패널 닫힌 후에도 파티클이 남지 않도록)
        if (Input.GetMouseButtonUp(0))
        {
            if (activeClickEffect != null)
            {
                Destroy(activeClickEffect);
                activeClickEffect = null;
            }
        }
    }

    private static void ConfigureFxInstance(GameObject effect)
    {
        SetEffectTopSorting(effect);
        SetParticleSystemsUseUnscaledTime(effect);
    }

    private static void SetEffectTopSorting(GameObject effect)
    {
        if (effect == null) return;
        foreach (var psr in effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            psr.sortingLayerName = "Setting";
            psr.sortingOrder = 100;
        }
    }

    /// <summary>Time.timeScale=0 일 때도 클릭 파티클이 재생되게 함.</summary>
    private static void SetParticleSystemsUseUnscaledTime(GameObject root)
    {
        if (root == null) return;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.useUnscaledTime = true;
        }
    }
}