using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Full-screen blood flood layer that rises from the floor without replacing <see cref="BloodPool"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BloodFloodOverlayGraphic))]
public sealed class BloodFloodOverlay : MonoBehaviour
{
    public enum LayerOrder
    {
        BehindTitle = 0,
        AboveTitle = 1,
        AboveDrips = 2,
    }

    [Header("Layering")]
    [SerializeField] LayerOrder layerOrder = LayerOrder.AboveTitle;

    [Header("Visual")]
    [SerializeField] BloodFloodOverlayGraphic graphic;
    [SerializeField] Material overlayMaterial;
    [SerializeField] Color bloodColor = new Color(0.29f, 0.02f, 0.02f, 1f);
    [SerializeField] Color edgeColor = new Color(0.12f, 0.01f, 0.01f, 1f);
    [SerializeField] float waveAmplitude = 10f;
    [SerializeField] float waveFrequency = 2.4f;
    [SerializeField] float maxAlpha = 0.9f;
    [SerializeField] float minAlpha = 0.28f;

    [Header("Impact fill")]
    [SerializeField] float fillPerImpact = 0.01f;
    [SerializeField] float impactRiseDuration = 1.25f;
    [SerializeField] float simpleDropFillMultiplier = 1f;
    [SerializeField] float attachedStreakFillMultiplier = 1.1f;
    [SerializeField] float maxFillAmount = 1f;

    [Header("Full settle")]
    [SerializeField] float fullThreshold = 0.98f;
    [SerializeField] float fullSettleDuration = 1f;
    [SerializeField] float fullWaveAmplitudeMultiplier = 1.8f;
    [SerializeField] Color finalBloodColor = new Color(0.12f, 0.01f, 0.01f, 1f);
    [SerializeField] Color finalEdgeColor = new Color(0.05f, 0.005f, 0.005f, 1f);
    [SerializeField] float finalMaxAlpha = 0.96f;
    [SerializeField] CanvasGroup contentToSubmerge;
    [SerializeField] float contentFadeDuration = 0.5f;
    [SerializeField] float contentSubmergedAlpha = 0.18f;
    [SerializeField] UnityEvent onFloodFull;

    [Header("Auto flood (demo / test)")]
    [SerializeField] float fillAmount;
    [SerializeField] float floodDuration = 18f;
    [SerializeField] float startDelay = 1.2f;
    [SerializeField] bool autoPlayOnEnable;
    [SerializeField] AnimationCurve floodCurve = DefaultFloodCurve();
    [SerializeField] float noiseDriftSpeed = 0.08f;

    Coroutine floodRoutine;
    Coroutine fillRiseRoutine;
    Coroutine fullSettleRoutine;
    Coroutine contentFadeRoutine;
    bool isPlaying;
    bool hasTriggeredFull;

    float targetFillAmount;
    float riseStartFill;
    float riseElapsed;
    float riseDuration;

    Color baselineBloodColor;
    Color baselineEdgeColor;
    float baselineWaveAmplitude;
    float baselineMaxAlpha;
    float baselineNoiseDriftSpeed;
    float baselineContentAlpha = 1f;
    bool hasBaselineContentAlpha;

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            fillAmount = Mathf.Clamp(value, 0f, maxFillAmount);
            targetFillAmount = Mathf.Max(targetFillAmount, fillAmount);
            ApplyVisualState();
            TryTriggerFullIfThresholdMet();
        }
    }

    public float TargetFillAmount => targetFillAmount;

    public bool IsFull { get; private set; }

    public LayerOrder RenderLayerOrder => layerOrder;

    public bool IsPlaying => isPlaying;

    public event Action FloodFull;

    void Reset()
    {
        graphic = GetComponent<BloodFloodOverlayGraphic>();
    }

    void Awake()
    {
        EnsureGraphic();
        CaptureVisualBaseline();
        ApplyVisualState();
    }

    void OnEnable()
    {
        ApplyLayerOrder();
        if (autoPlayOnEnable && Application.isPlaying)
            Play();
    }

    void OnDisable()
    {
        StopFlood();
        StopFillRise();
        StopFullSettle();
        StopContentFade();
    }

    void OnValidate()
    {
        if (graphic == null)
            graphic = GetComponent<BloodFloodOverlayGraphic>();

        maxFillAmount = Mathf.Clamp01(maxFillAmount);
        fullThreshold = Mathf.Clamp(fullThreshold, 0f, maxFillAmount);
        fillAmount = Mathf.Clamp(fillAmount, 0f, maxFillAmount);
        targetFillAmount = Mathf.Clamp(targetFillAmount, fillAmount, maxFillAmount);
        finalMaxAlpha = Mathf.Clamp01(finalMaxAlpha);
        contentFadeDuration = Mathf.Max(0.01f, contentFadeDuration);
        contentSubmergedAlpha = Mathf.Clamp01(contentSubmergedAlpha);
        fullSettleDuration = Mathf.Max(0.01f, fullSettleDuration);
        ApplyVisualState();
        ApplyLayerOrder();
    }

    public void ConfigureFromPayload(TitleStylePayload payload)
    {
        if (payload == null)
            return;

        bloodColor = Color.Lerp(payload.DarkColor, payload.Color, 0.35f);
        edgeColor = Color.Lerp(payload.DarkColor, Color.black, 0.25f);
        finalBloodColor = Color.Lerp(payload.DarkColor, Color.black, 0.35f);
        finalEdgeColor = Color.Lerp(payload.DarkColor, Color.black, 0.55f);
        CaptureVisualBaseline();
        ApplyVisualState();
    }

    public void ResetFlood()
    {
        StopFlood();
        StopFillRise();
        StopFullSettle();
        StopContentFade();
        ResetFullState();
        RestoreVisualBaseline();
        fillAmount = 0f;
        targetFillAmount = 0f;
        ApplyVisualState();
    }

    /// <summary>
    /// Adds fill from a blood drip floor impact and animates toward the new target over
    /// <see cref="impactRiseDuration"/> (or the supplied duration).
    /// </summary>
    public void AddFillFromImpact(BloodDripImpactInfo info)
    {
        AddFillOverTime(ComputeImpactFillAmount(info), impactRiseDuration);
    }

    /// <summary>
    /// Raises <see cref="FillAmount"/> from its current value toward a new cumulative target
    /// over <paramref name="duration"/> seconds. Repeated calls accumulate the target without
    /// decreasing fill.
    /// </summary>
    public void AddFillOverTime(float amount, float duration = -1f)
    {
        if (amount <= 0f || !isActiveAndEnabled)
            return;

        float riseSeconds = duration > 0f ? duration : impactRiseDuration;
        riseSeconds = Mathf.Max(0.01f, riseSeconds);

        float nextTarget = Mathf.Clamp(
            Mathf.Max(targetFillAmount, fillAmount) + amount,
            fillAmount,
            maxFillAmount);

        if (Mathf.Approximately(nextTarget, fillAmount))
            return;

        targetFillAmount = nextTarget;
        riseStartFill = fillAmount;
        riseElapsed = 0f;
        riseDuration = riseSeconds;

        if (fillRiseRoutine == null)
            fillRiseRoutine = StartCoroutine(FillRiseRoutine());
    }

    public float ComputeImpactFillAmount(BloodDripImpactInfo info)
    {
        float styleMultiplier = info.Style == BloodDripStyle.AttachedStreak
            ? attachedStreakFillMultiplier
            : simpleDropFillMultiplier;

        float amount = fillPerImpact * styleMultiplier;

        const float referenceSize = 12f;
        float sizeBlend = Mathf.Clamp01((info.PoolContribution * 0.5f + info.DropSize) / referenceSize);
        amount *= Mathf.Lerp(0.95f, 1.05f, sizeBlend);

        float maxPerImpact = fillPerImpact * Mathf.Max(attachedStreakFillMultiplier, simpleDropFillMultiplier) * 1.25f;
        return Mathf.Min(amount, maxPerImpact);
    }

    /// <summary>
    /// Returns true only the first time <see cref="fillAmount"/> crosses <see cref="fullThreshold"/>.
    /// </summary>
    public bool TryTriggerFullIfThresholdMet()
    {
        if (hasTriggeredFull || fillAmount < fullThreshold)
            return false;

        hasTriggeredFull = true;
        IsFull = true;
        onFloodFull?.Invoke();
        FloodFull?.Invoke();

        if (isActiveAndEnabled)
            fullSettleRoutine = StartCoroutine(PlayFullSettleRoutine());

        return true;
    }

    public void Play()
    {
        if (!isActiveAndEnabled)
            return;

        StopFlood();
        floodRoutine = StartCoroutine(FloodRoutine());
    }

    public void ResetAndPlay()
    {
        ResetFlood();
        Play();
    }

    public void StopFlood()
    {
        if (floodRoutine != null)
        {
            StopCoroutine(floodRoutine);
            floodRoutine = null;
        }

        isPlaying = false;
    }

    void StopFillRise()
    {
        if (fillRiseRoutine != null)
        {
            StopCoroutine(fillRiseRoutine);
            fillRiseRoutine = null;
        }
    }

    void StopFullSettle()
    {
        if (fullSettleRoutine != null)
        {
            StopCoroutine(fullSettleRoutine);
            fullSettleRoutine = null;
        }
    }

    void StopContentFade()
    {
        if (contentFadeRoutine != null)
        {
            StopCoroutine(contentFadeRoutine);
            contentFadeRoutine = null;
        }
    }

    void ResetFullState()
    {
        hasTriggeredFull = false;
        IsFull = false;
    }

    void CaptureVisualBaseline()
    {
        baselineBloodColor = bloodColor;
        baselineEdgeColor = edgeColor;
        baselineWaveAmplitude = waveAmplitude;
        baselineMaxAlpha = maxAlpha;
        baselineNoiseDriftSpeed = noiseDriftSpeed;
    }

    void RestoreVisualBaseline()
    {
        bloodColor = baselineBloodColor;
        edgeColor = baselineEdgeColor;
        waveAmplitude = baselineWaveAmplitude;
        maxAlpha = baselineMaxAlpha;
        noiseDriftSpeed = baselineNoiseDriftSpeed;

        if (contentToSubmerge != null && hasBaselineContentAlpha)
            contentToSubmerge.alpha = baselineContentAlpha;
    }

    bool ShouldSubmergeContent()
    {
        return contentToSubmerge != null
            && (layerOrder == LayerOrder.AboveTitle || layerOrder == LayerOrder.AboveDrips);
    }

    IEnumerator PlayFullSettleRoutine()
    {
        float duration = Mathf.Max(0.01f, fullSettleDuration);
        float elapsed = 0f;
        float peakWave = baselineWaveAmplitude * fullWaveAmplitudeMultiplier;
        float settledWave = baselineWaveAmplitude * 0.82f;
        float peakNoise = baselineNoiseDriftSpeed * 2.2f;
        float settledNoise = baselineNoiseDriftSpeed * 0.45f;

        if (ShouldSubmergeContent())
        {
            if (!hasBaselineContentAlpha)
            {
                baselineContentAlpha = contentToSubmerge.alpha;
                hasBaselineContentAlpha = true;
            }

            contentFadeRoutine = StartCoroutine(FadeContentRoutine());
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float swell = Mathf.Sin(t * Mathf.PI);
            float settle = SmoothStep(t);

            waveAmplitude = Mathf.Lerp(settledWave, peakWave, swell * (1f - settle * 0.35f));
            maxAlpha = Mathf.Lerp(baselineMaxAlpha, finalMaxAlpha, settle);
            bloodColor = Color.Lerp(baselineBloodColor, finalBloodColor, settle);
            edgeColor = Color.Lerp(baselineEdgeColor, finalEdgeColor, settle);
            noiseDriftSpeed = Mathf.Lerp(settledNoise, peakNoise, swell * (1f - settle * 0.25f));

            graphic.NoisePhase += Time.deltaTime * noiseDriftSpeed;
            ApplyVisualState();
            yield return null;
        }

        waveAmplitude = settledWave;
        maxAlpha = finalMaxAlpha;
        bloodColor = finalBloodColor;
        edgeColor = finalEdgeColor;
        noiseDriftSpeed = settledNoise;
        fillAmount = maxFillAmount;
        ApplyVisualState();
        fullSettleRoutine = null;
    }

    IEnumerator FadeContentRoutine()
    {
        float duration = Mathf.Max(0.01f, contentFadeDuration);
        float startAlpha = contentToSubmerge.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothStep(Mathf.Clamp01(elapsed / duration));
            contentToSubmerge.alpha = Mathf.Lerp(startAlpha, contentSubmergedAlpha, t);
            yield return null;
        }

        contentToSubmerge.alpha = contentSubmergedAlpha;
        contentFadeRoutine = null;
    }

    public void ApplyLayerOrder()
    {
        if (transform.parent == null)
            return;

        Transform title = transform.parent.Find("TMP_TitleText");
        if (title == null)
            title = transform.parent.Find("Title");

        switch (layerOrder)
        {
            case LayerOrder.BehindTitle:
                transform.SetSiblingIndex(0);
                break;
            case LayerOrder.AboveTitle:
                if (title != null)
                    transform.SetSiblingIndex(title.GetSiblingIndex() + 1);
                else
                    transform.SetSiblingIndex(1);
                break;
            case LayerOrder.AboveDrips:
                transform.SetAsLastSibling();
                break;
        }
    }

    IEnumerator FillRiseRoutine()
    {
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(riseElapsed / riseDuration);
            float nextFill = Mathf.Lerp(riseStartFill, targetFillAmount, t);
            fillAmount = Mathf.Max(fillAmount, nextFill);
            graphic.NoisePhase += Time.deltaTime * noiseDriftSpeed;
            ApplyVisualState();
            TryTriggerFullIfThresholdMet();
            yield return null;
        }

        fillAmount = Mathf.Max(fillAmount, targetFillAmount);
        ApplyVisualState();
        TryTriggerFullIfThresholdMet();
        fillRiseRoutine = null;
    }

    IEnumerator FloodRoutine()
    {
        isPlaying = true;
        fillAmount = 0f;
        targetFillAmount = 0f;
        ApplyVisualState();

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, floodDuration);
        AnimationCurve curve = floodCurve != null && floodCurve.length > 0
            ? floodCurve
            : DefaultFloodCurve();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            fillAmount = Mathf.Clamp(curve.Evaluate(normalizedTime), 0f, maxFillAmount);
            targetFillAmount = Mathf.Max(targetFillAmount, fillAmount);
            graphic.NoisePhase += Time.deltaTime * noiseDriftSpeed;
            ApplyVisualState();
            TryTriggerFullIfThresholdMet();
            yield return null;
        }

        fillAmount = maxFillAmount;
        targetFillAmount = maxFillAmount;
        ApplyVisualState();
        TryTriggerFullIfThresholdMet();
        isPlaying = false;
        floodRoutine = null;
    }

    void EnsureGraphic()
    {
        if (graphic != null)
            return;

        graphic = GetComponent<BloodFloodOverlayGraphic>();
        if (graphic == null)
            graphic = gameObject.AddComponent<BloodFloodOverlayGraphic>();
    }

    void ApplyVisualState()
    {
        EnsureGraphic();

        graphic.FillAmount = fillAmount;
        graphic.BloodColor = bloodColor;
        graphic.EdgeColor = edgeColor;
        graphic.WaveAmplitude = waveAmplitude;
        graphic.WaveFrequency = waveFrequency;
        graphic.MaxAlpha = maxAlpha;
        graphic.MinAlpha = minAlpha;
        graphic.raycastTarget = false;

        if (overlayMaterial != null)
            graphic.material = overlayMaterial;
    }

    static float SmoothStep(float t) => t * t * (3f - 2f * t);

    static AnimationCurve DefaultFloodCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0.15f),
            new Keyframe(0.3f, 0.12f, 0.35f, 0.65f),
            new Keyframe(0.65f, 0.48f, 1.1f, 1.35f),
            new Keyframe(1f, 1f, 1.6f, 0f));
    }

#if UNITY_EDITOR
    public void ConfigureForTests(
        float initialFill,
        float duration,
        float delay,
        bool autoPlay)
    {
        fillAmount = initialFill;
        targetFillAmount = initialFill;
        floodDuration = duration;
        startDelay = delay;
        autoPlayOnEnable = autoPlay;
        CaptureVisualBaseline();
        ApplyVisualState();
    }
#endif
}
