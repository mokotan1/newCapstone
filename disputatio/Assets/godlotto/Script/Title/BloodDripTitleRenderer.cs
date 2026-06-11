using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Applies backend title-style payloads to TMP and schedules blood drips from glyph lower anchors.
/// </summary>
public class BloodDripTitleRenderer : MonoBehaviour
{
    public struct GlyphAnchorInfo
    {
        public char Character;
        public Vector2 LocalPosition;
    }

    [Header("References")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] Transform dripContainer;
    [SerializeField] BloodPool bloodPool;
    [SerializeField] BloodFloodOverlay bloodFloodOverlay;
    [SerializeField] TitleFontRegistry fontRegistry;
    [SerializeField] RectTransform floorLine;

    [Header("Blood flood")]
    [SerializeField] bool autoPlayBloodFlood;

    [Header("Spawn timing (seconds)")]
    [SerializeField] float minDropDelay = 0.45f;
    [SerializeField] float maxDropDelay = 1.1f;
    [SerializeField] float minOozeDelay = 1.6f;
    [SerializeField] float maxOozeDelay = 3.2f;
    [SerializeField] float initialOozeDelayMin = 0.4f;
    [SerializeField] float initialOozeDelayMax = 1.2f;
    [SerializeField] bool loadMockPayloadOnStart;

    [Header("Visual tuning")]
    [SerializeField] float minStreakLength = 34f;
    [SerializeField] float maxStreakLength = 80f;
    [SerializeField] float minDropSize = 7f;
    [SerializeField] float maxDropSize = 11f;
    [SerializeField] float oozeGrowDuration = 1.9f;
    [SerializeField] float minFallDuration = 0.42f;
    [SerializeField] float maxFallDuration = 0.6f;
    [SerializeField] float intensityDelayScale = 0.55f;

    TitleStylePayload activePayload;
    System.Random random;
    Coroutine dropLoop;
    Coroutine oozeLoop;
    readonly List<GlyphAnchorInfo> glyphAnchors = new List<GlyphAnchorInfo>();

    public TMP_Text TitleText => titleText;
    public TitleStylePayload ActivePayload => activePayload;
    public IReadOnlyList<GlyphAnchorInfo> GlyphAnchors => glyphAnchors;

    void Start()
    {
        if (loadMockPayloadOnStart)
            ApplyMockPayload();
    }

    void OnDisable() => StopDripLoops();

    public void ApplyPayload(TitleStylePayload payload)
    {
        ApplyPayloadInternal(payload, replaceTitleText: true);
    }

    /// <summary>
    /// Applies drip colors, intensity, pool behavior, and scheduling to an existing TMP title
    /// without replacing <see cref="titleText"/> content.
    /// </summary>
    public void ApplyVisualsOnly(TitleStylePayload payload) =>
        ApplyPayloadInternal(payload, replaceTitleText: false);

    /// <summary>
    /// Alias for <see cref="ApplyVisualsOnly"/> — main-menu integration entry point.
    /// </summary>
    public void ApplyEffectToExistingTitle(TitleStylePayload payload) =>
        ApplyVisualsOnly(payload);

    public void ApplyMockPayload() => ApplyPayload(TitleStyleService.LoadMockPayload());

    public void ApplyMockVisualsOnly() => ApplyVisualsOnly(TitleStyleService.LoadMockPayload());

    void ApplyPayloadInternal(TitleStylePayload payload, bool replaceTitleText)
    {
        activePayload = payload ?? TitleStylePayload.CreateDefault();
        StopDripLoops();
        ClearActiveDrips();

        if (replaceTitleText)
            ApplyTextStyle(activePayload);
        else
            ApplyVisualStyle(activePayload);

        if (bloodPool != null)
        {
            bloodPool.Configure(activePayload);
            bloodPool.SetImpactCoordinateSpace(dripContainer as RectTransform);
            bloodPool.ResetPool();
        }

        ConfigureBloodFlood(activePayload);

        random = activePayload.HasSeed ? new System.Random(activePayload.Seed) : null;
        RefreshGlyphAnchors();

        if (isActiveAndEnabled && glyphAnchors.Count > 0)
            StartDripLoops();
    }

    public void RestartDrips()
    {
        StopDripLoops();
        ClearActiveDrips();
        bloodPool?.ResetPool();
        RestartBloodFlood();
        RefreshGlyphAnchors();

        if (isActiveAndEnabled && glyphAnchors.Count > 0)
            StartDripLoops();
    }

    public void RefreshGlyphAnchors()
    {
        glyphAnchors.Clear();
        if (titleText == null)
            return;

        titleText.ForceMeshUpdate();
        TMP_TextInfo textInfo = titleText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo info = textInfo.characterInfo[i];
            if (!info.isVisible)
                continue;

            char character = titleText.text.Length > i ? titleText.text[i] : '\0';
            if (char.IsWhiteSpace(character))
                continue;

            glyphAnchors.Add(new GlyphAnchorInfo
            {
                Character = character,
                LocalPosition = dripContainer != null
                    ? GetGlyphAnchor(i)
                    : new Vector2(
                        (info.bottomLeft.x + info.bottomRight.x) * 0.5f,
                        info.bottomLeft.y),
            });
        }
    }

    void ApplyTextStyle(TitleStylePayload payload)
    {
        if (titleText == null)
            return;

        titleText.text = payload.Text;
        ApplyVisualStyle(payload);
    }

    void ApplyVisualStyle(TitleStylePayload payload)
    {
        if (titleText == null)
            return;

        titleText.color = payload.Color;
        titleText.font = ResolveFont(payload);
        titleText.ForceMeshUpdate();
    }

    TMP_FontAsset ResolveFont(TitleStylePayload payload)
    {
        TitleFontRegistry registry = fontRegistry != null ? fontRegistry : TitleFontRegistry.GetOrCreate();
        if (registry != null)
            return registry.Resolve(payload.FontKey, payload.Language);

        return TMP_Settings.defaultFontAsset;
    }

    void StartDripLoops()
    {
        dropLoop = StartCoroutine(DropLoopRoutine());
        oozeLoop = StartCoroutine(OozeLoopRoutine());
    }

    void StopDripLoops()
    {
        if (dropLoop != null)
        {
            StopCoroutine(dropLoop);
            dropLoop = null;
        }

        if (oozeLoop != null)
        {
            StopCoroutine(oozeLoop);
            oozeLoop = null;
        }
    }

    IEnumerator DropLoopRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(NextRange(minDropDelay, maxDropDelay) * IntensityDelayMultiplier());
            SpawnDrop();
        }
    }

    IEnumerator OozeLoopRoutine()
    {
        yield return new WaitForSeconds(NextRange(initialOozeDelayMin, initialOozeDelayMax));
        while (true)
        {
            SpawnOoze();
            yield return new WaitForSeconds(NextRange(minOozeDelay, maxOozeDelay) * IntensityDelayMultiplier());
        }
    }

    float IntensityDelayMultiplier()
    {
        float intensity = activePayload != null ? activePayload.DripIntensity : TitleStylePayload.DefaultDripIntensity;
        return Mathf.Lerp(1.35f, 0.55f, intensity) * intensityDelayScale + (1f - intensityDelayScale);
    }

    void SpawnOoze()
    {
        if (!TryPickGlyphAnchor(out Vector2 anchor, out float floorY))
            return;

        float horizontalOffset = NextRange(-6f, 6f);
        float streakLength = NextRange(minStreakLength, maxStreakLength) * IntensityLengthMultiplier();
        float fallDuration = NextRange(minFallDuration, maxFallDuration);

        BloodDripPlayRequest request = BloodDripPlayRequest.CreateAttachedStreak(
            anchor,
            floorY,
            activePayload.Color,
            activePayload.BrightColor,
            activePayload.DarkColor,
            activePayload.DripIntensity);
        request.HorizontalOffset = horizontalOffset;
        request.StreakLength = streakLength;
        request.GrowDurationSeconds = oozeGrowDuration;
        request.FallDurationSeconds = fallDuration;
        request.RandomSource = random;
        request.ImpactCallback = HandleBloodDripImpact;

        BloodDrip drip = BloodDrip.Spawn(dripContainer, bloodPool);
        drip.Play(request, bloodPool);
    }

    void SpawnDrop()
    {
        if (!TryPickGlyphAnchor(out Vector2 anchor, out float floorY))
            return;

        float dropSize = NextRange(minDropSize, maxDropSize) * IntensityLengthMultiplier();
        float fallDuration = NextRange(minFallDuration, maxFallDuration);

        BloodDripPlayRequest request = BloodDripPlayRequest.CreateSimpleDrop(
            anchor,
            floorY,
            activePayload.Color,
            activePayload.BrightColor,
            activePayload.DarkColor,
            activePayload.DripIntensity);
        request.DropSize = dropSize;
        request.FallDurationSeconds = fallDuration;
        request.RandomSource = random;
        request.ImpactCallback = HandleBloodDripImpact;

        BloodDrip drip = BloodDrip.Spawn(dripContainer, bloodPool);
        drip.Play(request, bloodPool);
    }

    void HandleBloodDripImpact(BloodDripImpactInfo info)
    {
        if (bloodFloodOverlay == null)
            return;

        bloodFloodOverlay.AddFillFromImpact(info);
    }

    float IntensityLengthMultiplier()
    {
        float intensity = activePayload != null ? activePayload.DripIntensity : TitleStylePayload.DefaultDripIntensity;
        return Mathf.Lerp(0.75f, 1.15f, intensity);
    }

    bool TryPickGlyphAnchor(out Vector2 anchor, out float floorY)
    {
        anchor = Vector2.zero;
        floorY = 0f;

        if (glyphAnchors.Count == 0 || titleText == null || dripContainer == null)
            return false;

        GlyphAnchorInfo picked = glyphAnchors[NextInt(0, glyphAnchors.Count)];
        anchor = picked.LocalPosition;
        floorY = GetFloorLocalY();
        return true;
    }

    Vector2 GetGlyphAnchor(int characterIndex)
    {
        TMP_CharacterInfo info = titleText.textInfo.characterInfo[characterIndex];
        var bottomCenter = new Vector3(
            (info.bottomLeft.x + info.bottomRight.x) * 0.5f,
            info.bottomLeft.y,
            0f);

        Vector3 world = titleText.transform.TransformPoint(bottomCenter);
        return dripContainer.InverseTransformPoint(world);
    }

    float GetFloorLocalY()
    {
        if (floorLine != null)
            return dripContainer.InverseTransformPoint(floorLine.position).y;

        if (bloodPool != null)
            return dripContainer.InverseTransformPoint(bloodPool.transform.position).y;

        return 0f;
    }

    void ClearActiveDrips()
    {
        if (dripContainer == null)
            return;

        for (int i = dripContainer.childCount - 1; i >= 0; i--)
            Destroy(dripContainer.GetChild(i).gameObject);
    }

    void ConfigureBloodFlood(TitleStylePayload payload)
    {
        if (bloodFloodOverlay == null)
            return;

        bloodFloodOverlay.ConfigureFromPayload(payload);
        bloodFloodOverlay.ResetFlood();

        if (autoPlayBloodFlood && isActiveAndEnabled)
            bloodFloodOverlay.Play();
    }

    void RestartBloodFlood()
    {
        if (bloodFloodOverlay == null)
            return;

        bloodFloodOverlay.ResetFlood();

        if (autoPlayBloodFlood && isActiveAndEnabled)
            bloodFloodOverlay.Play();
    }

    public void SetBloodFloodFillAmount(float amount)
    {
        if (bloodFloodOverlay != null)
            bloodFloodOverlay.FillAmount = amount;
    }

    float NextRange(float min, float max)
    {
        if (random != null)
            return (float)(min + random.NextDouble() * (max - min));

        return UnityEngine.Random.Range(min, max);
    }

    int NextInt(int minInclusive, int maxExclusive)
    {
        if (random != null)
            return random.Next(minInclusive, maxExclusive);

        return UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    public void SetTitleTextForRuntime(TMP_Text text) => titleText = text;

#if UNITY_EDITOR
    public void SetTitleTextForTests(TMP_Text text) => titleText = text;

    public void SetLoadMockPayloadOnStartForTests(bool value) => loadMockPayloadOnStart = value;

    public void PrepareRandomStateForTests(TitleStylePayload payload)
    {
        random = payload != null && payload.HasSeed ? new System.Random(payload.Seed) : null;
    }

    public float SampleRangeForTests(float min, float max) => NextRange(min, max);

    public int SampleIndexForTests(int exclusiveMax) => NextInt(0, exclusiveMax);
#endif
}
