using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전역 오브젝트 클릭 잠금.
/// Clickable2D에서 ObjectClicked 이벤트가 발생하면 자동으로 잠금을 획득하고,
/// 그 클릭으로 시작된 Fungus 블록이 모두 종료되면 자동으로 해제됩니다.
///
/// 플로우차트마다 SetClickable2D를 수동으로 추가할 필요 없이
/// 인터랙션 중 다른 오브젝트 클릭을 전역에서 차단합니다.
/// </summary>
public static class InteractionLock
{
    static bool _locked;
    static int _pendingBlocks;
    static bool _firstBlockSeen;
    static float _lockedAtUnscaled;

    // 블록이 절대 시작되지 않는 극단적 상황에서 영구 잠금 방지
    const float SafetyTimeout = 20f;

    public static bool IsLocked
    {
        get
        {
            if (!_locked) return false;
            if (Time.unscaledTime - _lockedAtUnscaled > SafetyTimeout)
            {
                Unlock();
                return false;
            }
            return true;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    static void Init()
    {
        _locked = false;
        _pendingBlocks = 0;
        _firstBlockSeen = false;

        // -= 후 += : 에디터 도메인 리로드 없는 재시작 시 중복 구독 방지
        BlockSignals.OnBlockStart -= OnBlockStart;
        BlockSignals.OnBlockStart += OnBlockStart;
        BlockSignals.OnBlockEnd   -= OnBlockEnd;
        BlockSignals.OnBlockEnd   += OnBlockEnd;
        SceneManager.sceneLoaded  -= OnSceneLoaded;
        SceneManager.sceneLoaded  += OnSceneLoaded;
    }

    /// <summary>Clickable2D가 이벤트를 발생시키기 직전에 호출합니다.</summary>
    public static void AcquireForClick()
    {
        _locked = true;
        _pendingBlocks = 0;
        _firstBlockSeen = false;
        _lockedAtUnscaled = Time.unscaledTime;
    }

    /// <summary>외부에서 강제 해제가 필요한 경우 (예: 씬 전환 연출 등).</summary>
    public static void ForceUnlock() => Unlock();

    static void OnBlockStart(Block block)
    {
        if (!_locked) return;
        _pendingBlocks++;
        _firstBlockSeen = true;
    }

    static void OnBlockEnd(Block block)
    {
        // 첫 블록이 시작되기 전에 다른 블록이 끝나도 잠금 해제하지 않음
        if (!_locked || !_firstBlockSeen) return;
        _pendingBlocks = Mathf.Max(0, _pendingBlocks - 1);
        if (_pendingBlocks == 0)
            Unlock();
    }

    // 씬 전환 시 이전 씬의 잠금 상태가 남지 않도록 초기화
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Unlock();

    static void Unlock()
    {
        _locked = false;
        _pendingBlocks = 0;
        _firstBlockSeen = false;
    }
}
