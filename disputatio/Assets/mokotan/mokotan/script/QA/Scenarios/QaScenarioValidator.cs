#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godlotto.QA.Scenes;
using Newtonsoft.Json;

namespace Godlotto.QA.Scenarios
{
    /// <summary>
    /// <see cref="QaScenarioValidator.Validate"/> 호출 한 건의 불변 결과. 실패 시
    /// <see cref="Errors"/>에는 발견된 문제 전체가 담깁니다(첫 오류에서 멈추지 않고, Play Mode
    /// mutation을 시작하기 전에 가능한 모든 문제를 한 번에 보고합니다).
    /// </summary>
    public sealed class QaScenarioValidationResult
    {
        private static readonly IReadOnlyList<string> EmptyErrors = new ReadOnlyCollection<string>(new List<string>());

        public bool IsValid { get; }

        /// <summary><see cref="IsValid"/>일 때만 값이 있는, 검증을 통과한 시나리오.</summary>
        public QaScenarioDefinition Scenario { get; }

        /// <summary>발견된 검증 오류 전체. 절대 null이 아닙니다.</summary>
        public IReadOnlyList<string> Errors { get; }

        private QaScenarioValidationResult(bool isValid, QaScenarioDefinition scenario, IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Scenario = scenario;
            Errors = errors ?? EmptyErrors;
        }

        public static QaScenarioValidationResult Success(QaScenarioDefinition scenario)
        {
            return new QaScenarioValidationResult(true, scenario, EmptyErrors);
        }

        public static QaScenarioValidationResult Failure(IReadOnlyList<string> errors)
        {
            return new QaScenarioValidationResult(false, null, errors ?? EmptyErrors);
        }
    }

    /// <summary>
    /// 시나리오 JSON 스키마 v1의 엄격한 파서 겸 검증기(Task 9 §Step 1-2). 표준 Newtonsoft.Json
    /// POCO 바인딩만 사용하며(<c>TypeNameHandling.None</c>, 커스텀 <c>JsonConverter</c> 없음),
    /// JSON 문자열로부터 임의의 C# 타입/메서드/리플렉션 경로를 절대 실행하지 않습니다. 알려지지
    /// 않은 스키마 버전/명령/씬/프리셋/대상/어서션 종류, 중복 스텝 id, 0 이하의 타임아웃을
    /// Play Mode mutation이 시작되기 전에 전부 거부합니다. 씬/대상/프리셋의 존재 여부는
    /// 생성자로 주입된 <see cref="QaSceneRegistry"/>에게만 물어보므로(DIP), EditMode 테스트는
    /// 실제 씬을 로드하지 않고도 알려진 씬/대상/프리셋을 등록한 페이크 어댑터로 전체 규칙을
    /// 검증할 수 있습니다.
    /// </summary>
    public sealed class QaScenarioValidator
    {
        private static readonly JsonSerializerSettings SafeDeserializationSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        private readonly QaSceneRegistry sceneRegistry;

        public QaScenarioValidator(QaSceneRegistry sceneRegistry)
        {
            this.sceneRegistry = sceneRegistry ?? throw new ArgumentNullException(nameof(sceneRegistry));
        }

        /// <summary>
        /// 원본 JSON 문자열을 파싱하고 검증합니다. 파싱 자체가 실패하면(문법 오류 등) 그 사실만
        /// 담은 단일 오류로 실패를 반환합니다 — 예외를 호출자에게 전파하지 않습니다.
        /// </summary>
        public QaScenarioValidationResult Validate(string scenarioJson)
        {
            if (string.IsNullOrWhiteSpace(scenarioJson))
            {
                return QaScenarioValidationResult.Failure(new[] { "Scenario JSON must not be blank." });
            }

            QaScenarioDefinition scenario;
            try
            {
                scenario = JsonConvert.DeserializeObject<QaScenarioDefinition>(scenarioJson, SafeDeserializationSettings);
            }
            catch (JsonException ex)
            {
                return QaScenarioValidationResult.Failure(new[] { "Scenario JSON is malformed: " + ex.Message });
            }

            if (scenario == null)
            {
                return QaScenarioValidationResult.Failure(new[] { "Scenario JSON did not deserialize to an object." });
            }

            return Validate(scenario);
        }

        /// <summary>이미 역직렬화된 정의를 검증합니다(테스트에서 DTO를 직접 구성할 때 사용).</summary>
        public QaScenarioValidationResult Validate(QaScenarioDefinition scenario)
        {
            var errors = new List<string>();

            if (scenario == null)
            {
                errors.Add("Scenario must not be null.");
                return QaScenarioValidationResult.Failure(errors);
            }

            if (scenario.SchemaVersion != QaScenarioSchema.SupportedSchemaVersion)
            {
                errors.Add("Unknown/unsupported schemaVersion '" + scenario.SchemaVersion +
                    "'; only " + QaScenarioSchema.SupportedSchemaVersion + " is supported.");
            }

            if (string.IsNullOrWhiteSpace(scenario.Id))
            {
                errors.Add("Scenario 'id' must not be blank.");
            }

            IQaSceneAdapter sceneAdapter = null;
            if (string.IsNullOrWhiteSpace(scenario.Scene))
            {
                errors.Add("Scenario 'scene' must not be blank.");
            }
            else if (!sceneRegistry.TryResolveScene(scenario.Scene, out sceneAdapter))
            {
                errors.Add("Unknown scene '" + scenario.Scene + "'.");
            }

            if (!string.IsNullOrWhiteSpace(scenario.Preset) && sceneAdapter != null)
            {
                if (!ContainsOrdinal(sceneAdapter.PresetIds, scenario.Preset))
                {
                    errors.Add("Unknown preset '" + scenario.Preset + "' for scene '" + scenario.Scene + "'.");
                }
            }

            if (scenario.Steps == null || scenario.Steps.Count == 0)
            {
                errors.Add("Scenario must declare at least one step.");
            }
            else
            {
                var seenStepIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < scenario.Steps.Count; i++)
                {
                    ValidateStep(scenario.Steps[i], i, seenStepIds, errors);
                }
            }

            return errors.Count == 0
                ? QaScenarioValidationResult.Success(scenario)
                : QaScenarioValidationResult.Failure(errors);
        }

        private void ValidateStep(
            QaScenarioStepDefinition step, int index, HashSet<string> seenStepIds, List<string> errors)
        {
            if (step == null)
            {
                errors.Add("Step[" + index + "] must not be null.");
                return;
            }

            string label = string.IsNullOrWhiteSpace(step.Id)
                ? "Step[" + index + "]"
                : "Step '" + step.Id + "'";

            if (string.IsNullOrWhiteSpace(step.Id))
            {
                errors.Add(label + ": 'id' must not be blank.");
            }
            else if (!seenStepIds.Add(step.Id))
            {
                errors.Add(label + ": duplicate step id '" + step.Id + "'.");
            }

            if (step.TimeoutMs <= 0)
            {
                errors.Add(label + ": 'timeoutMs' must be a positive number, was " + step.TimeoutMs + ".");
            }

            if (string.IsNullOrWhiteSpace(step.Command)
                || !QaScenarioSchema.CommandKindsByName.TryGetValue(step.Command, out QaScenarioCommandKind commandKind))
            {
                errors.Add(label + ": unknown command '" + step.Command + "'.");
                return;
            }

            switch (commandKind)
            {
                case QaScenarioCommandKind.InteractionPointer:
                    ValidateTargetReference(step.Target, label, "target", errors);
                    break;
                case QaScenarioCommandKind.InteractionKey:
                    ValidateTargetReference(step.Target, label, "target", errors);
                    if (step.Text == null)
                    {
                        errors.Add(label + ": 'text' is required for interaction.key.");
                    }

                    break;
                case QaScenarioCommandKind.InteractionDrag:
                    ValidateTargetReference(step.Target, label, "target", errors);
                    ValidateTargetReference(step.DestinationTarget, label, "destinationTarget", errors);
                    break;
                case QaScenarioCommandKind.StateAssert:
                    ValidateAssertion(step.Assertion, label, errors);
                    break;
            }
        }

        private void ValidateTargetReference(string rawTargetId, string label, string fieldName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(rawTargetId))
            {
                errors.Add(label + ": '" + fieldName + "' must not be blank.");
                return;
            }

            if (!QaTargetId.TryCreate(rawTargetId, out QaTargetId targetId, out string parseError))
            {
                errors.Add(label + ": invalid " + fieldName + " '" + rawTargetId + "' (" + parseError + ").");
                return;
            }

            if (!sceneRegistry.TryResolveTarget(targetId, out _))
            {
                errors.Add(label + ": unknown " + fieldName + " '" + rawTargetId + "'.");
            }
        }

        private void ValidateAssertion(QaScenarioAssertionDefinition assertion, string label, List<string> errors)
        {
            if (assertion == null)
            {
                errors.Add(label + ": 'assertion' is required for state.assert steps.");
                return;
            }

            if (string.IsNullOrWhiteSpace(assertion.Kind)
                || !QaScenarioSchema.AssertionKindsByName.TryGetValue(assertion.Kind, out QaAssertionKind kind))
            {
                errors.Add(label + ": unknown assertion kind '" + assertion.Kind + "'.");
                return;
            }

            switch (kind)
            {
                case QaAssertionKind.FieldEquals:
                case QaAssertionKind.FieldBoolean:
                    if (string.IsNullOrWhiteSpace(assertion.Field))
                    {
                        errors.Add(label + ": assertion 'field' is required for '" + assertion.Kind + "'.");
                    }

                    break;
                case QaAssertionKind.InventoryContains:
                    if (string.IsNullOrWhiteSpace(assertion.Value) || !int.TryParse(assertion.Value, out _))
                    {
                        errors.Add(label + ": assertion 'value' must be an integer item id for 'inventoryContains'.");
                    }

                    break;
                case QaAssertionKind.TargetActive:
                    ValidateTargetReference(assertion.Value, label, "assertion.value", errors);
                    break;
                case QaAssertionKind.TargetInteractable:
                    ValidateTargetReference(assertion.Value, label, "assertion.value", errors);
                    break;
                case QaAssertionKind.FlowchartIdle:
                    if (string.IsNullOrWhiteSpace(assertion.Value))
                    {
                        errors.Add(label + ": assertion 'value' (flowchart name) is required for 'flowchartIdle'.");
                    }

                    break;
                case QaAssertionKind.QuestCurrentStepEquals:
                case QaAssertionKind.QuestStepCompleted:
                    if (string.IsNullOrWhiteSpace(assertion.Value))
                    {
                        errors.Add(label + ": assertion 'value' (quest step id) is required for '" + assertion.Kind + "'.");
                    }

                    break;
                case QaAssertionKind.InputUnlocked:
                case QaAssertionKind.NoNewConsoleError:
                    break;
            }
        }

        private static bool ContainsOrdinal(IReadOnlyCollection<string> values, string candidate)
        {
            if (values == null)
            {
                return false;
            }

            foreach (string value in values)
            {
                if (string.Equals(value, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
