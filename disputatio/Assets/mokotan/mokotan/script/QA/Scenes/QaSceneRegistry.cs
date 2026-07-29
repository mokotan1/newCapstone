#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Godlotto.QA.Scenes
{
    /// <summary>
    /// <see cref="QaSceneRegistry.Register"/> 호출 한 건의 불변 결과.
    /// </summary>
    public sealed class QaSceneRegistrationResult
    {
        public bool IsSuccess { get; }

        public string Message { get; }

        private QaSceneRegistrationResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
        }

        public static QaSceneRegistrationResult Success(string message = null)
        {
            return new QaSceneRegistrationResult(true, message);
        }

        public static QaSceneRegistrationResult Failure(string message)
        {
            return new QaSceneRegistrationResult(false, message);
        }
    }

    /// <summary>
    /// <see cref="QaSceneRegistry.TryResolveTarget"/>이 반환하는, 대상 ID와 그 대상을 소유한
    /// 어댑터를 묶은 불변 바인딩. 실제 GameObject/컴포넌트 참조 해석은 구체 어댑터(Task 12+)의
    /// 책임이며, 이 레지스트리 계층은 "어느 어댑터가 이 ID를 소유하는가"만 권위 있게 답합니다.
    /// </summary>
    public sealed class QaResolvedTarget
    {
        public QaTargetId TargetId { get; }

        public IQaSceneAdapter Adapter { get; }

        private QaResolvedTarget(QaTargetId targetId, IQaSceneAdapter adapter)
        {
            TargetId = targetId;
            Adapter = adapter;
        }

        public static QaResolvedTarget Create(QaTargetId targetId, IQaSceneAdapter adapter)
        {
            return new QaResolvedTarget(targetId, adapter);
        }
    }

    /// <summary>
    /// 씬 이름 → <see cref="IQaSceneAdapter"/>, 그리고 전역적으로 고유해야 하는 안정적
    /// <see cref="QaTargetId"/> → 소유 어댑터를 관리하는 레지스트리(디자인 문서 §4.4).
    /// 지원하지 않는 씬은 절대 이름 유사도로 대체 추측하지 않고 명시적으로 실패를
    /// 반환합니다(<see cref="TryResolveScene"/>). 등록 시 대상 ID 충돌(같은 어댑터 내부 중복
    /// 또는 다른 어댑터와의 충돌)은 전체를 원자적으로 거부합니다(부분 등록 없음).
    /// </summary>
    public sealed class QaSceneRegistry
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, IQaSceneAdapter> adaptersBySceneName =
            new Dictionary<string, IQaSceneAdapter>(StringComparer.Ordinal);
        private readonly Dictionary<QaTargetId, IQaSceneAdapter> adaptersByTargetId =
            new Dictionary<QaTargetId, IQaSceneAdapter>();

        /// <summary>
        /// 어댑터를 등록합니다. 다음 중 하나라도 위반하면 레지스트리 상태를 전혀 바꾸지 않고
        /// 실패를 반환합니다: null 어댑터, 빈 씬 이름, 이미 다른 어댑터가 소유한 씬 이름,
        /// 어댑터 내부의 중복 대상 ID, 이미 다른 어댑터가 소유한 대상 ID와의 충돌.
        /// </summary>
        public QaSceneRegistrationResult Register(IQaSceneAdapter adapter)
        {
            if (adapter == null)
            {
                return QaSceneRegistrationResult.Failure("Adapter must not be null.");
            }

            string sceneName = adapter.SceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return QaSceneRegistrationResult.Failure("Adapter.SceneName must not be blank.");
            }

            lock (sync)
            {
                if (adaptersBySceneName.TryGetValue(sceneName, out IQaSceneAdapter existingSceneOwner))
                {
                    return QaSceneRegistrationResult.Failure(
                        "Scene '" + sceneName + "' is already registered by adapter '" +
                        existingSceneOwner.GetType().Name + "'.");
                }

                IReadOnlyCollection<QaTargetId> declaredTargetIds = adapter.TargetIds
                    ?? Array.Empty<QaTargetId>();

                // 원자적 등록을 위해, 먼저 전체 대상 ID를 검증만 하고 아무 것도 커밋하지 않습니다.
                var seenWithinThisAdapter = new HashSet<QaTargetId>();
                foreach (QaTargetId targetId in declaredTargetIds)
                {
                    if (!seenWithinThisAdapter.Add(targetId))
                    {
                        return QaSceneRegistrationResult.Failure(
                            "Scene '" + sceneName + "' declares duplicate target id '" + targetId +
                            "' more than once. Hierarchy diagnostics: scene='" + sceneName +
                            "', target='" + targetId + "' (declared twice by the same adapter).");
                    }

                    if (adaptersByTargetId.TryGetValue(targetId, out IQaSceneAdapter existingTargetOwner))
                    {
                        return QaSceneRegistrationResult.Failure(
                            "Target id '" + targetId + "' is already registered by scene '" +
                            existingTargetOwner.SceneName + "' and cannot be re-registered by scene '" +
                            sceneName + "'. Hierarchy diagnostics: existing owner scene='" +
                            existingTargetOwner.SceneName + "', target='" + targetId +
                            "'; conflicting registration scene='" + sceneName + "', target='" + targetId + "'.");
                    }
                }

                // 검증을 모두 통과했을 때만 커밋합니다.
                adaptersBySceneName[sceneName] = adapter;
                foreach (QaTargetId targetId in declaredTargetIds)
                {
                    adaptersByTargetId[targetId] = adapter;
                }

                return QaSceneRegistrationResult.Success(
                    "Scene '" + sceneName + "' registered with " + declaredTargetIds.Count + " target id(s).");
            }
        }

        /// <summary>
        /// 씬 이름으로 어댑터를 조회합니다. 정확히 일치하는 등록만 인정하며, 이름 유사도나
        /// 대소문자 무시 등의 최선 추측(best-effort) 탐색은 절대 수행하지 않습니다.
        /// 지원하지 않는 씬이면 false를 반환하고 <paramref name="adapter"/>는 null입니다.
        /// </summary>
        public bool TryResolveScene(string sceneName, out IQaSceneAdapter adapter)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                adapter = null;
                return false;
            }

            lock (sync)
            {
                return adaptersBySceneName.TryGetValue(sceneName, out adapter);
            }
        }

        /// <summary>
        /// 안정적 대상 ID로 그 대상을 소유한 어댑터를 조회합니다. 등록되지 않은 ID면 false를
        /// 반환하고 <paramref name="target"/>은 null입니다.
        /// </summary>
        public bool TryResolveTarget(QaTargetId targetId, out QaResolvedTarget target)
        {
            lock (sync)
            {
                if (adaptersByTargetId.TryGetValue(targetId, out IQaSceneAdapter owner))
                {
                    target = QaResolvedTarget.Create(targetId, owner);
                    return true;
                }
            }

            target = null;
            return false;
        }

        /// <summary>현재 등록된 씬 이름 전체(진단/감사용). 절대 null이 아닙니다.</summary>
        public IReadOnlyCollection<string> RegisteredSceneNames
        {
            get
            {
                lock (sync)
                {
                    return new List<string>(adaptersBySceneName.Keys);
                }
            }
        }

        /// <summary>
        /// Build Settings에서 활성화된 씬 이름 목록을 받아, 등록된 어댑터가 없는 씬 이름만
        /// 순서를 보존하여 반환합니다(순수 함수 — <c>UnityEditor.EditorBuildSettings</c>를 직접
        /// 참조하지 않으므로 EditMode/PlayMode 모두에서 단위 테스트가 가능합니다).
        /// 롤아웃 기간에는 이 목록을 보고용으로만 사용하고, Task 13에서 하드 실패로
        /// 전환합니다(디자인 문서 §10 Rollout 6단계).
        /// </summary>
        public IReadOnlyList<string> AuditMissingAdapterScenes(IEnumerable<string> enabledSceneNames)
        {
            var missing = new List<string>();
            if (enabledSceneNames == null)
            {
                return missing;
            }

            lock (sync)
            {
                foreach (string sceneName in enabledSceneNames)
                {
                    if (string.IsNullOrWhiteSpace(sceneName))
                    {
                        continue;
                    }

                    if (!adaptersBySceneName.ContainsKey(sceneName))
                    {
                        missing.Add(sceneName);
                    }
                }
            }

            return missing;
        }
    }
}
#endif
