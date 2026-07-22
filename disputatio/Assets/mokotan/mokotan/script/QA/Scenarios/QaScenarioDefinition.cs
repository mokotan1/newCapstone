#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Godlotto.QA.Scenarios
{
    /// <summary>
    /// 시나리오 JSON(스키마 v1)이 선언할 수 있는 명시적 스텝 명령 종류(Task 9). 문자열은
    /// <see cref="QaScenarioSchema.CommandKindsByName"/>에 등록된 것만 인정되며, 이 목록 밖의
    /// 문자열/임의의 C# 멤버 이름/리플렉션 경로는 절대 실행되지 않습니다.
    /// </summary>
    public enum QaScenarioCommandKind
    {
        InteractionPointer,
        InteractionDrag,
        InteractionKey,
        StateAssert
    }

    /// <summary>
    /// 시나리오 JSON 스키마 v1이 인식하는 명령/어서션 이름의 권위 있는 화이트리스트
    /// (Task 9 §Step 2). <see cref="QaScenarioValidator"/>와 <see cref="QaScenarioRunner"/>가
    /// 동일한 이 사전만을 참조하므로, "검증이 통과시킨 명령/어서션"과 "런너가 실제로 실행하는
    /// 명령/어서션"이 항상 하나의 원천(single source of truth)에서 나옵니다.
    /// </summary>
    public static class QaScenarioSchema
    {
        /// <summary>현재 지원하는 유일한 스키마 버전. 다른 값은 항상 검증 실패입니다.</summary>
        public const int SupportedSchemaVersion = 1;

        public const string CommandInteractionPointer = "interaction.pointer";
        public const string CommandInteractionDrag = "interaction.drag";
        public const string CommandInteractionKey = "interaction.key";
        public const string CommandStateAssert = "state.assert";

        /// <summary>JSON 명령 문자열 → 실행 가능한 <see cref="QaScenarioCommandKind"/>.</summary>
        public static readonly IReadOnlyDictionary<string, QaScenarioCommandKind> CommandKindsByName =
            new Dictionary<string, QaScenarioCommandKind>(StringComparer.Ordinal)
            {
                [CommandInteractionPointer] = QaScenarioCommandKind.InteractionPointer,
                [CommandInteractionDrag] = QaScenarioCommandKind.InteractionDrag,
                [CommandInteractionKey] = QaScenarioCommandKind.InteractionKey,
                [CommandStateAssert] = QaScenarioCommandKind.StateAssert
            };

        /// <summary>
        /// JSON 어서션 <c>kind</c> 문자열(lowerCamelCase) → <see cref="QaAssertionKind"/>. Task 8에서
        /// 정의된 열거형 값과 1:1로 대응하며, 새 의미가 필요하면 <see cref="QaAssertionKind"/>와
        /// 이 사전에 동시에 추가해야 합니다.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, QaAssertionKind> AssertionKindsByName =
            new Dictionary<string, QaAssertionKind>(StringComparer.Ordinal)
            {
                ["fieldEquals"] = QaAssertionKind.FieldEquals,
                ["fieldBoolean"] = QaAssertionKind.FieldBoolean,
                ["inventoryContains"] = QaAssertionKind.InventoryContains,
                ["targetActive"] = QaAssertionKind.TargetActive,
                ["targetInteractable"] = QaAssertionKind.TargetInteractable,
                ["questCurrentStepEquals"] = QaAssertionKind.QuestCurrentStepEquals,
                ["questStepCompleted"] = QaAssertionKind.QuestStepCompleted,
                ["inputUnlocked"] = QaAssertionKind.InputUnlocked,
                ["flowchartIdle"] = QaAssertionKind.FlowchartIdle,
                ["noNewConsoleError"] = QaAssertionKind.NoNewConsoleError
            };
    }

    /// <summary>
    /// 시나리오 JSON 스키마 v1의 최상위 형태를 그대로 반영하는 배선(wire) DTO(Task 9 §Step 2).
    /// Newtonsoft.Json의 표준 POCO 바인딩(<c>TypeNameHandling.None</c>, 커스텀 컨버터 없음)으로만
    /// 채워지며, 임의의 C# 타입 이름/메서드 이름/리플렉션 경로는 절대 JSON으로부터 실행되지
    /// 않습니다. 이 타입 자체는 아직 검증되지 않은 원본 입력이며, <see cref="QaScenarioValidator"/>가
    /// 유효성을 확정한 뒤에만 <see cref="QaScenarioRunner"/>에 전달되어야 합니다(관용적
    /// 불변성 — 검증 이후에는 어떤 코드도 이 인스턴스의 속성을 다시 쓰지 않습니다).
    /// </summary>
    public sealed class QaScenarioDefinition
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("scene")]
        public string Scene { get; set; }

        [JsonProperty("preset")]
        public string Preset { get; set; }

        [JsonProperty("steps")]
        public List<QaScenarioStepDefinition> Steps { get; set; }
    }

    /// <summary>시나리오 JSON의 <c>steps[]</c> 배열 원소 하나를 반영하는 배선 DTO.</summary>
    public sealed class QaScenarioStepDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary><see cref="QaScenarioSchema.CommandKindsByName"/>의 키 중 하나여야 합니다.</summary>
        [JsonProperty("command")]
        public string Command { get; set; }

        /// <summary><c>interaction.pointer</c>/<c>interaction.key</c>의 대상, <c>interaction.drag</c>의 출발 대상.</summary>
        [JsonProperty("target")]
        public string Target { get; set; }

        /// <summary><c>interaction.drag</c>에서만 사용하는 도착 대상.</summary>
        [JsonProperty("destinationTarget")]
        public string DestinationTarget { get; set; }

        /// <summary><c>interaction.key</c>에서만 사용하는 입력 텍스트.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }

        /// <summary><c>state.assert</c>에서만 사용하는 어서션 정의.</summary>
        [JsonProperty("assertion")]
        public QaScenarioAssertionDefinition Assertion { get; set; }

        /// <summary>이 스텝의 최대 대기 시간(밀리초). 항상 양수여야 합니다.</summary>
        [JsonProperty("timeoutMs")]
        public int TimeoutMs { get; set; }
    }

    /// <summary>시나리오 JSON의 <c>steps[].assertion</c> 객체를 반영하는 배선 DTO.</summary>
    public sealed class QaScenarioAssertionDefinition
    {
        /// <summary><see cref="QaScenarioSchema.AssertionKindsByName"/>의 키 중 하나여야 합니다.</summary>
        [JsonProperty("kind")]
        public string Kind { get; set; }

        /// <summary><c>fieldEquals</c>/<c>fieldBoolean</c>에서 조회할 필드 이름.</summary>
        [JsonProperty("field")]
        public string Field { get; set; }

        /// <summary>
        /// 종류에 따라 기대 문자열(<c>fieldEquals</c>), 아이템 id(<c>inventoryContains</c>), 대상/
        /// Flowchart/퀘스트 단계 id로 사용되는 값.
        /// </summary>
        [JsonProperty("value")]
        public string Value { get; set; }

        /// <summary>불리언 기대값을 쓰는 종류의 기대값. 생략하면 <c>true</c>로 취급합니다.</summary>
        [JsonProperty("expected")]
        public bool? Expected { get; set; }

        /// <summary>사람이 읽을 수 있는 어서션 설명(선택).</summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// 검증을 통과한 이 정의를 실행 가능한 <see cref="QaAssertion"/>으로 변환합니다.
        /// <see cref="QaScenarioValidator"/>가 이미 <see cref="Kind"/>와 필수 필드를 검증했다는
        /// 전제 아래 호출되어야 하며, 그렇지 않은 경우(예: 검증되지 않은 정의를 직접 실행)
        /// <see cref="InvalidOperationException"/>을 던집니다 — 시나리오 JSON으로부터 임의의
        /// 동작을 추론하지 않고, 알려진 종류만 명시적으로 매핑합니다.
        /// </summary>
        public QaAssertion ToAssertion()
        {
            if (string.IsNullOrWhiteSpace(Kind)
                || !QaScenarioSchema.AssertionKindsByName.TryGetValue(Kind, out QaAssertionKind kind))
            {
                throw new InvalidOperationException(
                    "Assertion kind '" + Kind + "' is not a known QA assertion kind.");
            }

            bool expected = Expected ?? true;

            switch (kind)
            {
                case QaAssertionKind.FieldEquals:
                    return QaAssertion.FieldEquals(Field, Value ?? string.Empty, Description);
                case QaAssertionKind.FieldBoolean:
                    return QaAssertion.FieldBoolean(Field, expected, Description);
                case QaAssertionKind.InventoryContains:
                    return QaAssertion.InventoryContains(ParseItemId(Value), Description);
                case QaAssertionKind.TargetActive:
                    return QaAssertion.TargetActive(Value, expected, Description);
                case QaAssertionKind.TargetInteractable:
                    return QaAssertion.TargetInteractable(Value, expected, Description);
                case QaAssertionKind.QuestCurrentStepEquals:
                    return QaAssertion.QuestCurrentStepEquals(Value, Description);
                case QaAssertionKind.QuestStepCompleted:
                    return QaAssertion.QuestStepCompleted(Value, Description);
                case QaAssertionKind.InputUnlocked:
                    return QaAssertion.InputUnlocked(Description);
                case QaAssertionKind.FlowchartIdle:
                    return QaAssertion.FlowchartIdle(Value, expected, Description);
                case QaAssertionKind.NoNewConsoleError:
                    return QaAssertion.NoNewConsoleError(Description);
                default:
                    throw new InvalidOperationException(
                        "Assertion kind '" + Kind + "' has no runtime mapping.");
            }
        }

        private static int ParseItemId(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId)
                ? itemId
                : -1;
        }
    }
}
#endif
