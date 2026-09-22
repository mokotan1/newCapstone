using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 나침반 채(프레임)을 드래그로 회전시키고, 침은 <see cref="SetAngle"/> 등으로 세팅되는
/// 월드 Z 헤딩 방향을 유지합니다. 눈금(프레임)이 도는 만큼 침이 스케일상 해당 방향을 가리킵니다.
/// </summary>
public class CompassController : MonoBehaviour
{
    [Tooltip("컨트롤러 피벗(보통 Compass 루트). 마우스 각도 계산 원점.")]
    Transform PivotTransform => transform;

    [SerializeField] Transform needleTransform;

    [Tooltip("회전할 채(눈금). 비우면 프레임 드래그 비활성.")]
    [SerializeField] Transform rotatingFrameTransform;

    [Tooltip("채 디스크 히트(bound). 회전 타깃과 다를 필요 없음.")]
    [SerializeField] SpriteRenderer frameRenderer;

    [Tooltip("프레임이 없을 때 원형 디스크로 인식할 반경.")]
    [SerializeField] float dragMaxRadiusWorld = 6f;

    [Tooltip("true면 디스크 전체에서 드래그 가능(침 위 포함). false면 침 스프라이트 위에서는 프레임만 돌리지 않음.")]
    [SerializeField] bool treatNeedleAsPartOfDial = true;

    [Header("침 근처 제외 영역 — treatNeedleAsPartOfDial가 false일 때만")]
    [SerializeField] float needleHalfWidth = 0.945f;
    [SerializeField] float needleHalfHeight = 5.245f;

    [Header("침 헤딩 보간")]
    [SerializeField] float smoothSpeed = 0f;

    [Tooltip("침 헤딩 변경 시.")]
    public UnityEvent<float> onAngleChanged;

    [Tooltip("채 로컬 Z가 바뀔 때.")]
    public UnityEvent<float> onFrameRotationChanged;

    Vector2 Pivot2D => new Vector2(PivotTransform.position.x, PivotTransform.position.y);

    float magneticHeadingWorldZ;
    float displayHeadingZ;
    float frameLocalZ;
    float lastPolarDegFrameDrag;
    bool isDraggingFrame;
    Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;

        if (needleTransform != null)
            magneticHeadingWorldZ = needleTransform.eulerAngles.z;

        displayHeadingZ = magneticHeadingWorldZ;

        if (rotatingFrameTransform != null)
            frameLocalZ = NormalizeSigned180(rotatingFrameTransform.localEulerAngles.z);

        ApplyFrameRotation();
        ApplyNeedleRotation();
    }

    public void SetAngle(float degreesWorldZ)
    {
        magneticHeadingWorldZ = degreesWorldZ;
        displayHeadingZ = degreesWorldZ;
        ApplyNeedleRotation();
        onAngleChanged?.Invoke(displayHeadingZ);
    }

    public void SetAngleSmooth(float degreesWorldZ)
    {
        magneticHeadingWorldZ = degreesWorldZ;
    }

    public float GetAngle() => displayHeadingZ;

    public float GetFrameDialAngleLocal() => frameLocalZ;

    /// <summary>침 헤딩과 채 회전 차이(표시 각으로 사용 가능).</summary>
    public float GetNeedleVersusDialRelative()
        => NormalizeSigned180(displayHeadingZ - frameLocalZ);

    void Update()
    {
        HandleFrameDragInput();

        if (!isDraggingFrame && smoothSpeed > 0f &&
            Mathf.Abs(Mathf.DeltaAngle(displayHeadingZ, magneticHeadingWorldZ)) >= 0.01f)
        {
            displayHeadingZ = Mathf.LerpAngle(displayHeadingZ, magneticHeadingWorldZ, Time.deltaTime * smoothSpeed);
            ApplyNeedleRotation();
        }
    }

    void LateUpdate()
    {
        if (!isDraggingFrame && smoothSpeed <= 0f && needleTransform != null)
            ApplyNeedleRotation();

        // 인스펙터 등에서 회전 변경 시 내부 값 동기화
        if (!isDraggingFrame && rotatingFrameTransform != null)
        {
            float actual = NormalizeSigned180(rotatingFrameTransform.localEulerAngles.z);
            if (Mathf.Abs(Mathf.DeltaAngle(frameLocalZ, actual)) > 0.05f)
            {
                frameLocalZ = actual;
                onFrameRotationChanged?.Invoke(frameLocalZ);
            }
        }
    }

    void HandleFrameDragInput()
    {
        if (Input.GetMouseButtonUp(0))
            isDraggingFrame = false;

        if (rotatingFrameTransform == null)
            return;

        Vector2 mouseWorld = GetMouseWorldPos();

        if (Input.GetMouseButtonDown(0))
        {
            if (!PointerOnFrameDisk(mouseWorld))
                return;

            bool skipDial = (!treatNeedleAsPartOfDial) &&
                            needleTransform != null &&
                            IsMouseOnNeedle(mouseWorld);

            if (!skipDial)
            {
                isDraggingFrame = true;
                lastPolarDegFrameDrag = MouseToPolarDegrees(mouseWorld);
            }
        }

        if (!isDraggingFrame || !Input.GetMouseButton(0))
            return;

        mouseWorld = GetMouseWorldPos();
        if ((mouseWorld - Pivot2D).sqrMagnitude < 1e-12f)
            return;

        float polar = MouseToPolarDegrees(mouseWorld);
        float twist = Mathf.DeltaAngle(lastPolarDegFrameDrag, polar);
        frameLocalZ = NormalizeSigned180(frameLocalZ + twist);
        lastPolarDegFrameDrag = polar;

        ApplyFrameRotation();
        ApplyNeedleRotation();
        onFrameRotationChanged?.Invoke(frameLocalZ);
    }

    float MouseToPolarDegrees(Vector2 world)
        => Mathf.Atan2(world.x - Pivot2D.x, world.y - Pivot2D.y) * Mathf.Rad2Deg;

    bool PointerOnFrameDisk(Vector2 mouseWorld)
    {
        if (frameRenderer != null)
        {
            var b = frameRenderer.bounds;
            return mouseWorld.x >= b.min.x && mouseWorld.x <= b.max.x &&
                   mouseWorld.y >= b.min.y && mouseWorld.y <= b.max.y;
        }

        float r = Mathf.Max(0f, dragMaxRadiusWorld);
        if (r <= 0f)
            return false;

        return (mouseWorld - Pivot2D).sqrMagnitude <= r * r;
    }

    bool IsMouseOnNeedle(Vector2 mouseWorld)
    {
        if (needleTransform == null)
            return false;

        Vector2 local = needleTransform.InverseTransformPoint(mouseWorld);
        return Mathf.Abs(local.x) <= needleHalfWidth && Mathf.Abs(local.y) <= needleHalfHeight;
    }

    Vector2 GetMouseWorldPos()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return Vector2.zero;

        Vector3 mouse = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(mouse.x, mouse.y);
    }

    void ApplyNeedleRotation()
    {
        if (needleTransform == null)
            return;

        float z = smoothSpeed <= 0f ? magneticHeadingWorldZ : displayHeadingZ;
        needleTransform.rotation = Quaternion.Euler(0f, 0f, z);
    }

    void ApplyFrameRotation()
    {
        if (rotatingFrameTransform == null)
            return;

        rotatingFrameTransform.localRotation = Quaternion.Euler(0f, 0f, frameLocalZ);
    }

    static float NormalizeSigned180(float eulerZ)
        => Mathf.DeltaAngle(0f, eulerZ);
}
