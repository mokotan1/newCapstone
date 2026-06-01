using System.Collections;
using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SpecialJumpscareManager : SingletonMonoBehaviour<SpecialJumpscareManager>
{
    private const float OverlayPlaneZOffsetFromCamera = 1f;
    private const string MainCanvasTag = "MainCanvas";
    private const string BackgroundImageObjectName = "BackgroundImage";
    private const string No40UiHooksObjectName = "No40_UI_Hooks";
    private const int DarkOverlaySortingOrder = 32000;
    private const int JumpscareTopSortingOrder = 32760;
    private const int BlinkOverlaySortingOrder = 32767;
    private const float GhostSecondFrameDelay = 0.4166667f;
    private const int DarkOverlayTextureSize = 128;
    private const float DarkOverlaySoftEdgePixels = 3f;

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
    [Min(1)] public int initialBlinkCount = 1;
    [Min(0f)] public float blinkInterval = 0f;
    [Min(0.001f)] public float darkOverlayStartSize = 0.01f;
    [Min(0f)] public float secondFrameTime = 0.16666667f;
    [Min(0f)] public float fourthFrameTime = 0.5f;
    [Min(0f)] public float blackScreenShakeDuration = 1.5f;
    public float finalFrameHoldDuration = 2f;
    public string retrySceneName = SceneNames.MainScene;

    [Header("공포 연출 (지연 효과)")]
    [Tooltip("트리거 발동 후 효과가 시작되기까지의 지연 시간")]
    public float horrorEffectDelay = 0.5f;
    [Tooltip("공포 효과(포스트 프로세싱, 카메라 흔들림) 지속 시간")]
    public float horrorEffectDuration = 1.0f;
    [Tooltip("카메라 흔들림 강도")]
    public float cameraShakeMagnitude = 0.2f;

    [Header("Jumpscare SFX")]
    [SerializeField] private AudioClip heartbeatSound;
    [SerializeField] private AudioSource heartbeatAudioSource;
    [SerializeField] private AudioClip jumpscareSound;
    [SerializeField] private AudioSource jumpscareAudioSource;

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
    private bool isFinishControlledBySequence = false;
    private GameObject sayDialogObject;

    private Camera mainCam;
    private Vector3 originalCameraPos;

    private SpriteRenderer triggerSpriteRenderer;
    private Collider2D triggerCollider;
    private readonly List<GameObject> hiddenRootObjects = new List<GameObject>();
    private SpriteRenderer darkOverlay;
    private Sprite darkOverlaySprite;
    private Material darkOverlayMaterial;

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
        EnsureHeartbeatAudioSource();
        EnsureJumpscareAudioSource();

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
                if (ShouldDeferGhostUntilFirstEntryDialogue())
                {
                    ApplyPreRetrySceneIsolation();
                    StartCoroutine(WaitForFirstEntryThenSpawnEnemy());
                }
                else
                    SetupEnemyState(true);
            }
            else ShowParrotOnly();
        }
        else ShowParrotOnly();
    }

    private static bool ShouldDeferGhostUntilFirstEntryDialogue()
    {
        return string.Equals(SceneManager.GetActiveScene().name, SceneNames.HallPlayable, StringComparison.Ordinal)
            && PlayerPrefs.GetInt(No40ConditionalDialogueRunner.PrefsKeys.FirstEntryPlayed, 0) == 0;
    }

    private IEnumerator WaitForFirstEntryThenSpawnEnemy()
    {
        if (PlayerPrefs.GetInt(No40ConditionalDialogueRunner.PrefsKeys.FirstEntryPlayed, 0) != 0)
        {
            if (!hasTriggered)
                SetupEnemyState(true);
            yield break;
        }

        bool completed = false;
        Action onCompleted = () => completed = true;
        No40ConditionalDialogueRunner.OnFirstEntryDialogueCompleted += onCompleted;

        try
        {
            while (!completed
                && PlayerPrefs.GetInt(No40ConditionalDialogueRunner.PrefsKeys.FirstEntryPlayed, 0) == 0)
            {
                yield return null;
            }
        }
        finally
        {
            No40ConditionalDialogueRunner.OnFirstEntryDialogueCompleted -= onCompleted;
        }

        if (!hasTriggered)
            SetupEnemyState(true);
    }

    private void FitBlinkOverlayToScreen()
    {
        if (blinkOverlay == null) return;

        mainCam = Camera.main;
        if (mainCam == null) return;

        blinkOverlay.transform.SetParent(mainCam.transform);
        blinkOverlay.transform.localPosition = new Vector3(0, 0, 1f);
        blinkOverlay.sortingOrder = Mathf.Max(blinkOverlay.sortingOrder, BlinkOverlaySortingOrder);

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
            ApplyPreRetrySceneIsolation();
            SetTriggerVisible(true);
            OnEnemyAppeared?.Invoke();
            PlayHeartbeatSound();

            ChromaticOn();
            StartCoroutine(WaitAndExecuteScare());
        }
    }

    private void ApplyPreRetrySceneIsolation()
    {
        if (parrotObject != null)
            parrotObject.SetActive(false);

        SetHideObjectsByTag(true);
        SetOtherSceneRootObjectsVisible(false);
    }

    private void ShowParrotOnly()
    {
        if (parrotObject != null) parrotObject.SetActive(true);
        SetTriggerVisible(false);

        SetHideObjectsByTag(false);
        SetOtherSceneRootObjectsVisible(true);
        StopHeartbeatSound();
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
            StopHeartbeatSound();
            StopJumpscareSound();
            RestoreInputStateBeforeRetry();
            InventoryAccessState.TryUnlockAfterRetry(SceneManager.GetActiveScene().name, retrySceneName, true);
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
        isFinishControlledBySequence = true;
        DisableSayDialog();

        StartCoroutine(FullJumpscareSequence());
    }

    private void StartHorrorEffectNow(bool playJumpscareSound = true)
    {
        StartCoroutine(HorrorEffectSequence(blackScreenShakeDuration, playJumpscareSound));
    }

    private static Vector3 GetScreenCenterWorldPosition(float z)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return new Vector3(0f, 0f, z);

        return new Vector3(cam.transform.position.x, cam.transform.position.y, z);
    }

    // --- 카메라 흔들림 및 포스트 프로세싱 연출 코루틴 ---
    private IEnumerator HorrorEffectSequence(float duration, bool playJumpscareSound)
    {
        if (playJumpscareSound)
            PlayJumpscareSound();

        if (Camera.main != null)
        {
            originalCameraPos = Camera.main.transform.localPosition;
        }

        float elapsed = 0f;
        float effectRampUpTime = 0.2f;

        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
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
    // ---------------------------------------------

    private void EnsureHeartbeatAudioSource()
    {
        if (heartbeatAudioSource != null)
            return;

        heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        heartbeatAudioSource.playOnAwake = false;
        heartbeatAudioSource.loop = true;
        heartbeatAudioSource.spatialBlend = 0f;
    }

    private void PlayHeartbeatSound()
    {
        if (heartbeatSound == null)
            return;

        EnsureHeartbeatAudioSource();
        heartbeatAudioSource.clip = heartbeatSound;
        heartbeatAudioSource.Play();
    }

    private void StopHeartbeatSound()
    {
        if (heartbeatAudioSource != null)
            heartbeatAudioSource.Stop();
    }

    private void EnsureJumpscareAudioSource()
    {
        if (jumpscareAudioSource != null)
            return;

        jumpscareAudioSource = gameObject.AddComponent<AudioSource>();
        jumpscareAudioSource.playOnAwake = false;
        jumpscareAudioSource.loop = false;
        jumpscareAudioSource.spatialBlend = 0f;
    }

    private void PlayJumpscareSound()
    {
        StopHeartbeatSound();

        if (jumpscareSound == null)
            return;

        EnsureJumpscareAudioSource();
        jumpscareAudioSource.clip = jumpscareSound;
        jumpscareAudioSource.Play();
    }

    private void StopJumpscareSound()
    {
        if (jumpscareAudioSource != null)
            jumpscareAudioSource.Stop();
    }

    private void RestoreInputStateBeforeRetry()
    {
        Time.timeScale = 1f;
        SettingPanelWorldInputBlocker.End();
    }

    private IEnumerator FullJumpscareSequence()
    {
        isBlinkSequenceRunning = true;

        yield return StartCoroutine(BlinkRepeated(Mathf.Max(1, initialBlinkCount)));

        PrepareSecondFrameAtCenter();
        ShowJumpscareAnimatorFrameAtTopLayer(secondFrameTime);

        Vector3 darkenCenter = triggerObject != null ? triggerObject.transform.position : Vector3.zero;
        yield return StartCoroutine(DarkenFromWorldPoint(darkenCenter, horrorEffectDuration));

        StartHorrorEffectNow();
        if (blackScreenShakeDuration > 0f)
            yield return new WaitForSeconds(blackScreenShakeDuration);

        yield return StartCoroutine(AnimateBlink(0.5f, 0f, 0f, 2.0f, blinkDuration));
        ShowJumpscareAnimatorFrameAtTopLayer(fourthFrameTime);
        StartHorrorEffectNow(playJumpscareSound: false);
        yield return new WaitForSeconds(closedDuration);

        yield return StartCoroutine(AnimateBlink(0f, 0.5f, 2.0f, 0f, blinkDuration));

        if (finalFrameHoldDuration > 0f)
            yield return new WaitForSeconds(finalFrameHoldDuration);

        isBlinkSequenceRunning = false;
        if (jumpscareAnimator != null)
            jumpscareAnimator.speed = 0f;
        isFinishControlledBySequence = false;
        OnJumpscareFinished();
    }

    private void PrepareSecondFrameAtCenter()
    {
        SetTriggerVisible(false);

        if (triggerObject != null)
            triggerObject.transform.position = GetScreenCenterWorldPosition(triggerObject.transform.position.z);
        if (jumpscareAnimator != null)
            jumpscareAnimator.transform.position = GetScreenCenterWorldPosition(jumpscareAnimator.transform.position.z);
    }

    private IEnumerator BlinkOnce()
    {
        yield return StartCoroutine(AnimateBlink(0.5f, 0f, 0f, 2.0f, blinkDuration));
        yield return new WaitForSeconds(closedDuration);
        yield return StartCoroutine(AnimateBlink(0f, 0.5f, 2.0f, 0f, blinkDuration));
    }

    private IEnumerator BlinkRepeated(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return StartCoroutine(BlinkOnce());
            if (i < count - 1 && blinkInterval > 0f)
                yield return new WaitForSeconds(blinkInterval);
        }
    }

    private void ShowJumpscareAnimatorFrameAtTopLayer(float clipTime)
    {
        if (jumpscareAnimator == null)
            return;

        jumpscareAnimator.gameObject.SetActive(true);
        jumpscareAnimator.enabled = true;
        jumpscareAnimator.speed = 0f;
        RaiseAnimatorRenderersAboveDarkOverlay();
        jumpscareAnimator.Rebind();
        SetAnimatorClipTime(clipTime);
    }

    private void SetAnimatorClipTime(float clipTime)
    {
        RuntimeAnimatorController controller = jumpscareAnimator.runtimeAnimatorController;
        float clipLength = 1f;
        if (controller != null && controller.animationClips != null && controller.animationClips.Length > 0)
            clipLength = Mathf.Max(0.0001f, controller.animationClips[0].length);

        float normalizedTime = Mathf.Clamp01(clipTime / clipLength);
        jumpscareAnimator.Play(0, 0, normalizedTime);
        jumpscareAnimator.Update(0f);
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
        if (!isJumpscareInProgress)
            return;
        if (isFinishControlledBySequence)
            return;

        if (jumpscareAnimator != null)
        {
            jumpscareAnimator.speed = 1f;
            jumpscareAnimator.enabled = true;
            jumpscareAnimator.gameObject.SetActive(false);
        }
        HideDarkOverlay();
        if (gameOverObject != null) gameOverObject.SetActive(true);
        
        // 만약을 대비한 포스트 프로세싱 초기화 보장
        if (vignette != null) vignette.intensity.value = 0f;
        if (lensDistortion != null) lensDistortion.intensity.value = 0f;

        OnPlayerDied?.Invoke();
        OnJumpscareReset?.Invoke();
        ChromaticOff();

        isJumpscareInProgress = false;
        isFinishControlledBySequence = false;
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

    private void SetOtherSceneRootObjectsVisible(bool visible)
    {
        if (!visible)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root == null || !root.activeSelf || ShouldPreserveRootObject(root))
                    continue;

                root.SetActive(false);
                if (!hiddenRootObjects.Contains(root))
                    hiddenRootObjects.Add(root);
            }

            return;
        }

        for (int i = 0; i < hiddenRootObjects.Count; i++)
        {
            GameObject root = hiddenRootObjects[i];
            if (root != null)
                root.SetActive(true);
        }

        hiddenRootObjects.Clear();
    }

    private bool ShouldPreserveRootObject(GameObject root)
    {
        if (root == gameObject)
            return true;

        if (IsSayDialogRoot(root))
            return true;

        if (root.CompareTag(MainCanvasTag))
            return true;

        if (root.name == BackgroundImageObjectName)
            return true;

        if (root.name == No40UiHooksObjectName)
            return true;

        if (root.GetComponent<Flowchart>() != null)
            return true;

        Camera activeCamera = Camera.main;
        if (activeCamera != null && root == activeCamera.gameObject)
            return true;

        if (globalVolume != null && root == globalVolume.gameObject)
            return true;

        return root.GetComponent<UnityEngine.EventSystems.EventSystem>() != null;
    }

    private static bool IsSayDialogRoot(GameObject root)
    {
        return root.GetComponent<SayDialog>() != null
            || root.name == "SayDialog"
            || root.name == "SayDialogNotebook";
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

    private IEnumerator DarkenFromWorldPoint(Vector3 centerWorld, float duration)
    {
        EnsureDarkOverlay();
        if (darkOverlay == null)
            yield break;

        Camera cam = Camera.main;
        if (cam == null)
            yield break;

        darkOverlay.gameObject.SetActive(true);
        darkOverlay.transform.position = new Vector3(centerWorld.x, centerWorld.y, cam.transform.position.z + OverlayPlaneZOffsetFromCamera);
        float startSize = Mathf.Max(0.001f, darkOverlayStartSize);
        darkOverlay.transform.localScale = new Vector3(startSize, startSize, 1f);
        darkOverlay.color = Color.black;

        float targetDiameter = CalculateScreenCoveringDiameter(centerWorld, cam);
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            float size = Mathf.Lerp(startSize, targetDiameter, t);
            darkOverlay.transform.localScale = new Vector3(size, size, 1f);
            yield return null;
        }

        darkOverlay.transform.localScale = new Vector3(targetDiameter, targetDiameter, 1f);
    }

    private void EnsureDarkOverlay()
    {
        if (darkOverlay != null)
            return;

        GameObject overlayObject = new GameObject("SpecialJumpscareCenterDarkOverlay");
        DontDestroyOnLoad(overlayObject);
        darkOverlay = overlayObject.AddComponent<SpriteRenderer>();
        darkOverlaySprite = CreateDarkOverlaySprite();
        darkOverlay.sprite = darkOverlaySprite;
        darkOverlayMaterial = CreateDarkOverlayMaterial();
        if (darkOverlayMaterial != null)
            darkOverlay.material = darkOverlayMaterial;
        darkOverlay.sortingLayerID = blinkOverlay != null ? blinkOverlay.sortingLayerID : 0;
        darkOverlay.sortingOrder = DarkOverlaySortingOrder;
        darkOverlay.gameObject.SetActive(false);
    }

    private static Sprite CreateDarkOverlaySprite()
    {
        Texture2D texture = new Texture2D(DarkOverlayTextureSize, DarkOverlayTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (DarkOverlayTextureSize - 1) * 0.5f;
        float radius = center - DarkOverlaySoftEdgePixels;
        Color[] pixels = new Color[DarkOverlayTextureSize * DarkOverlayTextureSize];

        for (int y = 0; y < DarkOverlayTextureSize; y++)
        {
            for (int x = 0; x < DarkOverlayTextureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = 1f - Mathf.Clamp01((distance - radius) / DarkOverlaySoftEdgePixels);
                pixels[y * DarkOverlayTextureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, DarkOverlayTextureSize, DarkOverlayTextureSize), new Vector2(0.5f, 0.5f), DarkOverlayTextureSize);
    }

    private static Material CreateDarkOverlayMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        return shader != null ? new Material(shader) : null;
    }

    private void HideDarkOverlay()
    {
        if (darkOverlay != null)
            darkOverlay.gameObject.SetActive(false);
    }

    private static float CalculateScreenCoveringDiameter(Vector3 centerWorld, Camera cam)
    {
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth = worldHeight * cam.aspect;
        Vector3 camPos = cam.transform.position;

        Vector2[] corners =
        {
            new Vector2(camPos.x - worldWidth * 0.5f, camPos.y - worldHeight * 0.5f),
            new Vector2(camPos.x - worldWidth * 0.5f, camPos.y + worldHeight * 0.5f),
            new Vector2(camPos.x + worldWidth * 0.5f, camPos.y - worldHeight * 0.5f),
            new Vector2(camPos.x + worldWidth * 0.5f, camPos.y + worldHeight * 0.5f)
        };

        float maxDistance = 0f;
        Vector2 center = centerWorld;
        foreach (Vector2 corner in corners)
            maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, corner));

        return maxDistance * 2.1f;
    }

    private void RaiseAnimatorRenderersAboveDarkOverlay()
    {
        if (jumpscareAnimator == null)
            return;

        int sortingLayerID = darkOverlay != null
            ? darkOverlay.sortingLayerID
            : (blinkOverlay != null ? blinkOverlay.sortingLayerID : 0);

        Renderer[] renderers = jumpscareAnimator.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.sortingLayerID = sortingLayerID;
            renderer.sortingOrder = JumpscareTopSortingOrder;
        }
    }
}
