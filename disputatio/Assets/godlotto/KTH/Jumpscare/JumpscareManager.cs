using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public struct JumpscareSceneData
{
    public string sceneName;        // 씬 이름
    [Tooltip("카메라 중심을 (0,0)으로 하는 상대 좌표입니다. (0,0) 입력 시 화면 정중앙에 스폰됩니다.")]
    public Vector2 spawnPosition;
    [Range(0f, 100f)]
    [Tooltip("씬 진입 직후 첫 판정에서의 등장 확률(%). 실패 시 같은 씬에 일정 시간 이상 머무르면 100%로 등장합니다.")]
    public float spawnChance;
}

[System.Serializable]
public struct AutoRegisteredSpawnPositionOverride
{
    [Tooltip("자동 등록 대상 sceneName과 정확히 일치해야 적용됩니다.")]
    public string sceneName;
    [Tooltip("해당 자동 등록 씬에서 사용할 적 스폰 위치입니다.")]
    public Vector2 spawnPosition;
}

public class JumpscareManager : SingletonMonoBehaviour<JumpscareManager>
{
    public static event Action OnPlayerDied;
    public static event Action OnEnemyAppeared;
    public static event Action OnJumpscareReset;

    protected override bool PersistAcrossScenes => true;

    /// <summary>
    /// 에셋 파일명(확장자 제외)과 동일한 Scene.name을 가정합니다. Awake에서 targetScenes에 없는 이름만 병합합니다.
    /// </summary>
    private static readonly string[] MokotanJumpscareSceneNames =
    {
        "Hall_Left",
        "Hall_Left2",
        "Hallway_Left2",
        SceneNames.HallRight,
        "Hall_Right2",
        "Hallway_Right2",
        "Hallway_Right",
        "2floorRight",
        "2floorLeft",
        "2floorHallway_Right",
        "2floorHallway_Left",
    };

    [Header("씬별 설정 목록")]
    [SerializeField] private List<JumpscareSceneData> targetScenes;

    [Header("Mokotan 점프스케어 씬 자동 등록")]
    [Tooltip("코드에 정의된 Mokotan 복도·층 씬이 targetScenes에 없을 때 추가하며, 이때 사용할 진입 직후 spawnChance입니다. 이미 있는 sceneName은 건드리지 않습니다.")]
    [Range(0f, 100f)]
    [SerializeField] private float defaultSpawnChanceForMokotanScenes = 20f;
    [Tooltip("자동 등록되는 씬에 공통 적용할 기본 적 스폰 위치입니다.")]
    [SerializeField] private Vector2 defaultAutoRegisteredSpawnPosition = Vector2.zero;
    [Tooltip("자동 등록되는 씬별 적 스폰 위치 오버라이드 목록입니다. sceneName 일치 시 이 값이 우선 적용됩니다.")]
    [SerializeField] private List<AutoRegisteredSpawnPositionOverride> autoRegisteredSpawnPositionOverrides = new List<AutoRegisteredSpawnPositionOverride>();

    [Tooltip("진입 직후 스폰에 실패한 경우, 같은 활성 씬에 이 시간(초) 이상 머무르면 등장 확률 100%로 트리거를 띄웁니다.")]
    [SerializeField] private float guaranteedJumpscareAfterSeconds = 60f;

    [Header("효과 설정 (블러 등)")]
    public Volume globalVolume;

    [Header("눈깜빡임 오버레이 (SpriteRenderer)")]
    [SerializeField] private SpriteRenderer blinkOverlay;

    [Header("시간 설정")]
    [SerializeField] private float waitTimeToScare = 3f;
    [SerializeField] private float animationDuration = 2f;
    [SerializeField] private float blinkDuration = 0.2f;
    [SerializeField] private float closedDuration = 0.1f;
    [SerializeField] private string retrySceneName = SceneNames.MainScene;

    [Header("공포 연출 (지연 효과)")]
    [Tooltip("트리거 발동 후 효과가 시작되기까지의 지연 시간")]
    public float horrorEffectDelay = 0.5f;
    [Tooltip("공포 효과(포스트 프로세싱, 카메라 흔들림) 지속 시간")]
    public float horrorEffectDuration = 1.0f;
    [Tooltip("카메라 흔들림 강도")]
    public float cameraShakeMagnitude = 0.2f;

    [Header("트리거 깊이 (2D 오쏘)")]
    [Tooltip("트리거 월드 Z = Main Camera Z + 이 값. (예: 카메라 z=-10, 스프라이트 평면 z=0 → 10)")]
    [SerializeField] private float triggerWorldZOffsetFromCamera = 10f;

    [Header("트리거 스프라이트")]
    [Tooltip("Lit 씬 조명 없이도 보이게 URP Sprite-Unlit을 씁니다. 끄면 프리팹 머티리얼을 유지합니다.")]
    [SerializeField] private bool useUnlitMaterialForTrigger = true;
#if UNITY_EDITOR
    [Tooltip("에디터/개발 빌드에서만 스폰 직후 트리거 렌더 상태를 한 프레임 뒤 로그합니다.")]
    [SerializeField] private bool logTriggerRenderingAfterSpawn;
#endif

    [Header("오브젝트 할당")]
    [Tooltip("적 클릭 트리거용 오브젝트 (SpriteRenderer + Collider2D 필요)")]
    [SerializeField] private GameObject triggerObject;
    [SerializeField] private Animator jumpscareAnimator;

    [Header("게임오버 오브젝트")]
    [Tooltip("게임오버 시 표시할 오브젝트 (SpriteRenderer 기반)")]
    [SerializeField] private GameObject gameOverObject;
    [Tooltip("리트라이 클릭 영역 (Collider2D 필요)")]
    [SerializeField] private GameObject retryClickObject;

    [Header("적 등장 시 숨길 오브젝트")]
    [Tooltip("적이 등장하면 비활성화될 Sprite 오브젝트들의 Tag")]
    [SerializeField] private string hideObjectTag = "HideOnEnemy";

    // 포스트 프로세싱 효과 캐싱
    private Vignette vignette;
    private LensDistortion lensDistortion;

    private bool hasTriggered = false;
    private bool isJumpscareInProgress = false;
    private GameObject sayDialogObject;

    private Vector3 originalCameraPos;

    private SpriteRenderer triggerSpriteRenderer;
    private Collider2D triggerCollider;

    private JumpscareEffects _effects;
    private JumpscareSpawner _spawner;

    protected override void OnSingletonAwake()
    {
        EnsureMokotanJumpscareScenesRegistered();

        // Volume Profile에서 추가 포스트 프로세싱 효과 가져오기
        if (globalVolume == null)
            globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out vignette);
            globalVolume.profile.TryGet(out lensDistortion);
            
            if (vignette != null) vignette.intensity.value = 0f;
            if (lensDistortion != null) lensDistortion.intensity.value = 0f;
        }

        _effects = new JumpscareEffects(
            blinkOverlay,
            gameOverObject,
            jumpscareAnimator,
            blinkDuration,
            closedDuration);

        if (triggerObject != null)
        {
            triggerSpriteRenderer = triggerObject.GetComponent<SpriteRenderer>();
            triggerCollider = triggerObject.GetComponent<Collider2D>();
        }

        _spawner = new JumpscareSpawner(
            this,
            triggerObject,
            triggerSpriteRenderer,
            triggerCollider,
            blinkOverlay,
            _effects,
            targetScenes,
            triggerWorldZOffsetFromCamera,
            waitTimeToScare,
            guaranteedJumpscareAfterSeconds,
            hideObjectTag,
            useUnlitMaterialForTrigger,
            () => hasTriggered,
            ExecuteJumpscare
#if UNITY_EDITOR
            , logTriggerRenderingAfterSpawn
#endif
        );

        _effects.InitBlinkMaterial();
        _effects.FindAndBindVolume();
        _effects.FitBlinkOverlayToScreen();
        _effects.FitGameOverOverlayToScreen();

        _spawner.ApplyUnlitTriggerMaterialIfNeeded();
    }

    private void EnsureMokotanJumpscareScenesRegistered()
    {
        if (targetScenes == null)
            targetScenes = new List<JumpscareSceneData>();

        foreach (string sceneName in MokotanJumpscareSceneNames)
        {
            if (string.IsNullOrEmpty(sceneName))
                continue;

            bool exists = false;
            foreach (var entry in targetScenes)
            {
                if (entry.sceneName == sceneName)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
                continue;

            Vector2 autoRegisteredSpawnPosition = GetAutoRegisteredSpawnPosition(sceneName);
            targetScenes.Add(new JumpscareSceneData
            {
                sceneName = sceneName,
                spawnPosition = autoRegisteredSpawnPosition,
                spawnChance = defaultSpawnChanceForMokotanScenes
            });
        }
    }

    private Vector2 GetAutoRegisteredSpawnPosition(string sceneName)
    {
        foreach (var entry in autoRegisteredSpawnPositionOverrides)
        {
            if (string.Equals(entry.sceneName, sceneName, StringComparison.Ordinal))
                return entry.spawnPosition;
        }

        if (string.Equals(sceneName, SceneNames.HallRight, StringComparison.Ordinal))
            return new Vector2(0f, -2.75f);

        return defaultAutoRegisteredSpawnPosition;
    }

    public bool IsGhostEncounterBlockingMapUi()
    {
        if (isJumpscareInProgress)
            return true;

        if (triggerObject == null || !triggerObject.activeSelf)
            return false;

        if (triggerSpriteRenderer != null)
            return triggerSpriteRenderer.enabled;

        return triggerCollider != null && triggerCollider.enabled;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬 이동 시 포스트 프로세싱 효과 초기화
        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;

        _effects?.FindAndBindVolume();
        ResetJumpscareState();
        _effects?.FitBlinkOverlayToScreen();
        _effects?.FitGameOverOverlayToScreen();

        _spawner?.HandleSceneLoaded(scene, mode);
    }

    private void ResetJumpscareState()
    {
        _effects?.ResetBlinkSequenceFlag();
        StopAllCoroutines();
        hasTriggered = false;
        isJumpscareInProgress = false;

        _spawner?.SetTriggerVisible(false);
        _effects?.SetAnimatorActive(false);
        _effects?.SetGameOverActive(false);

        _effects?.ResetBlinkAndDepthOfField();

        _spawner?.SetHideObjectsByTag(false);
        _spawner?.SetMainCanvasVisible(true);

        RestoreSayDialog();
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
            CheckpointLoadCoordinator.LoadLatestOrFallback(retrySceneName);
        }
    }

    public void ExecuteJumpscare()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        StopAllCoroutines();

        isJumpscareInProgress = true;

        DisableSayDialog();

        _spawner?.SetTriggerVisible(false);

        if (triggerObject != null && jumpscareAnimator != null)
            _effects.PositionJumpscareAnimator(triggerObject.transform.position);

        // 지연 연출 효과 코루틴 실행
        StartCoroutine(DelayedHorrorSequence());

        if (_effects != null)
            StartCoroutine(_effects.FullJumpscareSequence());
    }

    // --- 카메라 흔들림 및 포스트 프로세싱 지연 연출 코루틴 ---
    private IEnumerator DelayedHorrorSequence()
    {
        yield return new WaitForSeconds(horrorEffectDelay);

        if (Camera.main != null)
        {
            originalCameraPos = Camera.main.transform.localPosition;
        }

        float elapsed = 0f;
        float effectRampUpTime = 0.2f;

        while (elapsed < horrorEffectDuration)
        {
            elapsed += Time.deltaTime;
            
            if (Camera.main != null)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * cameraShakeMagnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * cameraShakeMagnitude;
                Camera.main.transform.localPosition = new Vector3(originalCameraPos.x + x, originalCameraPos.y + y, originalCameraPos.z);
            }

            float t = Mathf.Clamp01(elapsed / effectRampUpTime);
            if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, 0.5f, t);
            if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, -0.6f, t);

            yield return null;
        }

        if (Camera.main != null)
        {
            Camera.main.transform.localPosition = originalCameraPos;
        }

        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;
    }
    // ---------------------------------------------------------

    public void OnFrameTransition()
    {
        if (_effects == null || _effects.IsBlinkSequenceRunning)
            return;
        StartCoroutine(_effects.FrameTransitionBlink());
    }

    public void OnJumpscareFinished()
    {
        _effects?.SetAnimatorActive(false);
        _effects?.ShowGameOverAfterFit();
        
        // 포스트 프로세싱 초기화 보장
        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;

        OnPlayerDied?.Invoke();

        isJumpscareInProgress = false;
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
