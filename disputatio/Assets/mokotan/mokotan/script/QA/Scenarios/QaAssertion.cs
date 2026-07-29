#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using Godlotto.QA.Evidence;

namespace Godlotto.QA.Scenarios
{
    /// <summary>
    /// <see cref="QaAssertion"/>이 지원하는 명시적 어서션 종류(Task 8 §Step 2). 새 의미가
    /// 필요하면 이 열거형에 값을 추가하고 <see cref="QaAssertion.Evaluate"/>에 분기를
    /// 더합니다 — 임의의 C# 람다/리플렉션 셀렉터는 절대 허용하지 않습니다(시나리오
    /// JSON 등으로부터 안전하게 재구성 가능해야 함).
    /// </summary>
    public enum QaAssertionKind
    {
        /// <summary><see cref="QaDriverSnapshot.Values"/>의 한 필드가 특정 문자열과 같은지(동등성).</summary>
        FieldEquals,

        /// <summary><see cref="QaDriverSnapshot.Values"/>의 한 필드를 불리언으로 해석해 기대값과 비교.</summary>
        FieldBoolean,

        /// <summary>인벤토리가 특정 아이템 id를 보유하는지.</summary>
        InventoryContains,

        /// <summary>안정적 대상/패널 id가 활성 상태인지.</summary>
        TargetActive,

        /// <summary>안정적 대상 id가 상호작용 가능한지.</summary>
        TargetInteractable,

        /// <summary>현재 퀘스트 단계 id가 기대값과 같은지.</summary>
        QuestCurrentStepEquals,

        /// <summary>특정 퀘스트 단계 id가 완료되었는지.</summary>
        QuestStepCompleted,

        /// <summary>전역 입력 게이트가 잠겨있지 않은지.</summary>
        InputUnlocked,

        /// <summary>지정한 Fungus Flowchart가 idle(실행 중인 블록 없음) 상태인지.</summary>
        FlowchartIdle,

        /// <summary>기준(baseline) 스냅샷 이후 새로운 Console 오류가 발생하지 않았는지.</summary>
        NoNewConsoleError
    }

    /// <summary>
    /// <see cref="QaAssertion.Evaluate"/> 호출 한 건의 불변 결과. <see cref="ObservedValue"/>는
    /// 타임아웃 진단(디자인 문서 Task 8 인터페이스: "last observed value")에 사용되므로,
    /// 실패했을 때도 항상 사람이 읽을 수 있는 관측값을 남깁니다.
    /// </summary>
    public sealed class QaAssertionResult
    {
        public bool Passed { get; }

        public string Message { get; }

        /// <summary>평가 시점에 실제로 관측된 값의 사람이 읽을 수 있는 표현. 절대 null이 아닙니다.</summary>
        public string ObservedValue { get; }

        private QaAssertionResult(bool passed, string message, string observedValue)
        {
            Passed = passed;
            Message = message ?? string.Empty;
            ObservedValue = observedValue ?? string.Empty;
        }

        public static QaAssertionResult Pass(string message, string observedValue)
        {
            return new QaAssertionResult(true, message, observedValue);
        }

        public static QaAssertionResult Fail(string message, string observedValue)
        {
            return new QaAssertionResult(false, message, observedValue);
        }
    }

    /// <summary>
    /// 타입 있는 QA 어서션(Task 8 §Step 2). 명시적 <see cref="QaAssertionKind"/>와 기대값만으로
    /// 구성되는 불변 값 타입이며, <see cref="Evaluate"/>는 <see cref="QaDriverSnapshot"/>(그리고
    /// <see cref="QaAssertionKind.NoNewConsoleError"/>일 때만 필요한 <c>baseline</c>)만으로 동작하는
    /// 순수 함수로, 절대 예외를 던지지 않습니다(Fail-Safe: 알 수 없는 필드/대상은 명시적 실패로
    /// 처리되지, 크래시로 이어지지 않습니다).
    /// </summary>
    public sealed class QaAssertion
    {
        public QaAssertionKind Kind { get; }

        /// <summary><see cref="QaAssertionKind.FieldEquals"/>/<see cref="QaAssertionKind.FieldBoolean"/>에서 조회할 필드 이름.</summary>
        public string FieldName { get; }

        /// <summary>
        /// 종류에 따라 대상/패널/Flowchart/퀘스트 단계 id, 또는 인벤토리 아이템 id의 문자열
        /// 표현을 담습니다. 해당하지 않는 종류에서는 빈 문자열입니다.
        /// </summary>
        public string TargetId { get; }

        /// <summary><see cref="QaAssertionKind.FieldEquals"/>의 기대 문자열.</summary>
        public string ExpectedString { get; }

        /// <summary>불리언 기대값을 쓰는 종류(FieldBoolean/TargetActive/TargetInteractable/FlowchartIdle)의 기대값.</summary>
        public bool ExpectedBool { get; }

        /// <summary>사람이 읽을 수 있는 어서션 설명. 생략하면 종류·파라미터로부터 자동 생성됩니다.</summary>
        public string Description { get; }

        private QaAssertion(
            QaAssertionKind kind,
            string fieldName,
            string targetId,
            string expectedString,
            bool expectedBool,
            string description)
        {
            Kind = kind;
            FieldName = fieldName ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            ExpectedString = expectedString ?? string.Empty;
            ExpectedBool = expectedBool;
            Description = !string.IsNullOrWhiteSpace(description)
                ? description
                : BuildDefaultDescription(kind, FieldName, TargetId, ExpectedString, expectedBool);
        }

        public static QaAssertion FieldEquals(string fieldName, string expected, string description = null)
        {
            RequireNonBlank(fieldName, nameof(fieldName));
            return new QaAssertion(QaAssertionKind.FieldEquals, fieldName, null, expected, default, description);
        }

        public static QaAssertion FieldBoolean(string fieldName, bool expected, string description = null)
        {
            RequireNonBlank(fieldName, nameof(fieldName));
            return new QaAssertion(QaAssertionKind.FieldBoolean, fieldName, null, null, expected, description);
        }

        public static QaAssertion InventoryContains(int itemId, string description = null)
        {
            return new QaAssertion(
                QaAssertionKind.InventoryContains, null, itemId.ToString(CultureInfo.InvariantCulture),
                null, default, description);
        }

        public static QaAssertion TargetActive(string targetId, bool expected = true, string description = null)
        {
            RequireNonBlank(targetId, nameof(targetId));
            return new QaAssertion(QaAssertionKind.TargetActive, null, targetId, null, expected, description);
        }

        public static QaAssertion TargetInteractable(string targetId, bool expected = true, string description = null)
        {
            RequireNonBlank(targetId, nameof(targetId));
            return new QaAssertion(QaAssertionKind.TargetInteractable, null, targetId, null, expected, description);
        }

        public static QaAssertion QuestCurrentStepEquals(string stepId, string description = null)
        {
            RequireNonBlank(stepId, nameof(stepId));
            return new QaAssertion(QaAssertionKind.QuestCurrentStepEquals, null, stepId, null, default, description);
        }

        public static QaAssertion QuestStepCompleted(string stepId, string description = null)
        {
            RequireNonBlank(stepId, nameof(stepId));
            return new QaAssertion(QaAssertionKind.QuestStepCompleted, null, stepId, null, default, description);
        }

        public static QaAssertion InputUnlocked(string description = null)
        {
            return new QaAssertion(QaAssertionKind.InputUnlocked, null, null, null, default, description);
        }

        public static QaAssertion FlowchartIdle(string flowchartName, bool expected = true, string description = null)
        {
            RequireNonBlank(flowchartName, nameof(flowchartName));
            return new QaAssertion(QaAssertionKind.FlowchartIdle, null, flowchartName, null, expected, description);
        }

        public static QaAssertion NoNewConsoleError(string description = null)
        {
            return new QaAssertion(QaAssertionKind.NoNewConsoleError, null, null, null, default, description);
        }

        /// <summary>
        /// 이 어서션을 <paramref name="current"/>에 대해 평가합니다. <see cref="QaAssertionKind.NoNewConsoleError"/>일
        /// 때만 <paramref name="baseline"/>을 사용하며, 생략하면 <c>ConsoleErrorCount == 0</c>인
        /// 기준선을 가정합니다(Fail-Safe: 기준선을 모르면 현재 오류가 하나라도 있으면 실패로 취급).
        /// </summary>
        public QaAssertionResult Evaluate(QaDriverSnapshot current, QaDriverSnapshot baseline = null)
        {
            if (current == null)
            {
                return QaAssertionResult.Fail(Description + " -- no snapshot was captured.", "(null snapshot)");
            }

            switch (Kind)
            {
                case QaAssertionKind.FieldEquals:
                    return EvaluateFieldEquals(current);
                case QaAssertionKind.FieldBoolean:
                    return EvaluateFieldBoolean(current);
                case QaAssertionKind.InventoryContains:
                    return EvaluateInventoryContains(current);
                case QaAssertionKind.TargetActive:
                    return EvaluateFlagMap(current.TargetActiveStates, "target active");
                case QaAssertionKind.TargetInteractable:
                    return EvaluateFlagMap(current.TargetInteractableStates, "target interactable");
                case QaAssertionKind.FlowchartIdle:
                    return EvaluateFlagMap(current.FlowchartIdleStates, "flowchart idle");
                case QaAssertionKind.QuestCurrentStepEquals:
                    return EvaluateQuestCurrentStep(current);
                case QaAssertionKind.QuestStepCompleted:
                    return EvaluateQuestStepCompleted(current);
                case QaAssertionKind.InputUnlocked:
                    return EvaluateInputUnlocked(current);
                case QaAssertionKind.NoNewConsoleError:
                    return EvaluateNoNewConsoleError(current, baseline);
                default:
                    return QaAssertionResult.Fail(
                        Description + " -- unsupported assertion kind '" + Kind + "'.", "(unsupported)");
            }
        }

        // -----------------------------------------------------------------------------------
        //  Evaluators
        // -----------------------------------------------------------------------------------

        private QaAssertionResult EvaluateFieldEquals(QaDriverSnapshot current)
        {
            if (!current.Values.TryGetValue(FieldName, out string actual))
            {
                return QaAssertionResult.Fail(
                    Description + " -- field '" + FieldName + "' was not present in the snapshot.", "(missing)");
            }

            bool passed = string.Equals(actual, ExpectedString, StringComparison.Ordinal);
            return passed
                ? QaAssertionResult.Pass(Description + " -- matched.", actual)
                : QaAssertionResult.Fail(
                    Description + " -- expected '" + ExpectedString + "' but observed '" + actual + "'.", actual);
        }

        private QaAssertionResult EvaluateFieldBoolean(QaDriverSnapshot current)
        {
            if (!current.Values.TryGetValue(FieldName, out string actualText)
                || !bool.TryParse(actualText, out bool actual))
            {
                return QaAssertionResult.Fail(
                    Description + " -- field '" + FieldName + "' was missing or not a boolean.", "(missing)");
            }

            return actual == ExpectedBool
                ? QaAssertionResult.Pass(Description + " -- matched.", actualText)
                : QaAssertionResult.Fail(
                    Description + " -- expected " + ExpectedBool + " but observed " + actual + ".", actualText);
        }

        private QaAssertionResult EvaluateInventoryContains(QaDriverSnapshot current)
        {
            string observed = FormatIntList(current.InventoryItemIds);

            if (!int.TryParse(TargetId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemId))
            {
                return QaAssertionResult.Fail(Description + " -- item id '" + TargetId + "' is not a valid integer.", observed);
            }

            foreach (int candidate in current.InventoryItemIds)
            {
                if (candidate == itemId)
                {
                    return QaAssertionResult.Pass(Description + " -- item present.", observed);
                }
            }

            return QaAssertionResult.Fail(Description + " -- item not found in inventory.", observed);
        }

        private QaAssertionResult EvaluateFlagMap(System.Collections.Generic.IReadOnlyDictionary<string, bool> map, string humanLabel)
        {
            if (!map.TryGetValue(TargetId, out bool actual))
            {
                return QaAssertionResult.Fail(
                    Description + " -- '" + TargetId + "' was not found in " + humanLabel + " states.", "(unknown)");
            }

            string observed = actual.ToString(CultureInfo.InvariantCulture);
            return actual == ExpectedBool
                ? QaAssertionResult.Pass(Description + " -- matched.", observed)
                : QaAssertionResult.Fail(
                    Description + " -- expected " + ExpectedBool + " but observed " + actual + ".", observed);
        }

        private QaAssertionResult EvaluateQuestCurrentStep(QaDriverSnapshot current)
        {
            string observed = current.QuestCurrentStepId;
            bool passed = string.Equals(observed, TargetId, StringComparison.Ordinal);
            return passed
                ? QaAssertionResult.Pass(Description + " -- matched.", observed)
                : QaAssertionResult.Fail(
                    Description + " -- expected '" + TargetId + "' but observed '" + observed + "'.", observed);
        }

        private QaAssertionResult EvaluateQuestStepCompleted(QaDriverSnapshot current)
        {
            string observed = FormatStringList(current.QuestCompletedStepIds);

            foreach (string completedStepId in current.QuestCompletedStepIds)
            {
                if (string.Equals(completedStepId, TargetId, StringComparison.Ordinal))
                {
                    return QaAssertionResult.Pass(Description + " -- step completed.", observed);
                }
            }

            return QaAssertionResult.Fail(Description + " -- step not yet completed.", observed);
        }

        private QaAssertionResult EvaluateInputUnlocked(QaDriverSnapshot current)
        {
            string observed = current.InputGateLocked.ToString(CultureInfo.InvariantCulture);
            return !current.InputGateLocked
                ? QaAssertionResult.Pass(Description + " -- input gate is open.", observed)
                : QaAssertionResult.Fail(Description + " -- input gate is still locked.", observed);
        }

        private static QaAssertionResult EvaluateNoNewConsoleError(QaDriverSnapshot current, QaDriverSnapshot baseline)
        {
            int baselineCount = baseline?.ConsoleErrorCount ?? 0;
            int currentCount = current.ConsoleErrorCount;
            string observed = currentCount.ToString(CultureInfo.InvariantCulture) +
                " (baseline " + baselineCount.ToString(CultureInfo.InvariantCulture) + ")";

            return currentCount <= baselineCount
                ? QaAssertionResult.Pass("No new console errors since baseline.", observed)
                : QaAssertionResult.Fail(
                    "Console error count increased from " + baselineCount + " to " + currentCount + ".", observed);
        }

        // -----------------------------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------------------------

        private static void RequireNonBlank(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(paramName + " must not be blank.", paramName);
            }
        }

        private static string FormatIntList(System.Collections.Generic.IReadOnlyList<int> values)
        {
            if (values.Count == 0)
            {
                return "(empty)";
            }

            var parts = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private static string FormatStringList(System.Collections.Generic.IReadOnlyList<string> values)
        {
            return values.Count == 0 ? "(empty)" : string.Join(",", values);
        }

        private static string BuildDefaultDescription(
            QaAssertionKind kind, string fieldName, string targetId, string expectedString, bool expectedBool)
        {
            switch (kind)
            {
                case QaAssertionKind.FieldEquals:
                    return "Field '" + fieldName + "' equals '" + expectedString + "'";
                case QaAssertionKind.FieldBoolean:
                    return "Field '" + fieldName + "' is " + expectedBool;
                case QaAssertionKind.InventoryContains:
                    return "Inventory contains item id " + targetId;
                case QaAssertionKind.TargetActive:
                    return "Target '" + targetId + "' active == " + expectedBool;
                case QaAssertionKind.TargetInteractable:
                    return "Target '" + targetId + "' interactable == " + expectedBool;
                case QaAssertionKind.QuestCurrentStepEquals:
                    return "Quest current step equals '" + targetId + "'";
                case QaAssertionKind.QuestStepCompleted:
                    return "Quest step '" + targetId + "' is completed";
                case QaAssertionKind.InputUnlocked:
                    return "Input gate is unlocked";
                case QaAssertionKind.FlowchartIdle:
                    return "Flowchart '" + targetId + "' idle == " + expectedBool;
                case QaAssertionKind.NoNewConsoleError:
                    return "No new console errors since baseline";
                default:
                    return kind.ToString();
            }
        }
    }
}
#endif
