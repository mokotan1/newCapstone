using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floor blood pool that grows as drips land and spawns lightweight splash sprites on impact.
/// Controlled by <see cref="BloodDripTitleRenderer"/> and invoked from <see cref="BloodDrip"/>.
/// Visual intent mirrors <c>growPool()</c> and <c>impact()</c> in <c>docs/blood-drip-title-final.html</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class BloodPool : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RectTransform poolVisualRect;
    [SerializeField] Image poolImage;
    [SerializeField] RectTransform splashRoot;
    [SerializeField] RectTransform spreadRoot;
    [Tooltip("Local space used by BloodDrip impact positions (typically the drip container).")]
    [SerializeField] RectTransform impactCoordinateSpace;

    [Header("Pool growth (HTML prototype defaults)")]
    [SerializeField] float basePoolWidth;
    [SerializeField] float basePoolHeight = 22f;
    [SerializeField] float maxPoolWidth = 420f;
    [SerializeField] float maxPoolHeight = 36f;
    [SerializeField] float widthGrowthMultiplier = 1.4f;
    [SerializeField] float heightPerWidth = 0.048f;
    [SerializeField] float growthDuration = 0.45f;
    [SerializeField] float growthBloomStrength = 0.045f;

    [Header("Splash")]
    [SerializeField] int splashCount = 3;
    [SerializeField] float splashDuration = 0.35f;
    [SerializeField] Vector2 splashSizeRange = new Vector2(2f, 4f);
    [SerializeField] Vector2 splashOffsetXRange = new Vector2(-14f, 14f);
    [SerializeField] Vector2 splashOffsetYRange = new Vector2(-10f, -2f);

    [Header("Floor spread stains")]
    [SerializeField] bool spreadEnabled = true;
    [SerializeField] int spreadBurstsPerImpact = 2;
    [SerializeField] Vector2 spreadWidthRange = new Vector2(36f, 110f);
    [SerializeField] Vector2 spreadHeightRange = new Vector2(3f, 12f);
    [SerializeField] float spreadDuration = 1.8f;
    [SerializeField] float spreadAlpha = 0.22f;
    [SerializeField] float maxSpreadWidth = 180f;
    [SerializeField] float spreadVerticalJitter = 6f;
    [SerializeField] int maxRetainedSpreadStains = 32;

    float accumulatedWidth;
    float displayedWidth;
    float displayedHeight;
    bool poolEnabled = true;
    Color brightColor = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultBrightColorHex, Color.red);
    Color mainColor = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultColorHex, Color.red);
    Color darkColor = TitleStylePayload.ParseHexColor(TitleStylePayload.DefaultDarkColorHex, Color.black);

    Coroutine growthRoutine;
    readonly List<Coroutine> activeSplashRoutines = new List<Coroutine>();
    readonly List<SpreadStainEntry> activeSpreadStains = new List<SpreadStainEntry>();
    readonly List<PoolBlobLayer> poolBlobLayers = new List<PoolBlobLayer>();
    float growthAsymmetryX;

    sealed class PoolBlobLayer
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 OffsetFraction;
        public Vector2 SizeFraction;
        public float Rotation;
        public float ColorLerp;
        public float AlphaMultiplier;
        public float WidthGrowthBias;
    }

    sealed class SpreadStainEntry
    {
        public GameObject Root;
        public Sequence Sequence;
    }

    public bool IsPoolEnabled => poolEnabled;

    public float CurrentPoolWidth => displayedWidth;

    public float CurrentPoolHeight => displayedHeight;

#if UNITY_EDITOR
    public int ActiveSpreadStainCountForTests => activeSpreadStains.Count;

    public bool SpreadEnabledForTests => spreadEnabled;
#endif

    void Awake()
    {
        EnsureReferences();
        ApplyPoolVisualSizeImmediate(basePoolWidth, basePoolHeight);
        RefreshPoolVisibility();
    }

    void OnDisable()
    {
        StopAllPoolCoroutines();
        KillAllSpreadTweens(clearChildren: false);
    }

    /// <summary>Applies renderer/backend colors and pool visibility.</summary>
    public void Configure(bool enabled, Color main, Color dark, Color bright)
    {
        SetColors(bright, main, dark);
        SetPoolEnabled(enabled);
    }

    public void Configure(TitleStylePayload payload)
    {
        if (payload == null)
            return;

        Configure(payload.PoolEnabled, payload.Color, payload.DarkColor, payload.BrightColor);
    }

    public void SetImpactCoordinateSpace(RectTransform space)
    {
        impactCoordinateSpace = space;
    }

    public void SetColors(Color bright, Color main, Color dark)
    {
        brightColor = bright;
        mainColor = main;
        darkColor = dark;
        ApplyPoolTint();
    }

    public void SetPoolEnabled(bool enabled)
    {
        poolEnabled = enabled;
        RefreshPoolVisibility();
    }

    public void ResetPool()
    {
        StopAllPoolCoroutines();
        ClearSplashChildren();
        KillAllSpreadTweens(clearChildren: true);
        accumulatedWidth = 0f;
        displayedWidth = basePoolWidth;
        displayedHeight = basePoolHeight;
        ApplyPoolVisualSizeImmediate(displayedWidth, displayedHeight);
        RefreshPoolVisibility();
    }

    /// <summary>
    /// Called when a drip lands. <paramref name="position"/> is in <see cref="impactCoordinateSpace"/> local units.
    /// </summary>
    public void PlayImpact(Vector2 position, float dripSize)
    {
        RegisterImpactAtSplashLocal(ToSplashLocal(position), dripSize);
    }

    /// <summary>Called from <see cref="BloodDrip"/> after converting container-local impact to world space.</summary>
    public void RegisterImpactAtWorld(Vector3 worldPosition, float dripSize)
    {
        if (splashRoot == null)
            EnsureReferences();

        Vector2 splashLocal = splashRoot != null
            ? (Vector2)splashRoot.InverseTransformPoint(worldPosition)
            : (Vector2)transform.InverseTransformPoint(worldPosition);

        RegisterImpactAtSplashLocal(splashLocal, dripSize);
    }

    void RegisterImpactAtSplashLocal(Vector2 splashLocal, float dripSize)
    {
        if (splashRoot == null)
            EnsureReferences();

        SpawnSplashes(splashLocal);

        if (!poolEnabled)
            return;

        float nextWidth = BloodPoolGrowth.ComputeNextWidth(
            accumulatedWidth,
            dripSize,
            widthGrowthMultiplier,
            maxPoolWidth);

        float nextHeight = BloodPoolGrowth.ComputeHeight(
            nextWidth,
            basePoolHeight,
            heightPerWidth,
            maxPoolHeight);

        accumulatedWidth = nextWidth;
        growthAsymmetryX = Random.Range(-0.09f, 0.09f);
        AnimatePoolTo(nextWidth, nextHeight);

        if (spreadEnabled)
            SpawnSpreadStains(splashLocal);
    }

    Vector2 ToSplashLocal(Vector2 position)
    {
        if (splashRoot == null)
            return position;

        if (impactCoordinateSpace == null || impactCoordinateSpace == splashRoot)
            return position;

        Vector3 world = impactCoordinateSpace.TransformPoint(position);
        return splashRoot.InverseTransformPoint(world);
    }

    void EnsureReferences()
    {
        if (spreadRoot == null)
        {
            Transform existing = transform.Find("SpreadRoot");
            spreadRoot = existing != null
                ? existing.GetComponent<RectTransform>()
                : CreateChildRect("SpreadRoot", transform);
        }

        if (splashRoot == null)
        {
            Transform existing = transform.Find("SplashRoot");
            splashRoot = existing != null
                ? existing.GetComponent<RectTransform>()
                : CreateChildRect("SplashRoot", transform);
        }

        if (poolVisualRect == null)
        {
            Transform existing = transform.Find("PoolVisual");
            if (existing != null)
            {
                poolVisualRect = existing.GetComponent<RectTransform>();
                poolImage = existing.GetComponent<Image>();
            }
        }

        if (poolVisualRect == null)
        {
            var poolGo = new GameObject("PoolVisual", typeof(RectTransform));
            poolGo.transform.SetParent(transform, false);

            poolVisualRect = poolGo.GetComponent<RectTransform>();
            poolVisualRect.anchorMin = new Vector2(0.5f, 0f);
            poolVisualRect.anchorMax = new Vector2(0.5f, 0f);
            poolVisualRect.pivot = new Vector2(0.5f, 0f);
            poolVisualRect.anchoredPosition = Vector2.zero;
        }

        EnsurePoolBlobLayers();
        RemoveLegacyPoolVisualImage();

        if (poolImage == null && poolBlobLayers.Count > 0)
            poolImage = poolBlobLayers[0].Image;

        EnsureChildRenderOrder();
        ApplyPoolTint();
    }

    void EnsurePoolBlobLayers()
    {
        if (poolVisualRect == null)
            return;

        if (poolBlobLayers.Count == 0)
            BuildDefaultPoolBlobLayers();

        for (int i = 0; i < poolBlobLayers.Count; i++)
        {
            PoolBlobLayer layer = poolBlobLayers[i];
            if (layer.Rect != null && layer.Image != null)
                continue;

            string blobName = GetDefaultBlobName(i);
            Transform existing = poolVisualRect.Find(blobName);
            if (existing != null)
            {
                layer.Rect = existing.GetComponent<RectTransform>();
                layer.Image = existing.GetComponent<Image>();
                continue;
            }

            layer.Rect = CreatePoolBlobRect(blobName, poolVisualRect);
            layer.Image = layer.Rect.GetComponent<Image>();
        }
    }

    void BuildDefaultPoolBlobLayers()
    {
        poolBlobLayers.Clear();
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(0f, 0.34f),
            SizeFraction = new Vector2(0.78f, 0.72f),
            Rotation = -2f,
            ColorLerp = 0.32f,
            AlphaMultiplier = 0.94f,
            WidthGrowthBias = 0f,
        });
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(-0.28f, 0.18f),
            SizeFraction = new Vector2(0.42f, 0.38f),
            Rotation = 11f,
            ColorLerp = 0.48f,
            AlphaMultiplier = 0.58f,
            WidthGrowthBias = -0.04f,
        });
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(0.31f, 0.14f),
            SizeFraction = new Vector2(0.36f, 0.34f),
            Rotation = -14f,
            ColorLerp = 0.44f,
            AlphaMultiplier = 0.52f,
            WidthGrowthBias = 0.05f,
        });
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(-0.44f, 0.08f),
            SizeFraction = new Vector2(0.16f, 0.14f),
            Rotation = 6f,
            ColorLerp = 0.22f,
            AlphaMultiplier = 0.72f,
            WidthGrowthBias = -0.02f,
        });
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(0.47f, 0.06f),
            SizeFraction = new Vector2(0.12f, 0.11f),
            Rotation = -9f,
            ColorLerp = 0.28f,
            AlphaMultiplier = 0.66f,
            WidthGrowthBias = 0.03f,
        });
        poolBlobLayers.Add(new PoolBlobLayer
        {
            OffsetFraction = new Vector2(0.08f, 0.52f),
            SizeFraction = new Vector2(0.1f, 0.09f),
            Rotation = 4f,
            ColorLerp = 0.18f,
            AlphaMultiplier = 0.55f,
            WidthGrowthBias = 0f,
        });
    }

    static string GetDefaultBlobName(int index)
    {
        switch (index)
        {
            case 0: return "BlobCenter";
            case 1: return "BlobLeft";
            case 2: return "BlobRight";
            case 3: return "BlobDropletL";
            case 4: return "BlobDropletR";
            default: return "BlobDroplet" + index;
        }
    }

    static RectTransform CreatePoolBlobRect(string name, Transform parent)
    {
        var blobGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasRenderer));
        blobGo.transform.SetParent(parent, false);

        var rect = blobGo.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        var image = blobGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = BloodDripUiHelper.WhiteSprite;
        image.type = Image.Type.Simple;
        return rect;
    }

    void RemoveLegacyPoolVisualImage()
    {
        if (poolVisualRect == null)
            return;

        if (!poolVisualRect.TryGetComponent(out Image legacyImage))
            return;

        if (poolImage == legacyImage)
            poolImage = poolBlobLayers.Count > 0 ? poolBlobLayers[0].Image : null;

        DestroyUiObject(legacyImage);
    }

    void EnsureChildRenderOrder()
    {
        if (spreadRoot != null)
            spreadRoot.SetAsFirstSibling();

        if (poolVisualRect != null)
            poolVisualRect.SetSiblingIndex(spreadRoot != null ? 1 : 0);

        if (splashRoot != null)
            splashRoot.SetAsLastSibling();
    }

    static RectTransform CreateChildRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    void ApplyPoolTint()
    {
        for (int i = 0; i < poolBlobLayers.Count; i++)
        {
            PoolBlobLayer layer = poolBlobLayers[i];
            if (layer.Image == null)
                continue;

            Color tint = Color.Lerp(darkColor, mainColor, layer.ColorLerp);
            tint.a = layer.AlphaMultiplier;
            layer.Image.color = tint;
        }

        if (poolImage == null && poolBlobLayers.Count > 0)
            poolImage = poolBlobLayers[0].Image;
    }

    void RefreshPoolVisibility()
    {
        bool show = poolEnabled && accumulatedWidth > 0.01f;

        for (int i = 0; i < poolBlobLayers.Count; i++)
        {
            Image image = poolBlobLayers[i].Image;
            if (image != null)
                image.enabled = show;
        }
    }

    void ApplyPoolVisualSizeImmediate(float width, float height, float bloom = 0f)
    {
        displayedWidth = width;
        displayedHeight = height;

        if (poolVisualRect == null)
            return;

        poolVisualRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        poolVisualRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        float bloomX = growthAsymmetryX * bloom;
        float bloomY = bloom * 0.38f;
        poolVisualRect.localScale = new Vector3(1f + bloomX, 1f + bloomY, 1f);

        ApplyPoolBlobLayout(width, height, bloom);
    }

    void ApplyPoolBlobLayout(float poolWidth, float poolHeight, float bloom)
    {
        if (poolWidth <= 0.01f || poolHeight <= 0.01f)
            return;

        for (int i = 0; i < poolBlobLayers.Count; i++)
        {
            PoolBlobLayer layer = poolBlobLayers[i];
            if (layer.Rect == null)
                continue;

            float widthScale = 1f + bloom * (1f + layer.WidthGrowthBias * 2f);
            float heightScale = 1f + bloom * 0.28f;
            Vector2 size = new Vector2(
                poolWidth * layer.SizeFraction.x * widthScale,
                poolHeight * layer.SizeFraction.y * heightScale);

            layer.Rect.sizeDelta = size;
            layer.Rect.anchoredPosition = new Vector2(
                poolWidth * layer.OffsetFraction.x,
                poolHeight * layer.OffsetFraction.y);
            layer.Rect.localRotation = Quaternion.Euler(0f, 0f, layer.Rotation);

            if (layer.Image != null)
            {
                Color tint = layer.Image.color;
                tint.a = layer.AlphaMultiplier * Mathf.Lerp(0.82f, 1f, bloom);
                layer.Image.color = tint;
            }
        }
    }

    void AnimatePoolTo(float targetWidth, float targetHeight)
    {
        if (growthRoutine != null)
            StopCoroutine(growthRoutine);

        growthRoutine = StartCoroutine(AnimatePoolRoutine(targetWidth, targetHeight));
    }

    IEnumerator AnimatePoolRoutine(float targetWidth, float targetHeight)
    {
        float startWidth = displayedWidth;
        float startHeight = displayedHeight;
        float elapsed = 0f;

        RefreshPoolVisibility();

        while (elapsed < growthDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growthDuration);
            float eased = EaseOutPool(t);

            float width = Mathf.Lerp(startWidth, targetWidth, eased);
            float height = Mathf.Lerp(startHeight, targetHeight, eased);
            float bloom = Mathf.Sin(t * Mathf.PI) * growthBloomStrength;
            ApplyPoolVisualSizeImmediate(width, height, bloom);

            yield return null;
        }

        ApplyPoolVisualSizeImmediate(targetWidth, targetHeight, 0f);
        growthRoutine = null;
    }

    static float EaseOutPool(float t)
    {
        // Approximates CSS cubic-bezier(.39,.58,.57,1)
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    void SpawnSplashes(Vector2 localPosition)
    {
        if (splashRoot == null || splashCount <= 0)
            return;

        for (int i = 0; i < splashCount; i++)
            activeSplashRoutines.Add(StartCoroutine(AnimateSplash(localPosition)));
    }

    IEnumerator AnimateSplash(Vector2 origin)
    {
        var splashGo = new GameObject("BloodSplash", typeof(RectTransform), typeof(Image), typeof(CanvasRenderer));
        splashGo.transform.SetParent(splashRoot, false);

        var rect = splashGo.GetComponent<RectTransform>();
        float size = Random.Range(splashSizeRange.x, splashSizeRange.y);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = origin;

        var image = splashGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = BloodDripUiHelper.WhiteSprite;
        image.color = mainColor;

        Vector2 delta = new Vector2(
            Random.Range(splashOffsetXRange.x, splashOffsetXRange.y),
            Random.Range(splashOffsetYRange.x, splashOffsetYRange.y));

        float elapsed = 0f;
        Color startColor = image.color;

        while (elapsed < splashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / splashDuration);
            float moveT = EaseOutSplash(t);

            rect.anchoredPosition = origin + delta * moveT;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, t);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            image.color = c;

            yield return null;
        }

        DestroyUiObject(splashGo);
        activeSplashRoutines.RemoveAll(c => c == null);
    }

    static float EaseOutSplash(float t) => 1f - (1f - t) * (1f - t);

    void SpawnSpreadStains(Vector2 splashLocal)
    {
        if (spreadRoot == null || spreadBurstsPerImpact <= 0)
            return;

        float poolCenterX = poolVisualRect != null ? poolVisualRect.anchoredPosition.x : 0f;
        float anchorX = Mathf.Lerp(poolCenterX, splashLocal.x, 0.65f);

        for (int i = 0; i < spreadBurstsPerImpact; i++)
        {
            float width = BloodPoolSpreadPolicy.ClampSpreadWidth(
                Random.Range(spreadWidthRange.x, spreadWidthRange.y),
                maxSpreadWidth);
            float height = Random.Range(spreadHeightRange.x, spreadHeightRange.y);
            float jitterY = Random.Range(-spreadVerticalJitter, spreadVerticalJitter);
            Vector2 origin = new Vector2(
                anchorX + Random.Range(-width * 0.12f, width * 0.12f),
                splashLocal.y + jitterY);

            CreateSpreadStain(origin, width, height);
        }

        TrimSpreadStainsToLimit();
    }

    void CreateSpreadStain(Vector2 localPosition, float targetWidth, float targetHeight)
    {
        var stainGo = new GameObject("BloodSpreadStain", typeof(RectTransform), typeof(CanvasGroup));
        stainGo.transform.SetParent(spreadRoot, false);

        var rect = stainGo.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = localPosition;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));

        float startWidth = targetWidth * Random.Range(0.08f, 0.16f);
        float startHeight = targetHeight * Random.Range(0.35f, 0.55f);
        rect.sizeDelta = new Vector2(startWidth, startHeight);
        rect.localScale = new Vector3(Random.Range(0.92f, 1.08f), Random.Range(0.75f, 1.05f), 1f);

        var primary = CreateSpreadLayer(stainGo.transform, "Primary", 1f);
        var secondary = CreateSpreadLayer(stainGo.transform, "Secondary", 0.72f);

        Color stainColor = Color.Lerp(darkColor, mainColor, Random.Range(0.15f, 0.45f));
        stainColor.a = 0f;
        primary.color = stainColor;
        secondary.color = Color.Lerp(darkColor, stainColor, 0.35f);

        var canvasGroup = stainGo.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        var targetSize = new Vector2(targetWidth, targetHeight);
        var sequence = DOTween.Sequence();
        sequence.SetLink(stainGo);
        sequence.Append(rect.DOSizeDelta(targetSize, spreadDuration).SetEase(Ease.OutQuad));
        sequence.Join(canvasGroup.DOFade(spreadAlpha, spreadDuration * 0.55f).SetEase(Ease.OutSine));
        sequence.Join(primary.DOFade(spreadAlpha, spreadDuration * 0.55f));
        sequence.Join(secondary.DOFade(spreadAlpha * 0.75f, spreadDuration * 0.65f));
        sequence.Append(rect.DOScale(
            new Vector3(rect.localScale.x * Random.Range(1.02f, 1.12f), rect.localScale.y * Random.Range(0.95f, 1.05f), 1f),
            spreadDuration * 0.25f).SetEase(Ease.OutSine));

        activeSpreadStains.Add(new SpreadStainEntry { Root = stainGo, Sequence = sequence });
    }

    static Image CreateSpreadLayer(Transform parent, string name, float sizeScale)
    {
        var layerGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasRenderer));
        layerGo.transform.SetParent(parent, false);

        var rect = layerGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one * sizeScale;

        var image = layerGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.sprite = BloodDripUiHelper.WhiteSprite;
        image.type = Image.Type.Simple;
        return image;
    }

    void TrimSpreadStainsToLimit()
    {
        int evictionCount = BloodPoolSpreadPolicy.ComputeEvictionCount(
            activeSpreadStains.Count,
            maxRetainedSpreadStains);

        for (int i = 0; i < evictionCount; i++)
            RemoveSpreadStainAt(0);
    }

    void RemoveSpreadStainAt(int index)
    {
        if (index < 0 || index >= activeSpreadStains.Count)
            return;

        SpreadStainEntry entry = activeSpreadStains[index];
        activeSpreadStains.RemoveAt(index);

        if (entry.Sequence != null && entry.Sequence.IsActive())
            entry.Sequence.Kill();

        if (entry.Root != null)
            DestroyUiObject(entry.Root);
    }

    void KillAllSpreadTweens(bool clearChildren)
    {
        for (int i = activeSpreadStains.Count - 1; i >= 0; i--)
        {
            SpreadStainEntry entry = activeSpreadStains[i];
            if (entry.Sequence != null && entry.Sequence.IsActive())
                entry.Sequence.Kill();

            if (clearChildren && entry.Root != null)
                DestroyUiObject(entry.Root);
        }

        activeSpreadStains.Clear();

        if (clearChildren && spreadRoot != null)
        {
            for (int i = spreadRoot.childCount - 1; i >= 0; i--)
                DestroyUiObject(spreadRoot.GetChild(i).gameObject);
        }
    }

    void ClearSplashChildren()
    {
        if (splashRoot == null)
            return;

        for (int i = splashRoot.childCount - 1; i >= 0; i--)
            DestroyUiObject(splashRoot.GetChild(i).gameObject);
    }

    static void DestroyUiObject(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(target);
            return;
        }
#endif
        Object.Destroy(target);
    }

    void StopAllPoolCoroutines()
    {
        if (growthRoutine != null)
        {
            StopCoroutine(growthRoutine);
            growthRoutine = null;
        }

        for (int i = activeSplashRoutines.Count - 1; i >= 0; i--)
        {
            if (activeSplashRoutines[i] != null)
                StopCoroutine(activeSplashRoutines[i]);
        }

        activeSplashRoutines.Clear();
    }

#if UNITY_EDITOR
    public void RegisterImpactForTests(Vector2 splashLocal, float dripSize)
    {
        RegisterImpactAtSplashLocal(splashLocal, dripSize);
    }

    public void SetSpreadEnabledForTests(bool enabled) => spreadEnabled = enabled;

    public void SetMaxRetainedSpreadStainsForTests(int max) => maxRetainedSpreadStains = max;

    public void SetSpreadBurstsPerImpactForTests(int bursts) => spreadBurstsPerImpact = bursts;
#endif
}

/// <summary>Pure growth math shared by <see cref="BloodPool"/> and EditMode tests.</summary>
public static class BloodPoolGrowth
{
    public static float ComputeNextWidth(
        float currentWidth,
        float dripAmount,
        float widthGrowthMultiplier,
        float maxWidth)
    {
        if (dripAmount <= 0f)
            return currentWidth;

        return Mathf.Min(currentWidth + dripAmount * widthGrowthMultiplier, maxWidth);
    }

    public static float ComputeHeight(
        float poolWidth,
        float baseHeight,
        float heightPerWidth,
        float maxHeight)
    {
        return Mathf.Min(baseHeight + poolWidth * heightPerWidth, maxHeight);
    }
}

/// <summary>Pure spread-stain retention math for <see cref="BloodPool"/> and EditMode tests.</summary>
public static class BloodPoolSpreadPolicy
{
    public static float ClampSpreadWidth(float width, float maxSpreadWidth)
    {
        if (maxSpreadWidth <= 0f)
            return width;

        return Mathf.Min(width, maxSpreadWidth);
    }

    public static int ComputeEvictionCount(int currentCount, int maxRetained)
    {
        if (maxRetained <= 0)
            return currentCount;

        return Mathf.Max(0, currentCount - maxRetained);
    }
}
