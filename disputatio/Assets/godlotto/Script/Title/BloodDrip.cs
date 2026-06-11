using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates one blood drip: attached streak growth, tip droplet detach/fall, or a simple falling drop.
/// Timing and visuals follow <c>oozeFrom</c>, <c>dripFrom</c>, and <c>impact</c> in
/// <c>docs/blood-drip-title-final.html</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class BloodDrip : MonoBehaviour
{
    public enum Mode
    {
        Ooze,
        Drop,
        FreeFall = Drop,
    }

    public struct AnimationRequest
    {
        public Mode Mode;
        public float StreakLength;
        public float PoolGrowAmount;
        public bool SpawnLocalSplashes;
        public float HangDelaySeconds;
        public float FallDurationSeconds;
    }

    public static float CubicBezierY(float t, float p1y, float p2y, float p3y, float p4y)
    {
        float u = 1f - t;
        return 3f * u * u * t * p2y + 3f * u * t * t * p3y + t * t * t * p4y;
    }

    public static AnimationRequest CreateOozeRequest(
        Vector2 anchor,
        float floorY,
        float streakLength,
        float horizontalOffset,
        BloodDripPalette palette)
    {
        return new AnimationRequest
        {
            Mode = Mode.Ooze,
            StreakLength = streakLength,
            PoolGrowAmount = BloodDripTiming.OozePoolGrowAmount,
            SpawnLocalSplashes = true,
            HangDelaySeconds = BloodDripTiming.OozeDetachDelaySeconds,
            FallDurationSeconds = BloodDripTiming.OozeFallSeconds,
        };
    }

    public static AnimationRequest CreateFreeFallRequest(
        Vector2 anchor,
        float floorY,
        float dropSize,
        float hangDelaySeconds,
        float fallDurationSeconds,
        BloodDripPalette palette,
        float poolGrowAmount)
    {
        return new AnimationRequest
        {
            Mode = Mode.FreeFall,
            PoolGrowAmount = poolGrowAmount,
            HangDelaySeconds = hangDelaySeconds,
            FallDurationSeconds = fallDurationSeconds,
            SpawnLocalSplashes = true,
        };
    }

    [SerializeField] RectTransform streakRoot;
    [SerializeField] Image streakImage;
    [SerializeField] RectTransform tipRoot;
    [SerializeField] Image tipImage;
    [SerializeField] RectTransform dropRoot;
    [SerializeField] Image dropImage;

    BloodPool pool;
    Transform container;
    Coroutine playRoutine;
    BloodDripPlayRequest activeRequest;
    float intensityScale = 1f;
    System.Random random;

    public bool IsPlaying { get; private set; }

    public event Action<BloodDrip> Finished;

    public static BloodDrip Spawn(Transform parent, BloodPool targetPool)
    {
        var root = new GameObject("BloodDrip", typeof(RectTransform), typeof(BloodDrip));
        root.transform.SetParent(parent, false);

        var drip = root.GetComponent<BloodDrip>();
        drip.pool = targetPool;
        drip.container = parent;
        drip.EnsureVisuals();
        return drip;
    }

    void EnsureVisuals()
    {
        if (streakRoot == null)
        {
            streakRoot = BloodDripUiHelper.CreateChildRect(transform, "Streak");
            streakImage = BloodDripUiHelper.CreateImage(streakRoot, Color.white);
            streakRoot.gameObject.SetActive(false);
        }

        if (tipRoot == null)
        {
            tipRoot = BloodDripUiHelper.CreateChildRect(transform, "Tip");
            tipImage = BloodDripUiHelper.CreateImage(tipRoot, Color.white);
            tipRoot.gameObject.SetActive(false);
        }

        if (dropRoot == null)
        {
            dropRoot = BloodDripUiHelper.CreateChildRect(transform, "Drop");
            dropImage = BloodDripUiHelper.CreateImage(dropRoot, Color.white);
            dropRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>Legacy entry point used by <see cref="BloodDripTitleRenderer"/>.</summary>
    public void Play(
        Mode mode,
        Vector2 anchor,
        float streakLength,
        float dropSize,
        float targetFloorY,
        Color main,
        Color dark,
        Color bright,
        float growDuration,
        float fallDuration)
    {
        var request = mode == Mode.Ooze
            ? BloodDripPlayRequest.CreateAttachedStreak(anchor, targetFloorY, main, bright, dark)
            : BloodDripPlayRequest.CreateSimpleDrop(anchor, targetFloorY, main, bright, dark);

        request.StreakLength = streakLength;
        if (mode == Mode.Drop && dropSize > 0f)
            request.HorizontalOffset = dropSize;

        PlayInternal(request, growDuration, fallDuration, dropSize);
    }

    /// <summary>Request-based entry point for explicit spawn parameters and callbacks.</summary>
    public void Play(BloodDripPlayRequest request, BloodPool targetPool = null)
    {
        if (targetPool != null)
            pool = targetPool;

        float growDuration = request.GrowDurationSeconds > 0f
            ? request.GrowDurationSeconds
            : BloodDripDefaults.GrowDuration;
        float fallDuration = request.FallDurationSeconds > 0f ? request.FallDurationSeconds : 0f;
        float dropSize = request.DropSize > 0f ? request.DropSize : 0f;
        PlayInternal(request, growDuration, fallDuration, dropSize);
    }

    void PlayInternal(BloodDripPlayRequest request, float growDuration, float fallDuration, float explicitDropSize)
    {
        StopPlayback();
        EnsureVisuals();

        activeRequest = request;
        intensityScale = BloodDripDefaults.EvaluateIntensityScale(request.IntensityScale);
        random = request.RandomSource;

        var rootRect = (RectTransform)transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = request.AnchorLocalPosition;
        rootRect.localScale = Vector3.one;

        ResetVisuals();

        playRoutine = StartCoroutine(request.Style == BloodDripStyle.AttachedStreak
            ? OozeRoutine(request, growDuration, fallDuration)
            : DropRoutine(request, fallDuration, explicitDropSize));
    }

    public void StopPlayback()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        IsPlaying = false;
    }

    void ResetVisuals()
    {
        streakRoot.gameObject.SetActive(false);
        tipRoot.gameObject.SetActive(false);
        dropRoot.gameObject.SetActive(false);
    }

    IEnumerator OozeRoutine(BloodDripPlayRequest request, float growDuration, float fallDuration)
    {
        IsPlaying = true;

        float streakLength = BloodDripDefaults.ResolveStreakLength(request.StreakLength, random) * intensityScale;
        float horizontalOffset = BloodDripDefaults.ResolveHorizontalOffset(request.HorizontalOffset, random);
        float growSeconds = growDuration > 0f ? growDuration : BloodDripDefaults.GrowDuration;
        float detachFallSeconds = fallDuration > 0f ? fallDuration : BloodDripDefaults.DetachFallDuration;
        float poolContribution = BloodDripDefaults.ComputeAttachedPoolContribution(intensityScale);

        ConfigureStreak(streakLength, request.DarkColor, request.BrightColor);
        ConfigureTip(request.MainColor, request.BrightColor);

        streakRoot.gameObject.SetActive(true);
        tipRoot.gameObject.SetActive(true);

        float topInset = BloodDripDefaults.FloorImpactOffset;
        streakRoot.anchoredPosition = new Vector2(horizontalOffset, -topInset);
        tipRoot.anchoredPosition = new Vector2(
            horizontalOffset - BloodDripDefaults.TipWidth * 0.5f,
            -topInset);

        float growElapsed = 0f;
        while (growElapsed < growSeconds)
        {
            growElapsed += Time.deltaTime;
            float t = BloodDripDefaults.GrowEase.Evaluate(Mathf.Clamp01(growElapsed / growSeconds));
            streakRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, streakLength * t);
            tipRoot.anchoredPosition = new Vector2(
                horizontalOffset - BloodDripDefaults.TipWidth * 0.5f,
                -topInset - streakLength * t);
            yield return null;
        }

        streakRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, streakLength);
        float tipEndY = -topInset - streakLength;
        tipRoot.anchoredPosition = new Vector2(
            horizontalOffset - BloodDripDefaults.TipWidth * 0.5f,
            tipEndY);

        float waitBeforeDetach = Mathf.Max(0f, BloodDripDefaults.HoldBeforeDetach - growSeconds);
        if (waitBeforeDetach > 0f)
            yield return new WaitForSeconds(waitBeforeDetach);

        float fallDistance = request.FloorLocalY - ((RectTransform)transform).anchoredPosition.y + tipEndY - BloodDripDefaults.FloorImpactOffset;
        if (fallDistance > 4f)
        {
            var tipCanvas = BloodDripUiHelper.GetOrAddCanvasGroup(tipImage.gameObject);
            tipCanvas.alpha = 1f;
            Color tipStart = tipImage.color;

            float fallElapsed = 0f;
            while (fallElapsed < detachFallSeconds)
            {
                fallElapsed += Time.deltaTime;
                float t = BloodDripDefaults.FallEase.Evaluate(Mathf.Clamp01(fallElapsed / detachFallSeconds));
                tipRoot.anchoredPosition = new Vector2(
                    horizontalOffset - BloodDripDefaults.TipWidth * 0.5f,
                    Mathf.Lerp(tipEndY, tipEndY - fallDistance, t));

                if (fallElapsed >= 0.25f)
                {
                    float fadeT = Mathf.Clamp01((fallElapsed - 0.25f) / 0.3f);
                    tipCanvas.alpha = 1f - fadeT;
                    tipImage.color = new Color(tipStart.r, tipStart.g, tipStart.b, 1f - fadeT);
                }

                yield return null;
            }

            Vector2 impactPosition = BloodDripDefaults.ComputeImpactPosition(
                new Vector2(((RectTransform)transform).anchoredPosition.x + horizontalOffset, request.AnchorLocalPosition.y),
                request.FloorLocalY);
            NotifyImpact(impactPosition, poolContribution, BloodDripDefaults.TipHeight, BloodDripStyle.AttachedStreak);
        }

        tipRoot.gameObject.SetActive(false);

        float fadeElapsed = 0f;
        Color streakStart = streakImage.color;
        while (fadeElapsed < BloodDripDefaults.StreakFadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(fadeElapsed / BloodDripDefaults.StreakFadeDuration);
            streakImage.color = new Color(streakStart.r, streakStart.g, streakStart.b, alpha);
            yield return null;
        }

        float cleanupDelay = Mathf.Max(
            0f,
            BloodDripDefaults.PostDetachCleanupDelay - BloodDripDefaults.StreakFadeDuration);
        if (cleanupDelay > 0f)
            yield return new WaitForSeconds(cleanupDelay);

        CompletePlayback();
    }

    IEnumerator DropRoutine(BloodDripPlayRequest request, float fallDuration, float explicitDropSize)
    {
        IsPlaying = true;

        float dropSize = explicitDropSize > 0f
            ? explicitDropSize
            : BloodDripDefaults.ComputeSimpleDropSize(random, intensityScale);
        float fallSeconds = fallDuration > 0f
            ? fallDuration
            : BloodDripDefaults.SampleRange(random, BloodDripDefaults.SimpleDropFallMin, BloodDripDefaults.SimpleDropFallMax);
        float hangDelay = BloodDripDefaults.SampleRange(
            random,
            BloodDripDefaults.SimpleDropWaitMin,
            BloodDripDefaults.SimpleDropWaitMax);

        dropRoot.gameObject.SetActive(true);
        dropImage.color = Color.Lerp(request.MainColor, request.BrightColor, 0.25f);
        dropRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dropSize);
        dropRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dropSize);
        dropRoot.anchoredPosition = new Vector2(-dropSize * 0.5f, -2f);
        dropRoot.localScale = Vector3.zero;

        float popElapsed = 0f;
        while (popElapsed < BloodDripDefaults.SimpleDropScaleInDuration)
        {
            popElapsed += Time.deltaTime;
            float t = BloodDripDefaults.PopInEase.Evaluate(
                Mathf.Clamp01(popElapsed / BloodDripDefaults.SimpleDropScaleInDuration));
            dropRoot.localScale = Vector3.one * t;
            yield return null;
        }

        dropRoot.localScale = Vector3.one;

        if (hangDelay > 0f)
            yield return new WaitForSeconds(hangDelay);

        float startY = dropRoot.anchoredPosition.y;
        float endY = request.FloorLocalY - ((RectTransform)transform).anchoredPosition.y - BloodDripDefaults.SimpleDropFallMargin;
        float fallDistance = startY - endY;

        if (fallDistance > 1f)
        {
            float fallElapsed = 0f;
            while (fallElapsed < fallSeconds)
            {
                fallElapsed += Time.deltaTime;
                float t = BloodDripDefaults.FallEase.Evaluate(Mathf.Clamp01(fallElapsed / fallSeconds));
                dropRoot.anchoredPosition = new Vector2(dropRoot.anchoredPosition.x, Mathf.Lerp(startY, endY, t));
                yield return null;
            }
        }

        Vector2 impactPosition = BloodDripDefaults.ComputeImpactPosition(
            new Vector2(((RectTransform)transform).anchoredPosition.x, request.AnchorLocalPosition.y),
            request.FloorLocalY);
        NotifyImpact(impactPosition, dropSize, dropSize, BloodDripStyle.SimpleDrop);

        dropRoot.gameObject.SetActive(false);
        CompletePlayback();
    }

    void ConfigureStreak(float streakLength, Color dark, Color bright)
    {
        streakRoot.pivot = new Vector2(0.5f, 1f);
        streakRoot.anchorMin = streakRoot.anchorMax = new Vector2(0.5f, 1f);
        streakRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BloodDripDefaults.StreakWidth);
        streakRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
        streakImage.color = Color.Lerp(dark, bright, 0.35f);
    }

    void ConfigureTip(Color main, Color bright)
    {
        tipRoot.pivot = new Vector2(0f, 1f);
        tipRoot.anchorMin = tipRoot.anchorMax = new Vector2(0.5f, 1f);
        tipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BloodDripDefaults.TipWidth);
        tipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BloodDripDefaults.TipHeight);
        tipImage.color = Color.Lerp(main, bright, 0.4f);
    }

    void NotifyImpact(Vector2 containerLocalPosition, float poolContribution, float dropSize, BloodDripStyle style)
    {
        var info = new BloodDripImpactInfo
        {
            LocalPosition = containerLocalPosition,
            PoolContribution = poolContribution,
            DropSize = dropSize,
            Style = style,
        };

        activeRequest.ImpactCallback?.Invoke(info);

        if (pool != null)
            pool.PlayImpact(containerLocalPosition, poolContribution);
    }

    void CompletePlayback()
    {
        IsPlaying = false;
        playRoutine = null;
        ResetVisuals();
        Finished?.Invoke(this);
        Destroy(gameObject);
    }
}
