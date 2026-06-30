using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모달 UI(설정·대사 로그·체셔/튜터 패널·책·지도·차단기 등)가 열려 있는 동안
/// 그 뒤의 월드 오브젝트 클릭과 HUD 버튼 클릭을 공통으로 차단하는 전역 게이트.
///
/// 여러 모달이 겹쳐 열릴 수 있으므로 owner 집합(stack)으로 관리하며,
/// owner 가 파괴/비활성화되어도 잠금이 영구히 남지 않도록 매 조회 시 정리(prune)합니다.
///
/// 기존 <see cref="InteractionLock"/>(연타·재진입 방지)과는 독립적인 계층입니다.
/// <see cref="InteractionLock"/>은 "클릭으로 시작된 Fungus 블록이 끝날 때까지" 잠그고,
/// 이 게이트는 "모달 UI가 열려 있는 동안" 뒤쪽 입력을 막습니다.
/// </summary>
public static class ModalInputGate
{
    private sealed class Scope
    {
        public object Owner;
        public GameObject AllowedRoot;
        public bool BlocksWorld;
        public bool BlocksHud;

        // allowedRoot 가 "한 번이라도 지정된 적이 있는지" 기록합니다.
        // 처음부터 null 로 시작한 차단-전용 스코프와, 루트가 파괴되어 null 이 된 스코프를
        // 구분하기 위해 필요합니다(영구 차단 방지, IsDeadScope 참고).
        public bool HadAllowedRoot;
    }

    private static readonly List<Scope> ActiveScopes = new List<Scope>();

    /// <summary>활성 모달 스코프가 하나라도 있으면 true.</summary>
    public static bool HasActiveScope
    {
        get
        {
            Prune();
            return ActiveScopes.Count > 0;
        }
    }

    public static bool IsBlockingWorldInput
    {
        get
        {
            Prune();
            for (int i = 0; i < ActiveScopes.Count; i++)
            {
                if (ActiveScopes[i].BlocksWorld)
                    return true;
            }

            return false;
        }
    }

    public static bool IsBlockingHudInput
    {
        get
        {
            Prune();
            for (int i = 0; i < ActiveScopes.Count; i++)
            {
                if (ActiveScopes[i].BlocksHud)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 모달을 열 때 호출합니다. 같은 owner 로 다시 호출하면 설정만 갱신합니다.
    /// </summary>
    /// <param name="owner">스코프 소유자. UnityEngine.Object 면 파괴 시 자동 정리됩니다.</param>
    /// <param name="allowedRoot">이 루트의 하위 오브젝트는 입력이 허용됩니다(모달 내부 버튼 등).</param>
    public static void Begin(object owner, GameObject allowedRoot, bool blocksHud = true, bool blocksWorld = true)
    {
        if (owner == null)
            return;

        Prune();

        for (int i = 0; i < ActiveScopes.Count; i++)
        {
            if (ReferenceEquals(ActiveScopes[i].Owner, owner))
            {
                Scope existing = ActiveScopes[i];
                existing.AllowedRoot = allowedRoot;
                existing.BlocksWorld = blocksWorld;
                existing.BlocksHud = blocksHud;
                existing.HadAllowedRoot = allowedRoot != null;

                // 같은 owner 로 다시 열리면 "가장 최근 활성화된 모달 = 최상단" 불변식을 위해
                // 리스트 끝(최상단)으로 이동시킵니다. (IsAllowed 가 최상단만 허용)
                if (i != ActiveScopes.Count - 1)
                {
                    ActiveScopes.RemoveAt(i);
                    ActiveScopes.Add(existing);
                }

                return;
            }
        }

        ActiveScopes.Add(new Scope
        {
            Owner = owner,
            AllowedRoot = allowedRoot,
            BlocksWorld = blocksWorld,
            BlocksHud = blocksHud,
            HadAllowedRoot = allowedRoot != null,
        });
    }

    /// <summary>모달을 닫을 때 호출합니다. 해당 owner 의 스코프만 제거합니다.</summary>
    public static void End(object owner)
    {
        for (int i = ActiveScopes.Count - 1; i >= 0; i--)
        {
            if (owner == null || ReferenceEquals(ActiveScopes[i].Owner, owner))
                ActiveScopes.RemoveAt(i);
        }

        Prune();
    }

    /// <summary>
    /// 대상이 입력을 받을 수 있는지. 활성 스코프가 없으면 항상 허용합니다.
    /// 모달이 겹쳐 있을 때는 모달 스택 의미에 맞게 <b>최상단(가장 최근 활성화된) 스코프</b>의
    /// allowedRoot 하위일 때만 허용합니다. 그래서 뒤쪽 모달의 내부 버튼은 막힙니다.
    /// </summary>
    public static bool IsAllowed(GameObject target)
    {
        Prune();

        if (ActiveScopes.Count == 0)
            return true;

        if (target == null)
            return false;

        // 최상단 스코프(리스트 마지막)만 입력을 허용합니다.
        Scope top = ActiveScopes[ActiveScopes.Count - 1];
        GameObject root = top.AllowedRoot;
        if (root == null)
            return false;

        Transform targetTransform = target.transform;
        return targetTransform == root.transform || targetTransform.IsChildOf(root.transform);
    }

    /// <summary><see cref="IsAllowed"/> 의 의미상 별칭.</summary>
    public static bool CanReceiveInput(GameObject target) => IsAllowed(target);

    /// <summary>월드 2D 오브젝트 클릭을 처리해도 되는지.</summary>
    public static bool CanWorldClick(GameObject target)
    {
        if (!IsBlockingWorldInput)
            return true;

        return IsAllowed(target);
    }

    /// <summary>이동/지도/뒤로가기 같은 HUD 버튼을 눌러도 되는지.</summary>
    public static bool CanUseHudButton(GameObject target)
    {
        if (!IsBlockingHudInput)
            return true;

        return IsAllowed(target);
    }

    private static void Prune()
    {
        for (int i = ActiveScopes.Count - 1; i >= 0; i--)
        {
            if (IsDeadScope(ActiveScopes[i]))
                ActiveScopes.RemoveAt(i);
        }
    }

    private static bool IsDeadScope(Scope scope)
    {
        if (scope == null)
            return true;

        object owner = scope.Owner;
        if (owner == null)
            return true;

        // owner 가 UnityEngine.Object(컴포넌트/게임오브젝트)면 파괴 여부를 확인합니다.
        if (owner is Object unityOwner && unityOwner == null)
            return true;

        // 컴포넌트 owner 가 비활성/파괴 상태면 OnDisable 이 누락되어도 자동 정리합니다.
        if (owner is Component component)
        {
            if (component == null || component.gameObject == null)
                return true;
            if (!component.gameObject.activeInHierarchy)
                return true;
        }

        // allowedRoot 가 한번 설정됐다가 파괴되어 null 이 되면 영구 차단을 막기 위해 정리합니다.
        // (처음부터 null 로 시작한 차단-전용 스코프는 BlocksWorld/Hud 의도를 유지합니다.)
        if (scope.HadAllowedRoot && scope.AllowedRoot == null)
            return true;

        return false;
    }

    /// <summary>
    /// 모든 모달 스코프를 즉시 해제합니다. 테스트 초기화 및
    /// 씬 전환 등 강제 정리가 필요한 경계에서 호출할 수 있습니다.
    /// </summary>
    public static void ResetForTests()
    {
        ActiveScopes.Clear();
    }
}
