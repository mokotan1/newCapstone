#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Godlotto.QA.Evidence
{
    /// <summary>
    /// <see cref="QaDriverSnapshot.AiConnectionState"/>가 나타낼 수 있는 명시적 AI 챗봇 연결
    /// 상태. 원본 프롬프트/응답 텍스트는 어떤 형태로도 스냅샷에 담기지 않으므로, AI 관련
    /// 상태는 오직 이 닫힌 열거형으로만 노출됩니다(Task 8 설계 가이드: "NO raw prompts/responses/tokens").
    /// </summary>
    public enum QaAiConnectionState
    {
        Idle,
        Connecting,
        Connected,
        Error
    }

    /// <summary>
    /// QA 드라이버/시나리오가 어서션·조건 대기에 사용하는 권위 있는 진단 스냅샷(Task 8).
    /// Task 6의 임시 stub을 대체하며, <see cref="Godlotto.QA.Scenes.QaSceneSnapshot"/>(씬 어댑터별
    /// 얕은 key-value, Task 5)와 달리 인벤토리·퀘스트·패널/대상·입력 게이트·Fungus·AI 상태를
    /// 하나의 구조화된 타입으로 모읍니다. 필드는 모두 명시적 허용목록(allow-list)이며, 원본
    /// 챗봇 프롬프트/응답 텍스트나 비밀값(토큰·API 키 등)은 어떤 필드로도 절대 포함하지
    /// 않습니다 — 그런 필드 자체가 이 타입에 존재하지 않는다는 것이 그 보장의 근거입니다.
    ///
    /// <see cref="Values"/>는 위 타입 필드를 사람이 읽을 수 있는 얕은 key-value로 평탄화한
    /// 파생(derived) 뷰이며, <c>DevelopmentQaEvidenceRecorder</c>가 evidence 로그(JSON)에
    /// 첨부하는 용도와 <c>QaAssertion.FieldEquals</c>/<c>FieldBoolean</c>이 필드를 이름으로
    /// 조회하는 용도로 함께 사용됩니다.
    /// </summary>
    public sealed class QaDriverSnapshot
    {
        // Values 딕셔너리의 잘 알려진 키. 호출자가 문자열을 직접 흩어 쓰지 않고 이 상수만
        // 참조하도록 하여, QaAssertion.FieldEquals/FieldBoolean과 evidence 로그 소비자가 항상
        // 동일한 이름을 바라보게 합니다.
        public const string SceneNameKey = "SceneName";
        public const string QuestCurrentStepIdKey = "QuestCurrentStepId";
        public const string InputGateLockedKey = "InputGateLocked";
        public const string AiConnectionStateKey = "AiConnectionState";
        public const string ConsoleErrorCountKey = "ConsoleErrorCount";
        public const string InventoryItemIdsKey = "InventoryItemIds";
        public const string QuestCompletedStepIdsKey = "QuestCompletedStepIds";
        public const string TargetActivePrefix = "TargetActive.";
        public const string TargetInteractablePrefix = "TargetInteractable.";
        public const string FlowchartIdlePrefix = "FlowchartIdle.";

        private static readonly IReadOnlyList<int> EmptyItemIds = new ReadOnlyCollection<int>(new List<int>());
        private static readonly IReadOnlyList<string> EmptyStringList = new ReadOnlyCollection<string>(new List<string>());
        private static readonly IReadOnlyDictionary<string, bool> EmptyFlagMap =
            new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>());

        /// <summary>이 스냅샷이 속한 QA run의 식별자(문자열 표현). 없으면 빈 문자열.</summary>
        public string RunId { get; }

        /// <summary>스냅샷이 캡처된 UTC 시각.</summary>
        public DateTime CapturedAtUtc { get; }

        /// <summary>캡처 시점의 활성 씬 이름.</summary>
        public string SceneName { get; }

        /// <summary>인벤토리에 보유 중인 아이템 id 목록(<c>Item.itemId</c>, 1~30). 절대 null이 아닙니다.</summary>
        public IReadOnlyList<int> InventoryItemIds { get; }

        /// <summary>튜토리얼 퀘스트의 현재 단계 id(<c>TutorialQuestIds</c>). 없으면 빈 문자열.</summary>
        public string QuestCurrentStepId { get; }

        /// <summary>완료된 퀘스트 단계 id 목록. 절대 null이 아닙니다.</summary>
        public IReadOnlyList<string> QuestCompletedStepIds { get; }

        /// <summary>
        /// 안정적 대상/패널 식별자(<c>QaTargetId</c> 문자열 또는 패널 이름) → 활성(<c>GameObject.activeInHierarchy</c>
        /// 등) 여부. 절대 null이 아닙니다.
        /// </summary>
        public IReadOnlyDictionary<string, bool> TargetActiveStates { get; }

        /// <summary>안정적 대상 식별자 → 상호작용 가능(<c>Selectable.interactable</c> 등) 여부. 절대 null이 아닙니다.</summary>
        public IReadOnlyDictionary<string, bool> TargetInteractableStates { get; }

        /// <summary>전역 입력 게이트가 잠겨 있는지(대사 중·씬 전환 중 등). <c>true</c>면 입력이 차단됨.</summary>
        public bool InputGateLocked { get; }

        /// <summary>Fungus Flowchart 이름 → idle(현재 실행 중인 블록이 없음) 여부. 절대 null이 아닙니다.</summary>
        public IReadOnlyDictionary<string, bool> FlowchartIdleStates { get; }

        /// <summary>AI 챗봇 연결 상태(닫힌 열거형). 원본 프롬프트/응답 텍스트는 절대 포함하지 않습니다.</summary>
        public QaAiConnectionState AiConnectionState { get; }

        /// <summary>캡처 시점까지 기록된 Console 오류 누적 개수(원본 로그 텍스트는 포함하지 않음).</summary>
        public int ConsoleErrorCount { get; }

        /// <summary>
        /// 위 모든 허용목록 필드를 평탄화한 얕은 key-value 뷰. evidence 로그 첨부와
        /// <c>QaAssertion.FieldEquals</c>/<c>FieldBoolean</c> 조회에 사용됩니다. 절대 null이 아닙니다.
        /// </summary>
        public IReadOnlyDictionary<string, string> Values { get; }

        private QaDriverSnapshot(
            string runId,
            DateTime capturedAtUtc,
            string sceneName,
            IReadOnlyList<int> inventoryItemIds,
            string questCurrentStepId,
            IReadOnlyList<string> questCompletedStepIds,
            IReadOnlyDictionary<string, bool> targetActiveStates,
            IReadOnlyDictionary<string, bool> targetInteractableStates,
            bool inputGateLocked,
            IReadOnlyDictionary<string, bool> flowchartIdleStates,
            QaAiConnectionState aiConnectionState,
            int consoleErrorCount)
        {
            RunId = runId ?? string.Empty;
            CapturedAtUtc = capturedAtUtc;
            SceneName = sceneName ?? string.Empty;
            InventoryItemIds = inventoryItemIds ?? EmptyItemIds;
            QuestCurrentStepId = questCurrentStepId ?? string.Empty;
            QuestCompletedStepIds = questCompletedStepIds ?? EmptyStringList;
            TargetActiveStates = targetActiveStates ?? EmptyFlagMap;
            TargetInteractableStates = targetInteractableStates ?? EmptyFlagMap;
            InputGateLocked = inputGateLocked;
            FlowchartIdleStates = flowchartIdleStates ?? EmptyFlagMap;
            AiConnectionState = aiConnectionState;
            ConsoleErrorCount = consoleErrorCount;
            Values = BuildFlatValues(this);
        }

        /// <summary>
        /// 새 스냅샷을 생성합니다. 모든 파라미터가 선택적이며, 생략된 컬렉션은 빈 컬렉션으로
        /// 대체됩니다(호출자가 null 방어를 직접 하지 않아도 됨). 전달된 컬렉션은 방어적으로
        /// 복사되어, 이후 호출자 측 원본 컬렉션 변경이 이미 생성된 스냅샷에 영향을 주지 않습니다.
        /// </summary>
        public static QaDriverSnapshot Create(
            string runId = null,
            DateTime capturedAtUtc = default,
            string sceneName = null,
            IReadOnlyList<int> inventoryItemIds = null,
            string questCurrentStepId = null,
            IReadOnlyList<string> questCompletedStepIds = null,
            IReadOnlyDictionary<string, bool> targetActiveStates = null,
            IReadOnlyDictionary<string, bool> targetInteractableStates = null,
            bool inputGateLocked = false,
            IReadOnlyDictionary<string, bool> flowchartIdleStates = null,
            QaAiConnectionState aiConnectionState = QaAiConnectionState.Idle,
            int consoleErrorCount = 0)
        {
            return new QaDriverSnapshot(
                runId,
                capturedAtUtc,
                sceneName,
                CopyList(inventoryItemIds, EmptyItemIds),
                questCurrentStepId,
                CopyList(questCompletedStepIds, EmptyStringList),
                CopyMap(targetActiveStates),
                CopyMap(targetInteractableStates),
                inputGateLocked,
                CopyMap(flowchartIdleStates),
                aiConnectionState,
                consoleErrorCount);
        }

        private static IReadOnlyList<int> CopyList(IReadOnlyList<int> source, IReadOnlyList<int> empty)
        {
            return source == null || source.Count == 0 ? empty : new ReadOnlyCollection<int>(new List<int>(source));
        }

        private static IReadOnlyList<string> CopyList(IReadOnlyList<string> source, IReadOnlyList<string> empty)
        {
            return source == null || source.Count == 0 ? empty : new ReadOnlyCollection<string>(new List<string>(source));
        }

        private static IReadOnlyDictionary<string, bool> CopyMap(IReadOnlyDictionary<string, bool> source)
        {
            return source == null || source.Count == 0
                ? EmptyFlagMap
                : new ReadOnlyDictionary<string, bool>(new Dictionary<string, bool>(source));
        }

        private static IReadOnlyDictionary<string, string> BuildFlatValues(QaDriverSnapshot snapshot)
        {
            var flat = new Dictionary<string, string>
            {
                [SceneNameKey] = snapshot.SceneName,
                [QuestCurrentStepIdKey] = snapshot.QuestCurrentStepId,
                [InputGateLockedKey] = snapshot.InputGateLocked.ToString(CultureInfo.InvariantCulture),
                [AiConnectionStateKey] = snapshot.AiConnectionState.ToString(),
                [ConsoleErrorCountKey] = snapshot.ConsoleErrorCount.ToString(CultureInfo.InvariantCulture),
                [InventoryItemIdsKey] = FormatIntList(snapshot.InventoryItemIds),
                [QuestCompletedStepIdsKey] = FormatStringList(snapshot.QuestCompletedStepIds)
            };

            AppendFlagMap(flat, TargetActivePrefix, snapshot.TargetActiveStates);
            AppendFlagMap(flat, TargetInteractablePrefix, snapshot.TargetInteractableStates);
            AppendFlagMap(flat, FlowchartIdlePrefix, snapshot.FlowchartIdleStates);

            return new ReadOnlyDictionary<string, string>(flat);
        }

        private static void AppendFlagMap(
            Dictionary<string, string> target, string keyPrefix, IReadOnlyDictionary<string, bool> source)
        {
            foreach (KeyValuePair<string, bool> entry in source)
            {
                target[keyPrefix + entry.Key] = entry.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string FormatIntList(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
            {
                return string.Empty;
            }

            var parts = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private static string FormatStringList(IReadOnlyList<string> values)
        {
            return values.Count == 0 ? string.Empty : string.Join(",", values);
        }
    }
}
#endif
