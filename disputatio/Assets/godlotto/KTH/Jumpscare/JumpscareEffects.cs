using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Visual stack for corridor jumpscares: blink overlay, URP DoF, animator beats, game-over overlay.
/// Coroutine entry points return <see cref="IEnumerator"/>; host <see cref="MonoBehaviour"/> runs them.
/// </summary>
public sealed class JumpscareEffects
{
    private const float OverlayPlaneZOffsetFromCamera = 1f;
    private const int DarkOverlaySortingOrder = 32000;
    private const int JumpscareTopSortingOrder = 32760;
    private const int BlinkOverlaySortingOrder = 32767;
    private const float GhostSecondFrameDelay = 0.4166667f;
    private const int DarkOverlayTextureSize = 128;
    private const float DarkOverlaySoftEdgePixels = 3f;

    private readonly SpriteRenderer _blinkOverlay;
    private readonly GameObject _gameOverObject;
    private readonly Animator _jumpscareAnimator;
    private readonly float _blinkDuration;
    private readonly float _closedDuration;
    private readonly int _initialBlinkCount;
    private readonly float _blinkInterval;
    private readonly float _darkOverlayStartSize;

    private DepthOfField _dof;
    private readonly int _blinkAmountProp = Shader.PropertyToID("_BlinkAmount");
    private bool _isBlinkSequenceRunning;
    private SpriteRenderer _darkOverlay;
    private Sprite _darkOverlaySprite;
    private Material _darkOverlayMaterial;

    public bool IsBlinkSequenceRunning => _isBlinkSequenceRunning;

    public JumpscareEffects(
        SpriteRenderer blinkOverlay,
        GameObject gameOverObject,
        Animator jumpscareAnimator,
        float blinkDuration,
        float closedDuration,
        int initialBlinkCount,
        float blinkInterval,
        float darkOverlayStartSize)
    {
        _blinkOverlay = blinkOverlay;
        _gameOverObject = gameOverObject;
        _jumpscareAnimator = jumpscareAnimator;
        _blinkDuration = blinkDuration;
        _closedDuration = closedDuration;
        _initialBlinkCount = Mathf.Max(1, initialBlinkCount);
        _blinkInterval = Mathf.Max(0f, blinkInterval);
        _darkOverlayStartSize = Mathf.Max(0.001f, darkOverlayStartSize);
    }

    public void InitBlinkMaterial()
    {
        if (_blinkOverlay != null && _blinkOverlay.material != null)
        {
            _blinkOverlay.material = new Material(_blinkOverlay.material);
            _blinkOverlay.material.SetFloat(_blinkAmountProp, 0.5f);
        }
    }

    public void FindAndBindVolume()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        Volume[] allVolumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        _dof = null;

        foreach (var v in allVolumes)
        {
            if (v.isGlobal && v.profile != null && v.profile.TryGet(out DepthOfField foundDof))
            {
                _dof = foundDof;
                break;
            }
        }

        if (_dof == null)
        {
            foreach (var v in allVolumes)
            {
                if (v.profile != null && v.profile.TryGet(out DepthOfField foundDof))
                {
                    _dof = foundDof;
                    break;
                }
            }
        }

        if (_dof != null)
        {
            _dof.active = true;
            _dof.gaussianMaxRadius.overrideState = true;
            _dof.gaussianMaxRadius.value = 0f;
        }
        else
        {
            GameLog.LogWarning($"[JumpscareEffects] 씬 '{sceneName}'에서 DepthOfField를 가진 Volume을 찾지 못했습니다!");
        }
    }

    public void FitBlinkOverlayToScreen()
    {
        if (_blinkOverlay == null) return;
        _blinkOverlay.sortingOrder = Mathf.Max(_blinkOverlay.sortingOrder, BlinkOverlaySortingOrder);
        FitFullscreenSpriteRendererToMainCamera(_blinkOverlay);
        FitDarkOverlayToScreenPlane();
    }

    public void FitGameOverOverlayToScreen()
    {
        if (_gameOverObject == null) return;
        SpriteRenderer sr = _gameOverObject.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        FitFullscreenSpriteRendererToMainCamera(sr);
    }

    private static void FitFullscreenSpriteRendererToMainCamera(SpriteRenderer sr)
    {
        if (sr == null) return;

        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 camPos = mainCam.transform.position;
        sr.transform.position = new Vector3(camPos.x, camPos.y, camPos.z + OverlayPlaneZOffsetFromCamera);

        float worldHeight = mainCam.orthographicSize * 2f;
        float worldWidth = worldHeight * mainCam.aspect;

        if (sr.sprite != null)
        {
            Vector2 spriteSize = sr.sprite.bounds.size;
            sr.transform.localScale = new Vector3(
                worldWidth / spriteSize.x,
                worldHeight / spriteSize.y,
                1f
            );
        }
    }

    public void ResetBlinkSequenceFlag()
    {
        _isBlinkSequenceRunning = false;
    }

    public void ResetBlinkAndDepthOfField()
    {
        if (_blinkOverlay != null && _blinkOverlay.material != null)
            _blinkOverlay.material.SetFloat(_blinkAmountProp, 0.5f);
        if (_dof != null)
            _dof.gaussianMaxRadius.value = 0f;
        HideDarkOverlay();
    }

    public void SetAnimatorActive(bool active)
    {
        if (_jumpscareAnimator != null)
        {
            if (active)
                _jumpscareAnimator.enabled = true;
            _jumpscareAnimator.gameObject.SetActive(active);
        }
    }

    public void SetGameOverActive(bool active)
    {
        if (_gameOverObject != null)
            _gameOverObject.SetActive(active);
    }

    public void SetBlinkOverlayActive(bool active)
    {
        if (_blinkOverlay != null)
            _blinkOverlay.gameObject.SetActive(active);
    }

    public void ShowGameOverAfterFit()
    {
        FitGameOverOverlayToScreen();
        if (_gameOverObject != null)
            _gameOverObject.SetActive(true);
    }

    public void PositionJumpscareAnimator(Vector3 worldPosition)
    {
        if (_jumpscareAnimator != null)
            _jumpscareAnimator.transform.position = worldPosition;
    }

    public IEnumerator FullJumpscareSequence(
        Vector3 darkenCenterWorld,
        float darkenDuration,
        float blackScreenShakeDuration,
        float finalFrameHoldDuration,
        float secondFrameTime,
        float fourthFrameTime,
        Action onSecondFrameWillShow,
        Action onBlackScreenShakeStarted,
        Action onFinalFrameShakeStarted,
        Action onSequenceFinished)
    {
        _isBlinkSequenceRunning = true;

        yield return BlinkRepeated(_initialBlinkCount);

        onSecondFrameWillShow?.Invoke();
        ShowJumpscareAnimatorFrameAtTopLayer(secondFrameTime);

        yield return DarkenFromWorldPoint(darkenCenterWorld, darkenDuration);

        onBlackScreenShakeStarted?.Invoke();
        if (blackScreenShakeDuration > 0f)
            yield return new WaitForSeconds(blackScreenShakeDuration);

        yield return AnimateBlink(0.5f, 0f, 0f, 2.0f, _blinkDuration);
        ShowJumpscareAnimatorFrameAtTopLayer(fourthFrameTime);
        onFinalFrameShakeStarted?.Invoke();
        yield return new WaitForSeconds(_closedDuration);
        yield return AnimateBlink(0f, 0.5f, 2.0f, 0f, _blinkDuration);

        if (finalFrameHoldDuration > 0f)
            yield return new WaitForSeconds(finalFrameHoldDuration);

        _isBlinkSequenceRunning = false;
        if (_jumpscareAnimator != null)
            _jumpscareAnimator.speed = 0f;
        onSequenceFinished?.Invoke();
    }

    public IEnumerator FullJumpscareSequence()
    {
        yield return AnimateBlink(0.5f, 0f, 0f, 2.0f, _blinkDuration);
        yield return new WaitForSeconds(_closedDuration);
        if (_jumpscareAnimator != null)
        {
            _jumpscareAnimator.gameObject.SetActive(true);
            _jumpscareAnimator.Rebind();
            _jumpscareAnimator.Update(0f);
        }

        yield return AnimateBlink(0f, 0.5f, 2.0f, 0f, _blinkDuration);
    }

    private IEnumerator BlinkOnce()
    {
        yield return AnimateBlink(0.5f, 0f, 0f, 2.0f, _blinkDuration);
        yield return new WaitForSeconds(_closedDuration);
        yield return AnimateBlink(0f, 0.5f, 2.0f, 0f, _blinkDuration);
    }

    private IEnumerator BlinkRepeated(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return BlinkOnce();
            if (i < count - 1 && _blinkInterval > 0f)
                yield return new WaitForSeconds(_blinkInterval);
        }
    }

    private void ShowJumpscareAnimatorFrameAtTopLayer(float clipTime)
    {
        if (_jumpscareAnimator == null)
            return;

        _jumpscareAnimator.gameObject.SetActive(true);
        _jumpscareAnimator.enabled = true;
        _jumpscareAnimator.speed = 0f;
        RaiseAnimatorRenderersAboveDarkOverlay();
        _jumpscareAnimator.Rebind();
        SetAnimatorClipTime(clipTime);
    }

    private void SetAnimatorClipTime(float clipTime)
    {
        RuntimeAnimatorController controller = _jumpscareAnimator.runtimeAnimatorController;
        float clipLength = 1f;
        if (controller != null && controller.animationClips != null && controller.animationClips.Length > 0)
            clipLength = Mathf.Max(0.0001f, controller.animationClips[0].length);

        float normalizedTime = Mathf.Clamp01(clipTime / clipLength);
        _jumpscareAnimator.Play(0, 0, normalizedTime);
        _jumpscareAnimator.Update(0f);
    }

    public IEnumerator FrameTransitionBlink()
    {
        _isBlinkSequenceRunning = true;

        yield return AnimateBlink(0.5f, 0f, 0f, 2.0f, _blinkDuration);
        yield return new WaitForSeconds(_closedDuration);
        yield return AnimateBlink(0f, 0.5f, 2.0f, 0f, _blinkDuration);

        _isBlinkSequenceRunning = false;
    }

    private IEnumerator AnimateBlink(float bStart, float bEnd, float blStart, float blEnd, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (_blinkOverlay != null && _blinkOverlay.material != null)
                _blinkOverlay.material.SetFloat(_blinkAmountProp, Mathf.Lerp(bStart, bEnd, t));

            if (_dof != null)
                _dof.gaussianMaxRadius.value = Mathf.Lerp(blStart, blEnd, t);

            yield return null;
        }

        if (_blinkOverlay != null && _blinkOverlay.material != null)
            _blinkOverlay.material.SetFloat(_blinkAmountProp, bEnd);

        if (_dof != null)
            _dof.gaussianMaxRadius.value = blEnd;
    }

    public void HideAllVisualLayers()
    {
        if (_jumpscareAnimator != null)
            _jumpscareAnimator.enabled = true;
        SetAnimatorActive(false);
        SetGameOverActive(false);
        SetBlinkOverlayActive(false);
        HideDarkOverlay();
    }

    private IEnumerator DarkenFromWorldPoint(Vector3 centerWorld, float duration)
    {
        EnsureDarkOverlay();
        if (_darkOverlay == null)
            yield break;

        Camera mainCam = Camera.main;
        if (mainCam == null)
            yield break;

        _darkOverlay.gameObject.SetActive(true);
        _darkOverlay.transform.position = new Vector3(centerWorld.x, centerWorld.y, mainCam.transform.position.z + OverlayPlaneZOffsetFromCamera);
        _darkOverlay.transform.localScale = new Vector3(_darkOverlayStartSize, _darkOverlayStartSize, 1f);
        _darkOverlay.color = Color.black;

        float targetDiameter = CalculateScreenCoveringDiameter(centerWorld, mainCam);
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / safeDuration));
            float size = Mathf.Lerp(_darkOverlayStartSize, targetDiameter, t);
            _darkOverlay.transform.localScale = new Vector3(size, size, 1f);
            yield return null;
        }

        _darkOverlay.transform.localScale = new Vector3(targetDiameter, targetDiameter, 1f);
    }

    private void EnsureDarkOverlay()
    {
        if (_darkOverlay != null)
            return;

        GameObject overlayObject = new GameObject("JumpscareCenterDarkOverlay");
        UnityEngine.Object.DontDestroyOnLoad(overlayObject);
        _darkOverlay = overlayObject.AddComponent<SpriteRenderer>();
        _darkOverlaySprite = CreateDarkOverlaySprite();
        _darkOverlay.sprite = _darkOverlaySprite;
        _darkOverlayMaterial = CreateDarkOverlayMaterial();
        if (_darkOverlayMaterial != null)
            _darkOverlay.material = _darkOverlayMaterial;
        _darkOverlay.sortingLayerID = _blinkOverlay != null ? _blinkOverlay.sortingLayerID : 0;
        _darkOverlay.sortingOrder = DarkOverlaySortingOrder;
        _darkOverlay.gameObject.SetActive(false);
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

    private void FitDarkOverlayToScreenPlane()
    {
        if (_darkOverlay == null || Camera.main == null)
            return;

        Vector3 position = _darkOverlay.transform.position;
        _darkOverlay.transform.position = new Vector3(position.x, position.y, Camera.main.transform.position.z + OverlayPlaneZOffsetFromCamera);
    }

    private void HideDarkOverlay()
    {
        if (_darkOverlay != null)
            _darkOverlay.gameObject.SetActive(false);
    }

    private static float CalculateScreenCoveringDiameter(Vector3 centerWorld, Camera mainCam)
    {
        float worldHeight = mainCam.orthographicSize * 2f;
        float worldWidth = worldHeight * mainCam.aspect;
        Vector3 camPos = mainCam.transform.position;

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
        if (_jumpscareAnimator == null)
            return;

        int sortingLayerID = _darkOverlay != null
            ? _darkOverlay.sortingLayerID
            : (_blinkOverlay != null ? _blinkOverlay.sortingLayerID : 0);

        Renderer[] renderers = _jumpscareAnimator.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.sortingLayerID = sortingLayerID;
            renderer.sortingOrder = JumpscareTopSortingOrder;
        }
    }
}
