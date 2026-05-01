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
            if (trailRenderer != null)
                trailRenderer.emitting = false;

            // 트레일 오브젝트의 모든 렌더러를 최상위 레이어로 설정
            // (세팅 패널 등 어떤 UI가 열려도 항상 마우스 이펙트가 위에 보이도록)
            foreach (var r in mouseTrailObject.GetComponentsInChildren<Renderer>(true))
            {
                r.sortingLayerName = "Setting";
                r.sortingOrder = 100;
            }

            // 설정 패널 등에서 Time.timeScale=0 일 때도 입자 트레일이 멈추지 않게 함
            SetParticleSystemsUseUnscaledTime(mouseTrailObject.gameObject);
        }
    }

    private GameObject activeClickEffect;

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
                if (activeClickEffect != null) Destroy(activeClickEffect);
                activeClickEffect = Instantiate(clickEffectPrefab, mouseWorldPos, Quaternion.identity);
                ConfigureFxInstance(activeClickEffect);
            }
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

        // 4. 드래그 꼬리 (마우스를 *누르고 있는 동안*)
        if (trailRenderer != null)
        {
            // GetMouseButton(0)은 누르고 있는 '동안' true를 반환
            trailRenderer.emitting = Input.GetMouseButton(0);
        }
    }

    // 세팅 패널 등 최상위 UI보다 위에 렌더링 + timeScale==0 에서도 시뮬레이션
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

    /// <summary>IntegratedSettingUI 등이 Time.timeScale=0 으로 멈춰도 마우스 이펙트가 재생되게 함.</summary>
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