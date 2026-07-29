#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// <see cref="QaDriverSnapshot"/>를 조립하는 조합기(Task 8). 실제 게임 상태(인벤토리 매니저,
    /// 퀘스트 트래커, Fungus Flowchart, 패널, 입력 게이트, AI 챗봇 연결 상태 등)를 직접 알지
    /// 못하며, 각 필드를 채우는 방법은 생성자로 주입되는 델리게이트(콜백)에만 의존합니다
    /// (DIP: 이 타입은 구체 게임 매니저가 아니라 <c>Func&lt;T&gt;</c> 계약에만 의존). 이렇게
    /// 하면 EditMode 단위 테스트가 씬을 로드하지 않고도 페이크 콜백만으로 스냅샷 캡처를
    /// 검증할 수 있습니다. 모든 콜백은 선택적이며, 생략되거나 예외를 던지면 안전한 기본값으로
    /// 대체됩니다(Fail-Safe: 진단 프로브 하나의 오류가 QA run 전체를 무너뜨리면 안 됨).
    /// </summary>
    public sealed class QaStateProbe
    {
        private static readonly IReadOnlyDictionary<string, bool> EmptyFlagMap = new Dictionary<string, bool>();

        private readonly Func<string> sceneNameProvider;
        private readonly Func<IReadOnlyList<int>> inventoryItemIdsProvider;
        private readonly Func<string> questCurrentStepIdProvider;
        private readonly Func<IReadOnlyList<string>> questCompletedStepIdsProvider;
        private readonly Func<IReadOnlyDictionary<string, bool>> targetActiveStatesProvider;
        private readonly Func<IReadOnlyDictionary<string, bool>> targetInteractableStatesProvider;
        private readonly Func<bool> inputGateLockedProvider;
        private readonly Func<IReadOnlyDictionary<string, bool>> flowchartIdleStatesProvider;
        private readonly Func<QaAiConnectionState> aiConnectionStateProvider;
        private readonly Func<int> consoleErrorCountProvider;
        private readonly Func<DateTime> utcNowProvider;

        /// <param name="sceneNameProvider">활성 씬 이름. 생략하면 빈 문자열.</param>
        /// <param name="inventoryItemIdsProvider">보유 인벤토리 아이템 id 목록. 생략하면 빈 목록.</param>
        /// <param name="questCurrentStepIdProvider">현재 퀘스트 단계 id. 생략하면 빈 문자열.</param>
        /// <param name="questCompletedStepIdsProvider">완료된 퀘스트 단계 id 목록. 생략하면 빈 목록.</param>
        /// <param name="targetActiveStatesProvider">대상/패널 id → 활성 여부. 생략하면 빈 맵.</param>
        /// <param name="targetInteractableStatesProvider">대상 id → 상호작용 가능 여부. 생략하면 빈 맵.</param>
        /// <param name="inputGateLockedProvider">전역 입력 게이트 잠금 여부. 생략하면 <c>false</c>(열림).</param>
        /// <param name="flowchartIdleStatesProvider">Flowchart 이름 → idle 여부. 생략하면 빈 맵.</param>
        /// <param name="aiConnectionStateProvider">AI 챗봇 연결 상태. 생략하면 <see cref="QaAiConnectionState.Idle"/>.</param>
        /// <param name="consoleErrorCountProvider">누적 Console 오류 개수. 생략하면 0.</param>
        /// <param name="utcNowProvider">테스트용 시각 주입 훅. 생략하면 <see cref="DateTime.UtcNow"/> 사용.</param>
        public QaStateProbe(
            Func<string> sceneNameProvider = null,
            Func<IReadOnlyList<int>> inventoryItemIdsProvider = null,
            Func<string> questCurrentStepIdProvider = null,
            Func<IReadOnlyList<string>> questCompletedStepIdsProvider = null,
            Func<IReadOnlyDictionary<string, bool>> targetActiveStatesProvider = null,
            Func<IReadOnlyDictionary<string, bool>> targetInteractableStatesProvider = null,
            Func<bool> inputGateLockedProvider = null,
            Func<IReadOnlyDictionary<string, bool>> flowchartIdleStatesProvider = null,
            Func<QaAiConnectionState> aiConnectionStateProvider = null,
            Func<int> consoleErrorCountProvider = null,
            Func<DateTime> utcNowProvider = null)
        {
            this.sceneNameProvider = sceneNameProvider;
            this.inventoryItemIdsProvider = inventoryItemIdsProvider;
            this.questCurrentStepIdProvider = questCurrentStepIdProvider;
            this.questCompletedStepIdsProvider = questCompletedStepIdsProvider;
            this.targetActiveStatesProvider = targetActiveStatesProvider;
            this.targetInteractableStatesProvider = targetInteractableStatesProvider;
            this.inputGateLockedProvider = inputGateLockedProvider;
            this.flowchartIdleStatesProvider = flowchartIdleStatesProvider;
            this.aiConnectionStateProvider = aiConnectionStateProvider;
            this.consoleErrorCountProvider = consoleErrorCountProvider;
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
        }

        /// <summary>
        /// 등록된 콜백을 모두 호출하여 하나의 <see cref="QaDriverSnapshot"/>을 캡처합니다.
        /// 개별 콜백이 예외를 던지면 그 필드만 안전한 기본값으로 대체되고, 나머지 필드
        /// 캡처는 계속 진행됩니다(부분 실패가 전체 캡처를 막지 않음).
        /// </summary>
        public QaDriverSnapshot Capture(string runId = null)
        {
            return QaDriverSnapshot.Create(
                runId: runId,
                capturedAtUtc: utcNowProvider(),
                sceneName: SafeInvoke(sceneNameProvider, string.Empty, nameof(sceneNameProvider)),
                inventoryItemIds: SafeInvoke(inventoryItemIdsProvider, Array.Empty<int>(), nameof(inventoryItemIdsProvider)),
                questCurrentStepId: SafeInvoke(questCurrentStepIdProvider, string.Empty, nameof(questCurrentStepIdProvider)),
                questCompletedStepIds: SafeInvoke(
                    questCompletedStepIdsProvider, Array.Empty<string>(), nameof(questCompletedStepIdsProvider)),
                targetActiveStates: SafeInvoke(targetActiveStatesProvider, EmptyFlagMap, nameof(targetActiveStatesProvider)),
                targetInteractableStates: SafeInvoke(
                    targetInteractableStatesProvider, EmptyFlagMap, nameof(targetInteractableStatesProvider)),
                inputGateLocked: SafeInvoke(inputGateLockedProvider, false, nameof(inputGateLockedProvider)),
                flowchartIdleStates: SafeInvoke(flowchartIdleStatesProvider, EmptyFlagMap, nameof(flowchartIdleStatesProvider)),
                aiConnectionState: SafeInvoke(
                    aiConnectionStateProvider, QaAiConnectionState.Idle, nameof(aiConnectionStateProvider)),
                consoleErrorCount: SafeInvoke(consoleErrorCountProvider, 0, nameof(consoleErrorCountProvider)));
        }

        private static T SafeInvoke<T>(Func<T> provider, T fallback, string providerName)
        {
            if (provider == null)
            {
                return fallback;
            }

            try
            {
                T value = provider();
                return value != null ? value : fallback;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[QaStateProbe] " + providerName + " threw: " + ex.GetType().Name);
                return fallback;
            }
        }
    }
}
#endif
