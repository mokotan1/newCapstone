using System.Collections;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SpecialJumpscareManager : SingletonMonoBehaviour<SpecialJumpscareManager>
{
    public static event Action OnPlayerDied;
    public static event Action OnEnemyAppeared;
    public static event Action OnJumpscareReset;

    [Header("눈깜빡임 오버레이 (SpriteRenderer)")]
    [Tooltip("카메라 앞에 배치할 전체화면 눈깜빡임 Sprite")]
    public SpriteRenderer blinkOverlay;

    [Header("효과 설정 (블러 등)")]
    public Volume globalVolume;

    [Header("시간 및 확률 설정")]
    public float waitTimeToScare = 3f;
    [Range(0f, 100f)]
    public float spawnChance = 100f;
    public float blinkDuration = 0.2f;
    public float closedDuration = 0.1f;
    public string retrySceneName = SceneNames.MainScene;

    [Header("공포 연출 (지연 효과)")]
    [Tooltip("트리거 발동 후 효과가 시작되기까지의 지연 시간")]
    public float horrorEffectDelay = 0.5f;
    [Tooltip("공포 효과(포스트 프로세싱, 카메라 흔들림) 지속 시간")]
    public float horrorEffectDuration = 1.0f;
    [Tooltip("카메라 흔들림 강도")]
    public float cameraShakeMagnitude = 0.2f;

    [Header("오브젝트")]
    public GameObject parrotObject;
    [Tooltip("적 클릭 트리거용 오브젝트 (SpriteRenderer + Collider2D 필요)")]
    public GameObject triggerObject;
    public Animator jumpscareAnimator;

    [Header("게임오버 오브젝트")]
    [Tooltip("게임오버 시 표시할 오브젝트 (SpriteRenderer 기반)")]
    public GameObject gameOverObject;
    [Tooltip("리트라이 클릭 영역 (Collider2D 필요)")]
    public GameObject retryClickObject;

    [Header("적 등장 시 숨길 오브젝트")]
    [Tooltip("적이 등장하면 비활성화될 Sprite 오브젝트들의 Tag")]
    public string hideObjectTag = "HideOnEnemy";

    [Header("포스트 프로세싱")]
    private ChromaticAberration chromaticAberration;
    private Vignette vignette;
    private LensDistortion lensDistortion;
    private Coroutine chromaticCoroutine;

    private static bool hasVisitedSpecialScene = false;
    private bool hasTriggered = false;
    private DepthOfField dof;
    private readonly int blinkAmountProp = Shader.PropertyToID("_BlinkAmount");
    private bool isBlinkSequenceRunning = false;

    private bool isJumpscareInProgress = false;
    private GameObject sayDialogObject;

    private Camera mainCam;
    private Vector3 originalCameraPos;

    private SpriteRenderer triggerSpriteRenderer;
    private Collider2D triggerCollider;

    void Start()
    {
        if (globalVolume == null)
            globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null)
        {
            if (globalVolume.profile.TryGet(out chromaticAberration))
                chromaticAberration.intensity.value = 0f;

            globalVolume.profile.TryGet(out dof);
            if (dof != null) dof.gaussianMaxRadius.value = 0f;

            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out lensDistortion);
            
            if (vignette != null) vignette.intensity.value = 0f;
            if (lensDistortion != null) lensDistortion.intensity.value = 0f;
        }

        if (blinkOverlay != null && blinkOverlay.material != null)
        {
            blinkOverlay.material = new Material(blinkOverlay.material);
            blinkOverlay.material.SetFloat(blinkAmountProp, 0.5f);
        }

        FitBlinkOverlayToScreen();

        if (triggerObject != null)
        {
            triggerSpriteRenderer = triggerObject.GetComponent<SpriteRenderer>();
            triggerCollider = triggerObject.GetComponent<Collider2D>();
        }

        jumpscareAnimator.gameObject.SetActive(false);
        if (gameOverObject != null) gameOverObject.SetActive(false);

        if (!hasVisitedSpecialScene)
        {
            float randomValue = UnityEngine.Random.Range(0f, 100f);
            if (randomValue <= spawnChance)
            {
                hasVisitedSpecialScene = true;
                SetupEnemyState(true);
            }
            else ShowParrotOnly();
        }
        else ShowParrotOnly();
    }

    private void FitBlinkOverlayToScreen()
    {
        if (blinkOverlay == null) return;

        mainCam = Camera.main;
        if (mainCam == null) return;

        blinkOverlay.transform.SetParent(mainCam.transform);
        blinkOverlay.transform.localPosition = new Vector3(0, 0, 1f);

        float worldHeight = mainCam.orthographicSize * 2f;
        float worldWidth = worldHeight * mainCam.aspect;

        if (blinkOverlay.sprite != null)
        {
            Vector2 spriteSize = blinkOverlay.sprite.bounds.size;
            blinkOverlay.transform.localScale = new Vector3(
                worldWidth / spriteSize.x,
                worldHeight / spriteSize.y,
                1f
            );
        }
    }

    private void SetupEnemyState(bool isPresent)
    {
        if (isPresent)
        {
            if (parrotObject != null) parrotObject.SetActive(false);
            SetTriggerVisible(true);

            SetHideObjectsByTag(true);
            OnEnemyAppeared?.Invoke();

            ChromaticOn();
            StartCoroutine(WaitAndExecuteScare());
        }
    }

    private void ShowParrotOnly()
    {
        if (parrotObject != null) parrotObject.SetActive(true);
        SetTriggerVisible(false);

        SetHideObjectsByTag(false);
    }

    private void SetTriggerVisible(bool visible)
    {
        if (triggerSpriteRenderer != null) triggerSpriteRenderer.enabled = visible;
        if (triggerCollider != null) triggerCollider.enabled = visible;
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (Camera.main == null) return;
        if (isJumpscareInProgress) return;

        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(worldPos);
        if (hit == null) return;

        if (!hasTriggered && triggerObject != null
            && triggerCollider != null && triggerCollider.enabled
            && hit.gameObject == triggerObject)
        {
            ExecuteJumpscare();
            return;
        }

        if (retryClickObject != null && retryClickObject.activeSelf
            && hit.gameObject == retryClickObject)
        {
            CheckpointLoadCoordinator.RefreshLatestProgressSnapshot();
            CheckpointLoadCoordinator.LoadLatestOrFallback(retrySceneName);
        }
    }

    private IEnumerator WaitAndExecuteScare()
    {
        yield return new WaitForSeconds(waitTimeToScare);
        ExecuteJumpscare();
    }

    public void ExecuteJumpscare()
    {
        if (hasTriggered) return;
        hasTriggered = true;
        StopAllCoroutines();

        isJumpscareInProgress = true;
        DisableSayDialog();
        SetTriggerVisible(false);

        // 지연 연출 및 점프스케어 시퀀스 시작
        StartCoroutine(DelayedHorrorSequence());
        StartCoroutine(FullJumpscareSequence());
    }

    // --- 수정된 연출 코루틴 (1초 발동 후 종료) ---
    private IEnumerator DelayedHorrorSequence()
    {
        // 1. 지정된 시간(0.5초) 대기
        yield return new WaitForSeconds(horrorEffectDelay);

        // 2. 카메라 원래 위치 저장
        if (Camera.main != null)
        {
            originalCameraPos = Camera.main.transform.localPosition;
        }

        // 3. 지정된 시간(1초) 동안 효과 발동
        float elapsed = 0f;
        float effectRampUpTime = 0.2f; // 효과가 최대로 도달하는 시간

        while (elapsed < horrorEffectDuration)
        {
            elapsed += Time.deltaTime;
            
            // 카메라 흔들림 적용
            if (Camera.main != null)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * cameraShakeMagnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * cameraShakeMagnitude;
                Camera.main.transform.localPosition = new Vector3(originalCameraPos.x + x, originalCameraPos.y + y, originalCameraPos.z);
            }

            // 포스트 프로세싱 적용 (부드럽게 최대치 도달 후 유지)
            float t = Mathf.Clamp01(elapsed / effectRampUpTime);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.5f, t);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, t);

            yield return null;
        }

        // 4. 효과 종료 및 원래 상태로 즉시 복구
        if (Camera.main != null)
        {
            Camera.main.transform.localPosition = originalCameraPos;
        }

        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;
    }
    // ---------------------------------------------

    private IEnumerator FullJumpscareSequence()
    {
        yield return StartCoroutine(AnimateBlink(0.5f, 0f, 0f, 2.0f, blinkDuration));
        yield return new WaitForSeconds(closedDuration);

        jumpscareAnimator.gameObject.SetActive(true);
        jumpscareAnimator.SetTrigger("Scare");

        yield return StartCoroutine(AnimateBlink(0f, 0.5f, 2.0f, 0f, blinkDuration));
    }

    public void OnFrameTransition()
    {
        if (isBlinkSequenceRunning) return;
        StartCoroutine(FrameTransitionBlink());
    }

    private IEnumerator FrameTransitionBlink()
    {
        isBlinkSequenceRunning = true;

        yield return StartCoroutine(AnimateBlink(0.5f, 0f, 0f, 2.0f, blinkDuration));
        yield return new WaitForSeconds(closedDuration);
        yield return StartCoroutine(AnimateBlink(0f, 0.5f, 2.0f, 0f, blinkDuration));

        isBlinkSequenceRunning = false;
    }

    private IEnumerator AnimateBlink(float bStart, float bEnd, float blStart, float blEnd, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (blinkOverlay != null && blinkOverlay.material != null)
                blinkOverlay.material.SetFloat(blinkAmountProp, Mathf.Lerp(bStart, bEnd, t));

            if (dof != null)
                dof.gaussianMaxRadius.value = Mathf.Lerp(blStart, blEnd, t);

            yield return null;
        }

        if (blinkOverlay != null && blinkOverlay.material != null)
            blinkOverlay.material.SetFloat(blinkAmountProp, bEnd);

        if (dof != null)
            dof.gaussianMaxRadius.value = blEnd;
    }

    public void OnJumpscareFinished()
    {
        jumpscareAnimator.gameObject.SetActive(false);
        if (gameOverObject != null) gameOverObject.SetActive(true);
        
        // 만약을 대비한 포스트 프로세싱 초기화 보장
        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;

        OnPlayerDied?.Invoke();
        OnJumpscareReset?.Invoke();
        ChromaticOff();

        isJumpscareInProgress = false;
    }

    private void SetHideObjectsByTag(bool hide)
    {
        if (string.IsNullOrEmpty(hideObjectTag)) return;

        GameObject[] targets = GameObject.FindGameObjectsWithTag(hideObjectTag);
        foreach (var obj in targets)
        {
            if (obj != null)
                obj.SetActive(!hide);
        }
    }

    public void ChromaticOn()
    {
        if (chromaticAberration == null) return;

        if (chromaticCoroutine != null)
        {
            StopCoroutine(chromaticCoroutine);
        }
        
        chromaticCoroutine = StartCoroutine(ChromaticRoutine());
    }

    public void ChromaticOff()
    {
        if (chromaticAberration == null) return;

        if (chromaticCoroutine != null)
        {
            StopCoroutine(chromaticCoroutine);
            chromaticCoroutine = null;
        }

        chromaticAberration.intensity.value = 0f;
    }

    private IEnumerator ChromaticRoutine()
    {
        float timer = 0f;

        while (true)
        {
            timer += Time.deltaTime;
            float intensity = 0.5f * Mathf.Cos((timer / 10f) * 2f * Mathf.PI) + 0.5f;
            chromaticAberration.intensity.value = intensity;
            yield return null;
        }
    }

    private void DisableSayDialog()
    {
        if (sayDialogObject == null)
            sayDialogObject = GameObject.Find("SayDialog");

        if (sayDialogObject != null && sayDialogObject.activeSelf)
            sayDialogObject.SetActive(false);
    }

    private void RestoreSayDialog()
    {
        if (sayDialogObject != null && !sayDialogObject.activeSelf)
            sayDialogObject.SetActive(true);
        sayDialogObject = null;
    }
}
