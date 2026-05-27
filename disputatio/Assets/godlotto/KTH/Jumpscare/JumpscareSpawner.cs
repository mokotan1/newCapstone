using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene entry rolls, trigger placement, guaranteed delayed spawn, and hallway UI hiding for <see cref="JumpscareManager"/>.
/// Coroutine flows return <see cref="IEnumerator"/> for the host behaviour to run.
/// </summary>
public sealed class JumpscareSpawner
{
    private const float NonRightHallSpawnChancePercent = 20f;
    private const string RightHallSceneName = SceneNames.HallRight;
    private const string SpriteUnlitShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string MainCanvasTag = "MainCanvas";

    private readonly GameObject _triggerObject;
    private readonly SpriteRenderer _triggerSpriteRenderer;
    private readonly Collider2D _triggerCollider;
    private readonly SpriteRenderer _blinkOverlay;
    private readonly JumpscareEffects _effects;
    private readonly List<JumpscareSceneData> _targetScenes;
    private readonly float _triggerWorldZOffsetFromCamera;
    private readonly float _waitTimeToScare;
    private readonly float _guaranteedJumpscareAfterSeconds;
    private readonly string _hideObjectTag;
    private readonly bool _useUnlitMaterialForTrigger;
    private readonly Func<bool> _isSpawnConsumed;
    private readonly Action _onEnemyAppeared;
    private readonly Action _executeJumpscare;

#if UNITY_EDITOR
    private readonly bool _logTriggerRenderingAfterSpawn;
#endif

    private readonly List<GameObject> _hiddenMainCanvases = new List<GameObject>();
    private readonly MonoBehaviour _coroutineHost;
    private Camera _mainCam;

    public JumpscareSpawner(
        MonoBehaviour coroutineHost,
        GameObject triggerObject,
        SpriteRenderer triggerSpriteRenderer,
        Collider2D triggerCollider,
        SpriteRenderer blinkOverlay,
        JumpscareEffects effects,
        List<JumpscareSceneData> targetScenes,
        float triggerWorldZOffsetFromCamera,
        float waitTimeToScare,
        float guaranteedJumpscareAfterSeconds,
        string hideObjectTag,
        bool useUnlitMaterialForTrigger,
        Func<bool> isSpawnConsumed,
        Action onEnemyAppeared,
        Action executeJumpscare
#if UNITY_EDITOR
        , bool logTriggerRenderingAfterSpawn
#endif
        )
    {
        _coroutineHost = coroutineHost;
        _triggerObject = triggerObject;
        _triggerSpriteRenderer = triggerSpriteRenderer;
        _triggerCollider = triggerCollider;
        _blinkOverlay = blinkOverlay;
        _effects = effects;
        _targetScenes = targetScenes;
        _triggerWorldZOffsetFromCamera = triggerWorldZOffsetFromCamera;
        _waitTimeToScare = waitTimeToScare;
        _guaranteedJumpscareAfterSeconds = guaranteedJumpscareAfterSeconds;
        _hideObjectTag = hideObjectTag;
        _useUnlitMaterialForTrigger = useUnlitMaterialForTrigger;
        _isSpawnConsumed = isSpawnConsumed;
        _onEnemyAppeared = onEnemyAppeared;
        _executeJumpscare = executeJumpscare;
#if UNITY_EDITOR
        _logTriggerRenderingAfterSpawn = logTriggerRenderingAfterSpawn;
#endif
    }

    public void ApplyUnlitTriggerMaterialIfNeeded()
    {
        if (!_useUnlitMaterialForTrigger || _triggerSpriteRenderer == null)
            return;

        Shader shader = Shader.Find(SpriteUnlitShaderName);
        if (shader == null)
        {
            GameLog.LogWarning($"[JumpscareSpawner] 셰이더를 찾을 수 없습니다: '{SpriteUnlitShaderName}'. 트리거 머티리얼을 바꾸지 않습니다.");
            return;
        }

        _triggerSpriteRenderer.material = new Material(shader);
    }

    public void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (_targetScenes == null)
            return;

        bool isTargetScene = false;

        foreach (var data in _targetScenes)
        {
            if (data.sceneName != scene.name)
                continue;

            isTargetScene = true;
            float effectiveSpawnChance = IsRightHallScene(scene.name) ? data.spawnChance : NonRightHallSpawnChancePercent;
            float randomValue = UnityEngine.Random.Range(0f, 100f);
            if (randomValue <= effectiveSpawnChance)
                _coroutineHost.StartCoroutine(SpawnTriggerFlow(data.spawnPosition));
            else if (_guaranteedJumpscareAfterSeconds > 0f)
                _coroutineHost.StartCoroutine(GuaranteedSpawnAfterStay(scene.name, data.spawnPosition));
            break;
        }

        if (!isTargetScene)
            HideAllJumpscareObjects();
    }

    private static bool IsRightHallScene(string sceneName)
    {
        return string.Equals(sceneName, RightHallSceneName, StringComparison.Ordinal);
    }

    public IEnumerator GuaranteedSpawnAfterStay(string expectedSceneName, Vector2 spawnPosition)
    {
        yield return new WaitForSeconds(_guaranteedJumpscareAfterSeconds);
        if (_isSpawnConsumed())
            yield break;
        if (SceneManager.GetActiveScene().name != expectedSceneName)
            yield break;

        yield return SpawnTriggerFlow(spawnPosition);
    }

    public IEnumerator SpawnTriggerFlow(Vector2 spawnPos)
    {
        PrepareSpawn(spawnPos);

#if UNITY_EDITOR
        if (_logTriggerRenderingAfterSpawn && _triggerObject != null)
            _coroutineHost.StartCoroutine(DebugLogTriggerRenderingState());
#endif

        yield return new WaitForSeconds(_waitTimeToScare);
        _executeJumpscare();
    }

    private void PrepareSpawn(Vector2 spawnPos)
    {
        if (_triggerObject != null)
            _triggerObject.SetActive(true);
        if (_blinkOverlay != null)
            _blinkOverlay.gameObject.SetActive(true);

        if (_triggerObject != null)
        {
            float worldZ = GetTriggerWorldPlaneZ();

            if (_mainCam == null)
                _mainCam = Camera.main;

            Vector2 cameraCenter = _mainCam != null ? (Vector2)_mainCam.transform.position : Vector2.zero;
            float wx = cameraCenter.x + spawnPos.x;
            float wy = cameraCenter.y + spawnPos.y;
            _triggerObject.transform.position = new Vector3(wx, wy, worldZ);
        }

        SetTriggerVisible(true);
        SetHideObjectsByTag(true);
        SetMainCanvasVisible(false);
        _onEnemyAppeared?.Invoke();
    }

    private float GetTriggerWorldPlaneZ()
    {
        if (_mainCam == null)
            _mainCam = Camera.main;
        if (_mainCam == null)
            return 0f;
        return _mainCam.transform.position.z + _triggerWorldZOffsetFromCamera;
    }

    public void SetTriggerVisible(bool visible)
    {
        if (_triggerSpriteRenderer != null)
            _triggerSpriteRenderer.enabled = visible;
        if (_triggerCollider != null)
            _triggerCollider.enabled = visible;
    }

    public void SetHideObjectsByTag(bool hide)
    {
        if (string.IsNullOrEmpty(_hideObjectTag))
            return;

        GameObject[] targets = GameObject.FindGameObjectsWithTag(_hideObjectTag);
        foreach (var obj in targets)
        {
            if (obj != null)
                obj.SetActive(!hide);
        }
    }

    public void SetMainCanvasVisible(bool visible)
    {
        if (!visible)
        {
            _hiddenMainCanvases.Clear();
            GameObject[] canvases;
            try
            {
                canvases = GameObject.FindGameObjectsWithTag(MainCanvasTag);
            }
            catch (UnityException)
            {
                return;
            }

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.activeSelf)
                    continue;

                canvas.SetActive(false);
                _hiddenMainCanvases.Add(canvas);
            }
            return;
        }

        for (int i = 0; i < _hiddenMainCanvases.Count; i++)
        {
            GameObject canvas = _hiddenMainCanvases[i];
            if (canvas != null)
                canvas.SetActive(true);
        }
        _hiddenMainCanvases.Clear();
    }

    public void HideAllJumpscareObjects()
    {
        if (_triggerObject != null)
            _triggerObject.SetActive(false);
        _effects.HideAllVisualLayers();
    }

#if UNITY_EDITOR
    private IEnumerator DebugLogTriggerRenderingState()
    {
        yield return null;

        if (_triggerSpriteRenderer == null || _triggerObject == null)
            yield break;

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameLog.Log("[JumpscareSpawner] DebugTrigger: Main Camera 없음");
            yield break;
        }

        cam.WorldToViewportPoint(_triggerObject.transform.position);
    }
#endif
}
