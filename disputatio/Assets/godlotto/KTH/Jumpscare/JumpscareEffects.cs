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

    private readonly SpriteRenderer _blinkOverlay;
    private readonly GameObject _gameOverObject;
    private readonly Animator _jumpscareAnimator;
    private readonly float _blinkDuration;
    private readonly float _closedDuration;

    private DepthOfField _dof;
    private readonly int _blinkAmountProp = Shader.PropertyToID("_BlinkAmount");
    private bool _isBlinkSequenceRunning;

    public bool IsBlinkSequenceRunning => _isBlinkSequenceRunning;

    public JumpscareEffects(
        SpriteRenderer blinkOverlay,
        GameObject gameOverObject,
        Animator jumpscareAnimator,
        float blinkDuration,
        float closedDuration)
    {
        _blinkOverlay = blinkOverlay;
        _gameOverObject = gameOverObject;
        _jumpscareAnimator = jumpscareAnimator;
        _blinkDuration = blinkDuration;
        _closedDuration = closedDuration;
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
        FitFullscreenSpriteRendererToMainCamera(_blinkOverlay);
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
    }

    public void SetAnimatorActive(bool active)
    {
        if (_jumpscareAnimator != null)
            _jumpscareAnimator.gameObject.SetActive(active);
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

    public IEnumerator FullJumpscareSequence()
    {
        yield return AnimateBlink(0.5f, 0f, 0f, 2.0f, _blinkDuration);
        yield return new WaitForSeconds(_closedDuration);

        if (_jumpscareAnimator != null)
        {
            _jumpscareAnimator.gameObject.SetActive(true);
        }

        yield return AnimateBlink(0f, 0.5f, 2.0f, 0f, _blinkDuration);
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
        SetAnimatorActive(false);
        SetGameOverActive(false);
        SetBlinkOverlayActive(false);
    }
}
